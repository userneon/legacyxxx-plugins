using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PanoramaManager;

namespace LegacyXHud;

// The Skins tab of the menu, laid out like the website's Skinchanger page: a team tab (Terrorist / Counter-Terrorist), the
// loadout overview (every gun by column, then Agent, Gloves and Knife, each with what is equipped), and, after a click, the
// skins of that pick with "your choice" on the right and an Equip button. Knives and gloves go through a list of types first.
// The pick is saved to the website loadout through the plugin API and applied at once through SkinBridge's !rs.
// Everything shown comes from the API (the same catalog the website uses); nothing is listed here by hand except the
// picture names the game's own files use (class names, see CONTRACT.md).
// Reading and saving use SkinBridge's credentials (LEGACYX_SKINBRIDGE_*), which already carry skinchanger:read and :write.
public sealed partial class LegacyXHud
{
    private const int SkinTypeSlots = 25;
    private const int SkinItemSlots = 9;
    private const int TileSlots = 35;            // 4 columns of 8 guns, then Agent, Gloves, Knife
    private static readonly int[] ColumnStart = { 0, 8, 16, 24, 32 };
    private static readonly int[] ColumnSize = { 8, 8, 8, 8, 3 };

    private string skinPluginId = "legacyx-skinbridge";
    private string skinSecret = "";
    private bool SkinsReady => apiBase.Length > 0 && skinSecret.Length > 0;

    private sealed record Equipped(string ItemId, string WeaponClass, string Skin, int? PaintId, int? Defindex, string? Rarity);
    private sealed record Tile(int Column, string Slot, string WeaponClass, string Label, string? Model, Equipped? Eq);
    private sealed record SkinType(string WeaponClass, int Skins);
    private sealed record SkinItem(string Id, string Name, int? PaintId, int? Defindex, string? Rarity, string RarityName);

    private sealed class SkinView
    {
        public string Team = "t";
        public string Mode = "loadout";                    // loadout | types | items
        public Tile?[] Slots = new Tile?[TileSlots];       // the overview, by pool position
        public Tile? Picked;                               // the tile that was clicked
        public List<SkinType> Types = new();               // knife / glove types
        public string? TypeClass;                          // the picked knife / glove type
        public int Offset;
        public int Total;
        public List<SkinItem> Items = new();
        public int Selected = -1;
        public bool Busy;
    }

    private readonly Dictionary<int, SkinView> skinViews = new();

    // The glove models of the skin pictures (class sp-<model>-<paint id>); knives use the names below.
    private static readonly Dictionary<string, string> GloveModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bloodhound Gloves"] = "bloodhound", ["Broken Fang Gloves"] = "brokenfang", ["Driver Gloves"] = "driver", ["Hand Wraps"] = "handwraps",
        ["Hydra Gloves"] = "hydra", ["Moto Gloves"] = "moto", ["Specialist Gloves"] = "specialist", ["Sport Gloves"] = "sport",
    };

    // The knife pictures baked into the Workshop addon (class kn-<name>, see CONTRACT.md); the server cannot send an image.
    private static readonly Dictionary<string, string> KnifeIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bayonet"] = "bayonet", ["Bowie Knife"] = "bowie", ["Butterfly Knife"] = "butterfly", ["Classic Knife"] = "classic",
        ["Falchion Knife"] = "falchion", ["Flip Knife"] = "flip", ["Gut Knife"] = "gut", ["Huntsman Knife"] = "huntsman",
        ["Karambit"] = "karambit", ["Kukri Knife"] = "kukri", ["M9 Bayonet"] = "m9-bayonet", ["Navaja Knife"] = "navaja",
        ["Nomad Knife"] = "nomad", ["Paracord Knife"] = "paracord", ["Shadow Daggers"] = "shadow-daggers", ["Skeleton Knife"] = "skeleton",
        ["Stiletto Knife"] = "stiletto", ["Survival Knife"] = "survival", ["Talon Knife"] = "talon", ["Ursus Knife"] = "ursus",
    };

    // ---- API ------------------------------------------------------------------------------------------

    private async Task<(int Status, JsonElement? Body)> SkinsApiAsync(HttpMethod method, string path, string? json = null)
    {
        using var request = new HttpRequestMessage(method, $"{apiBase}{path}");
        request.Headers.Add("x-plugin-id", skinPluginId);
        request.Headers.Add("x-plugin-secret", skinSecret);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        JsonElement? body = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            body = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON: the status alone tells what happened.
        }
        return ((int)response.StatusCode, body);
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    /// <summary>Runs an API read away from the game thread, then hands the result to <paramref name="done"/> on it, if the player is still there.</summary>
    private void SkinsFetch(CCSPlayerController player, string path, Action<CCSPlayerController, SkinView, int, JsonElement?> done)
    {
        var slot = player.Slot;
        _ = Task.Run(async () =>
        {
            int status = 0;
            JsonElement? body = null;
            try
            {
                (status, body) = await SkinsApiAsync(HttpMethod.Get, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Skins request failed: {ex.Message}");
            }
            Server.NextFrame(() =>
            {
                var p = Utilities.GetPlayerFromSlot(slot);
                if (p is not { IsValid: true } || menu is null || !skinViews.TryGetValue(slot, out var view)) return;
                done(p, view, status, body);
            });
        });
    }

    private static bool SkinsFailed(int status, JsonElement? body, out JsonElement json, out string message)
    {
        json = default;
        message = "";
        if (status == 404) message = "Sign in on legacyx.cc with this Steam account to pick skins.";
        else if (status != 200 || body is not { } found) message = "Could not load skins. Try again.";
        else json = found;
        return message.Length > 0;
    }

    // ---- opening and navigation -------------------------------------------------------------------------

    private SkinView SkinViewFor(CCSPlayerController player)
    {
        if (!skinViews.TryGetValue(player.Slot, out var view)) skinViews[player.Slot] = view = new SkinView();
        return view;
    }

    private void SkinsStatus(PanelHandle panel, CCSPlayerController player, string text) => panel.SetVariableFor(player, "sk_status", text);

    /// <summary>The Skins tab was opened, or the other team was picked: load the overview of that team.</summary>
    private void SkinsOpen(PanelHandle panel, CCSPlayerController player, string? team = null)
    {
        var view = SkinViewFor(player);
        if (team is not null) view.Team = team;
        view.Mode = "loadout";
        view.Picked = null;
        view.Selected = -1;
        if (!SkinsReady)
        {
            SkinsStatus(panel, player, "Skins are not available on this server right now.");
            return;
        }
        SkinsStatus(panel, player, "Loading ...");
        SkinsRender(panel, player, view);
        var wantedTeam = view.Team;
        SkinsFetch(player, $"/api/v1/plugin/menu/skins/loadout?steam_id={player.SteamID}&team={wantedTeam}", (p, current, status, body) =>
        {
            if (current.Team != wantedTeam) return;
            if (SkinsFailed(status, body, out var json, out var message))
            {
                SkinsStatus(menu!, p, message);
                return;
            }
            current.Slots = new Tile?[TileSlots];
            var rows = new int[ColumnStart.Length];
            foreach (var t in json.GetProperty("tiles").EnumerateArray())
            {
                var column = Math.Clamp(Number(t, "column") ?? 0, 0, ColumnStart.Length - 1);
                if (rows[column] >= ColumnSize[column]) continue;
                Equipped? eq = null;
                if (t.TryGetProperty("equipped", out var e) && e.ValueKind == JsonValueKind.Object)
                    eq = new Equipped(Text(e, "itemId") ?? "", Text(e, "weaponClass") ?? "", Text(e, "skin") ?? "", Number(e, "paintId"), Number(e, "defindex"), Text(e, "rarity"));
                current.Slots[ColumnStart[column] + rows[column]++] = new Tile(column, Text(t, "slot") ?? "", Text(t, "weaponClass") ?? "", Text(t, "label") ?? "", Text(t, "model"), eq);
            }
            SkinsStatus(menu!, p, "");
            SkinsRender(menu!, p, current);
        });
    }

    /// <summary>A knife or glove tile was clicked: list the types first.</summary>
    private void SkinsLoadTypes(PanelHandle panel, CCSPlayerController player, SkinView view, Tile tile)
    {
        view.Picked = tile;
        view.Mode = "types";
        view.Types = new();
        SkinsStatus(panel, player, "Loading ...");
        SkinsRender(panel, player, view);
        SkinsFetch(player, $"/api/v1/plugin/menu/skins/types?steam_id={player.SteamID}&slot={tile.Slot}", (p, current, status, body) =>
        {
            if (current.Picked != tile) return;
            if (SkinsFailed(status, body, out var json, out var message))
            {
                SkinsStatus(menu!, p, message);
                return;
            }
            current.Types = json.GetProperty("types").EnumerateArray()
                .Select(t => new SkinType(Text(t, "weaponClass") ?? "", Number(t, "skins") ?? 0))
                .Where(t => t.WeaponClass.Length > 0).Take(SkinTypeSlots).ToList();
            SkinsStatus(menu!, p, "");
            SkinsRender(menu!, p, current);
        });
    }

    /// <summary>The skins of a pick (a gun, the agents of a team, a knife or glove type), one page.</summary>
    private void SkinsLoadItems(PanelHandle panel, CCSPlayerController player, SkinView view, Tile tile, string weaponClass, int offset)
    {
        view.Picked = tile;
        view.Mode = "items";
        view.TypeClass = tile.Slot is "knife" or "glove" ? weaponClass : null;
        view.Offset = Math.Max(0, offset);
        view.Selected = -1;
        SkinsStatus(panel, player, "Loading ...");
        var wanted = view.Offset;
        var path = $"/api/v1/plugin/menu/skins/items?steam_id={player.SteamID}&slot={tile.Slot}&weapon_class={Uri.EscapeDataString(weaponClass)}&offset={wanted}";
        SkinsFetch(player, path, (p, current, status, body) =>
        {
            if (current.Picked != tile || current.Mode != "items" || current.Offset != wanted) return;
            if (SkinsFailed(status, body, out var json, out var message))
            {
                SkinsStatus(menu!, p, message);
                return;
            }
            current.Total = json.GetProperty("total").GetInt32();
            current.Items = json.GetProperty("items").EnumerateArray()
                .Select(i => new SkinItem(Text(i, "id") ?? "", Text(i, "name") ?? "", Number(i, "paintId"), Number(i, "defindex"), Text(i, "rarity"), Text(i, "rarityName") ?? ""))
                .Where(i => i.Id.Length > 0).Take(SkinItemSlots).ToList();
            SkinsStatus(menu!, p, "");
            SkinsRender(menu!, p, current);
        });
    }

    private void SkinsEquip(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        if (view.Busy || view.Picked is not { } tile) return;
        if (view.Selected < 0 || view.Selected >= view.Items.Count)
        {
            SkinsStatus(panel, player, "Pick a skin first.");
            return;
        }
        view.Busy = true;
        var item = view.Items[view.Selected];
        var team = view.Team;       // the tab the pick was made on: it is saved for that team only
        var steamId = player.SteamID;
        var playerSlot = player.Slot;
        SkinsStatus(panel, player, "Saving ...");
        _ = Task.Run(async () =>
        {
            string message;
            string label = "";
            var saved = false;
            try
            {
                var json = JsonSerializer.Serialize(new { steam_id = steamId.ToString(), catalog_item_id = item.Id, team });
                var (status, body) = await SkinsApiAsync(HttpMethod.Post, "/api/v1/plugin/menu/skins/equip", json);
                if (status == 200 && body is { } ok)
                {
                    saved = true;
                    label = Text(ok, "label") ?? item.Name;
                    message = $"Equipped {label}.";
                }
                else if (status == 404) message = "Sign in on legacyx.cc with this Steam account to pick skins.";
                else if (status == 409) message = "The server does not see you yet. Try again in a few seconds.";
                else
                {
                    Console.WriteLine($"[{ModuleName}] Skin equip refused: HTTP {status} {body}");
                    message = $"Could not save (error {status}). Tell staff.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Skin equip failed: {ex.Message}");
                message = "Could not save. Try again.";
            }
            Server.NextFrame(() =>
            {
                if (skinViews.TryGetValue(playerSlot, out var current)) current.Busy = false;
                var p = Utilities.GetPlayerFromSlot(playerSlot);
                if (p is not { IsValid: true } || menu is null) return;
                SkinsStatus(menu, p, message);
                if (!saved || current is null) return;
                // The overview shows the new pick at once (it is read again whenever the tab is opened).
                var weaponClass = current.TypeClass ?? tile.WeaponClass;
                var newTile = tile with { Eq = new Equipped(item.Id, weaponClass, item.Name, item.PaintId, item.Defindex, item.Rarity) };
                for (var i = 0; i < current.Slots.Length; i++)
                    if (current.Slots[i] == tile) current.Slots[i] = newTile;
                current.Picked = newTile;
                SkinsRender(menu, p, current);
                MenuSound(p, soundPick, LevelPick);
                // Same as typing !rs: SkinBridge reads the loadout from the API and applies it.
                p.ExecuteClientCommandFromServer("css_rs");
            });
        });
    }

    // ---- drawing ----------------------------------------------------------------------------------------

    private static string? SkinModel(string? weaponClass)
    {
        if (weaponClass is null) return null;
        if (KnifeIcons.TryGetValue(weaponClass, out var knife)) return knife;
        return GloveModels.TryGetValue(weaponClass, out var glove) ? glove : null;
    }

    /// <summary>The picture class of a skin: the game's own picture of that paint on that model (no class = no picture).</summary>
    private static string? SkinPicture(string slot, string? model, int? paintId, int? defindex)
    {
        if (slot == "agent") return defindex is { } id ? $"ag-{id}" : null;
        if (model is null) return null;
        if (paintId is { } paint) return $"sp-{model}-{paint}";
        return slot == "knife" ? $"sp-{model}-0" : null;   // a knife without a paint is the plain one
    }

    private static string TypeLabel(Tile tile, string weaponClass) =>
        tile.Slot == "knife" && weaponClass.EndsWith(" Knife", StringComparison.Ordinal) ? weaponClass[..^6] : weaponClass;

    private static string PickedName(SkinView view)
    {
        if (view.Picked is not { } tile) return "";
        return tile.Slot switch
        {
            "agent" => tile.Label == "Agent" ? (tile.WeaponClass == "Terrorist" ? "Terrorist agents" : "Counter-Terrorist agents") : tile.Label,
            "knife" or "glove" => view.TypeClass is { } type ? TypeLabel(tile, type) : tile.Label,
            _ => tile.Label,
        };
    }

    private void SkinsRender(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        panel.SetClassFor(player, "sk_team_t", "active", view.Team == "t");
        panel.SetClassFor(player, "sk_team_ct", "active", view.Team == "ct");
        panel.SetClassFor(player, "sk_view_loadout", "hidden", view.Mode != "loadout");
        panel.SetClassFor(player, "sk_view_types", "hidden", view.Mode != "types");
        panel.SetClassFor(player, "sk_view_items", "hidden", view.Mode != "items");
        if (view.Mode == "loadout") RenderLoadout(panel, player, view);
        else if (view.Mode == "types") RenderTypes(panel, player, view);
        else RenderItems(panel, player, view);
    }

    private void RenderLoadout(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        panel.SetVariableFor(player, "sk_eq_count", $"Equipped {view.Slots.Count(t => t?.Eq is not null)}");
        for (var i = 0; i < TileSlots; i++)
        {
            var tile = view.Slots[i];
            panel.SetClassFor(player, $"sk_w{i}", "hidden", tile is null);
            if (tile is null) continue;
            panel.SetVariableFor(player, $"sk_w{i}_name", tile.Label);
            var eq = tile.Eq;
            panel.SetClassFor(player, $"sk_w{i}_ck", "hidden", eq is null);
            SetState(panel, player, $"sk_w{i}_dot", "rc", eq?.Rarity is { } rarity ? "rc-" + rarity : null);
            string? pic;
            if (eq is not null)
                pic = SkinPicture(tile.Slot, tile.Slot == "weapon" ? tile.Model : SkinModel(eq.WeaponClass), eq.PaintId, eq.Defindex);
            else
                pic = tile.Slot == "weapon" && tile.Model is not null ? "gn-" + tile.Model : tile.Slot == "knife" ? "kn-karambit" : null;
            SetState(panel, player, $"sk_w{i}_pic", "sp", pic);
        }
    }

    private void RenderTypes(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        var tile = view.Picked;
        panel.SetVariableFor(player, "sk_ttitle", tile?.Slot == "glove" ? "Gloves" : "Knives");
        panel.SetVariableFor(player, "sk_equipped", tile?.Eq is { } eq ? $"Equipped: {eq.WeaponClass} | {eq.Skin}" : "Pick a type, then a skin.");
        for (var i = 0; i < SkinTypeSlots; i++)
        {
            var has = i < view.Types.Count && tile is not null;
            panel.SetClassFor(player, $"sk_type{i}", "hidden", !has);
            if (!has) continue;
            var type = view.Types[i];
            panel.SetVariableFor(player, $"sk_type{i}_name", TypeLabel(tile!, type.WeaponClass));
            panel.SetVariableFor(player, $"sk_type{i}_n", $"{type.Skins} skins");
            SetState(panel, player, $"sk_type{i}_ic", "kn", tile!.Slot == "knife" && KnifeIcons.TryGetValue(type.WeaponClass, out var knife) ? "kn-" + knife : null);
        }
    }

    private void RenderItems(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        var tile = view.Picked;
        var agent = tile?.Slot == "agent";
        panel.SetVariableFor(player, "sk_title", PickedName(view));
        panel.SetVariableFor(player, "sk_count", agent ? $"{view.Total} agents" : $"{view.Total} skins");
        var pages = Math.Max(1, (view.Total + SkinItemSlots - 1) / SkinItemSlots);
        panel.SetVariableFor(player, "sk_page", $"{view.Offset / SkinItemSlots + 1} / {pages}");
        panel.SetClassFor(player, "sk_prev", "disabled", view.Offset <= 0);
        panel.SetClassFor(player, "sk_next", "disabled", view.Offset + SkinItemSlots >= view.Total);
        var model = tile is null ? null : tile.Slot == "weapon" ? tile.Model : SkinModel(view.TypeClass);
        var eq = tile?.Eq;
        for (var i = 0; i < SkinItemSlots; i++)
        {
            var has = i < view.Items.Count && tile is not null;
            panel.SetClassFor(player, $"sk_item{i}", "hidden", !has);
            panel.SetClassFor(player, $"sk_item{i}", "sel", has && i == view.Selected);
            if (!has) continue;
            var item = view.Items[i];
            panel.SetVariableFor(player, $"sk_item{i}_name", item.Name);
            panel.SetVariableFor(player, $"sk_item{i}_rar", item.RarityName);
            SetState(panel, player, $"sk_item{i}_ln", "rc", item.Rarity is { } rarity ? "rc-" + rarity : null);
            SetState(panel, player, $"sk_item{i}_pic", "sp", SkinPicture(tile!.Slot, model, item.PaintId, item.Defindex));
            // "Saved": the pick the player already has on this gun / type / agent.
            var saved = eq is not null && (agent ? eq.Defindex is not null && eq.Defindex == item.Defindex
                : eq.PaintId == item.PaintId && (view.TypeClass is null || eq.WeaponClass == view.TypeClass));
            panel.SetClassFor(player, $"sk_item{i}_saved", "hidden", !saved);
        }
        var chosen = view.Selected >= 0 && view.Selected < view.Items.Count ? view.Items[view.Selected] : null;
        panel.SetVariableFor(player, "sk_pv_name", chosen is null ? "Pick a skin" : agent ? chosen.Name : $"{(view.TypeClass is { } type ? TypeLabel(tile!, type) : tile?.Label)} | {chosen.Name}");
        panel.SetVariableFor(player, "sk_pv_rar", chosen?.RarityName ?? "");
        SetState(panel, player, "sk_pv_pic", "sp", chosen is null || tile is null ? null : SkinPicture(tile.Slot, model, chosen.PaintId, chosen.Defindex));
    }

    /// <summary>Clicks of the Skins tab. Returns true when the id was one of its buttons.</summary>
    private bool SkinsClick(PanelHandle panel, CCSPlayerController player, string id)
    {
        var view = SkinViewFor(player);
        switch (id)
        {
            case "sk_team_t": SkinsOpen(panel, player, "t"); return true;
            case "sk_team_ct": SkinsOpen(panel, player, "ct"); return true;
            case "sk_tback":
                view.Mode = "loadout";
                view.Picked = null;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_back":
                view.Selected = -1;
                view.Mode = view.TypeClass is not null ? "types" : "loadout";
                if (view.Mode == "loadout") view.Picked = null;
                view.TypeClass = null;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_cancel":
                view.Selected = -1;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_equip":
                SkinsEquip(panel, player, view);
                return true;
            case "sk_prev":
                if (view.Picked is { } back && view.Offset > 0) SkinsLoadItems(panel, player, view, back, PickedClass(view), view.Offset - SkinItemSlots);
                return true;
            case "sk_next":
                if (view.Picked is { } forward && view.Offset + SkinItemSlots < view.Total) SkinsLoadItems(panel, player, view, forward, PickedClass(view), view.Offset + SkinItemSlots);
                return true;
        }
        if (id.StartsWith("sk_w", StringComparison.Ordinal) && int.TryParse(id.AsSpan(4), out var slot) && slot >= 0 && slot < TileSlots)
        {
            if (view.Slots[slot] is not { } tile) return true;
            if (tile.Slot is "knife" or "glove") SkinsLoadTypes(panel, player, view, tile);
            else SkinsLoadItems(panel, player, view, tile, tile.WeaponClass, 0);
            return true;
        }
        if (id.StartsWith("sk_type", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var type) && type >= 0 && type < view.Types.Count && view.Picked is { } typeTile)
        {
            SkinsLoadItems(panel, player, view, typeTile, view.Types[type].WeaponClass, 0);
            return true;
        }
        if (id.StartsWith("sk_item", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var item) && item >= 0 && item < view.Items.Count)
        {
            view.Selected = item;
            SkinsStatus(panel, player, "");
            SkinsRender(panel, player, view);
            return true;
        }
        return false;
    }

    /// <summary>The class the current skin list was asked for: the knife / glove type, the gun, or the team of the agents.</summary>
    private static string PickedClass(SkinView view) => view.TypeClass ?? view.Picked?.WeaponClass ?? "";
}

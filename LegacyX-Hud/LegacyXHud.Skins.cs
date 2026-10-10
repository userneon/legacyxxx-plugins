using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PanoramaManager;

namespace LegacyXHud;

// The Skins tab of the menu: knives, gloves, guns and agents. The player picks a type (Karambit, Sport Gloves, AK-47, the
// Terrorist agents …), then one of its skins; the pick is saved to the website loadout through the plugin API and applied
// at once through SkinBridge's !rs. Everything shown comes from the API (the same catalog the website uses); nothing is
// listed here by hand except the picture names the game's own files use (class names, see CONTRACT.md).
// Reading and saving use SkinBridge's credentials (LEGACYX_SKINBRIDGE_*), which already carry skinchanger:read and :write.
public sealed partial class LegacyXHud
{
    private const int SkinTypeSlots = 25;
    private const int SkinItemSlots = 12;

    private string skinPluginId = "legacyx-skinbridge";
    private string skinSecret = "";
    private bool SkinsReady => apiBase.Length > 0 && skinSecret.Length > 0;

    private sealed record SkinType(string WeaponClass, int Skins, string? Model);
    private sealed record SkinItem(string Id, string Name, int? PaintId, int? Defindex);

    private sealed class SkinView
    {
        public string Slot = "knife";         // knife | glove | weapon | agent
        public string Group = "Rifles";       // the gun group while Slot is weapon
        public List<SkinType> Types = new();
        public string? WeaponClass;           // the picked type; for agents the team
        public string? Model;                 // the picked gun's model name (picture class)
        public int Offset;
        public int Total;
        public List<SkinItem> Items = new();
        public string Equipped = "";
        public bool Busy;
    }

    private readonly Dictionary<int, SkinView> skinViews = new();

    private static readonly string[] GunGroups = { "Rifles", "SMGs", "Heavy", "Pistols" };

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

    // ---- opening and navigation -------------------------------------------------------------------------

    private SkinView SkinViewFor(CCSPlayerController player)
    {
        if (!skinViews.TryGetValue(player.Slot, out var view)) skinViews[player.Slot] = view = new SkinView();
        return view;
    }

    private void SkinsStatus(PanelHandle panel, CCSPlayerController player, string text) => panel.SetVariableFor(player, "sk_status", text);

    /// <summary>The Skins tab was opened, or a slot (Knives / Gloves / Guns / Agents) or a gun group was picked: list the types.</summary>
    private void SkinsOpen(PanelHandle panel, CCSPlayerController player, string? slot = null, string? group = null)
    {
        var view = SkinViewFor(player);
        if ((slot is not null && slot != view.Slot) || (group is not null && group != view.Group))
        {
            if (slot is not null) view.Slot = slot;
            if (group is not null) view.Group = group;
            view.Types = new();
            view.WeaponClass = null;
            view.Model = null;
            view.Equipped = "";
        }
        if (!SkinsReady)
        {
            SkinsStatus(panel, player, "Skins are not available on this server right now.");
            return;
        }
        SkinsStatus(panel, player, "Loading ...");
        SkinsRender(panel, player, view);
        var steamId = player.SteamID;
        var playerSlot = player.Slot;
        var wantedSlot = view.Slot;
        var wantedGroup = view.Group;
        _ = Task.Run(async () =>
        {
            try
            {
                var path = $"/api/v1/plugin/menu/skins/types?steam_id={steamId}&slot={wantedSlot}" + (wantedSlot == "weapon" ? $"&group={wantedGroup}" : "");
                var (status, body) = await SkinsApiAsync(HttpMethod.Get, path);
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(playerSlot);
                    if (p is not { IsValid: true } || menu is null || !skinViews.TryGetValue(playerSlot, out var current) || current.Slot != wantedSlot || current.Group != wantedGroup) return;
                    if (status == 404)
                    {
                        SkinsStatus(menu, p, "Sign in on legacyx.cc with this Steam account to pick skins.");
                        return;
                    }
                    if (status != 200 || body is not { } json)
                    {
                        SkinsStatus(menu, p, "Could not load skins. Try again.");
                        return;
                    }
                    current.Types = json.GetProperty("types").EnumerateArray()
                        .Select(t => new SkinType(Text(t, "weaponClass") ?? "", Number(t, "skins") ?? 0, Text(t, "model")))
                        .Where(t => t.WeaponClass.Length > 0).Take(SkinTypeSlots).ToList();
                    current.Equipped = Text(json, "equippedText") ?? "";
                    current.WeaponClass = null;
                    SkinsStatus(menu, p, "");
                    SkinsRender(menu, p, current);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Skins list failed: {ex.Message}");
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(playerSlot);
                    if (p is { IsValid: true } && menu is not null) SkinsStatus(menu, p, "Could not load skins. Try again.");
                });
            }
        });
    }

    private void SkinsLoadItems(PanelHandle panel, CCSPlayerController player, SkinView view, SkinType type, int offset)
    {
        view.WeaponClass = type.WeaponClass;
        view.Model = type.Model;
        view.Offset = Math.Max(0, offset);
        SkinsStatus(panel, player, "Loading ...");
        var steamId = player.SteamID;
        var playerSlot = player.Slot;
        var slot = view.Slot;
        var weaponClass = type.WeaponClass;
        var wanted = view.Offset;
        _ = Task.Run(async () =>
        {
            try
            {
                var path = $"/api/v1/plugin/menu/skins/items?steam_id={steamId}&slot={slot}&weapon_class={Uri.EscapeDataString(weaponClass)}&offset={wanted}";
                var (status, body) = await SkinsApiAsync(HttpMethod.Get, path);
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(playerSlot);
                    if (p is not { IsValid: true } || menu is null || !skinViews.TryGetValue(playerSlot, out var current) || current.WeaponClass != weaponClass) return;
                    if (status != 200 || body is not { } json)
                    {
                        SkinsStatus(menu, p, "Could not load skins. Try again.");
                        return;
                    }
                    current.Total = json.GetProperty("total").GetInt32();
                    current.Offset = wanted;
                    current.Items = json.GetProperty("items").EnumerateArray()
                        .Select(i => new SkinItem(Text(i, "id") ?? "", Text(i, "name") ?? "", Number(i, "paintId"), Number(i, "defindex")))
                        .Where(i => i.Id.Length > 0).Take(SkinItemSlots).ToList();
                    SkinsStatus(menu, p, "");
                    SkinsRender(menu, p, current);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Skins page failed: {ex.Message}");
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(playerSlot);
                    if (p is { IsValid: true } && menu is not null) SkinsStatus(menu, p, "Could not load skins. Try again.");
                });
            }
        });
    }

    private void SkinsEquip(PanelHandle panel, CCSPlayerController player, SkinView view, int index)
    {
        if (view.Busy || index < 0 || index >= view.Items.Count) return;
        view.Busy = true;
        var item = view.Items[index];
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
                var json = JsonSerializer.Serialize(new { steam_id = steamId.ToString(), catalog_item_id = item.Id });
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
                if (!saved) return;
                // Knives and gloves are one pick per slot, so the list can say what is on; guns and agents are many.
                if (current is not null && current.Slot is "knife" or "glove") current.Equipped = label;
                menu.SetClassFor(p, $"sk_item{index}", "sel", true);
                MenuSound(p, soundPick);
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

    private static string TypeLabel(SkinView view, string weaponClass) =>
        view.Slot == "knife" && weaponClass.EndsWith(" Knife", StringComparison.Ordinal) ? weaponClass[..^6]
        : view.Slot == "agent" ? (weaponClass == "Terrorist" ? "Terrorists" : "Counter-Terrorists")
        : weaponClass;

    private void SkinsRender(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        foreach (var slot in new[] { "knife", "glove", "weapon", "agent" })
            panel.SetClassFor(player, $"sk_slot_{slot}", "active", view.Slot == slot);
        panel.SetClassFor(player, "sk_groups", "hidden", view.Slot != "weapon");
        foreach (var group in GunGroups)
            panel.SetClassFor(player, $"sk_grp_{group.ToLowerInvariant()}", "active", view.Group == group);
        var showItems = view.WeaponClass is not null;
        panel.SetClassFor(player, "sk_view_types", "hidden", showItems);
        panel.SetClassFor(player, "sk_view_items", "hidden", !showItems);
        if (!showItems)
        {
            panel.SetVariableFor(player, "sk_equipped", view.Equipped.Length > 0 ? $"Equipped: {view.Equipped}" : "Pick a type, then a skin.");
            for (var i = 0; i < SkinTypeSlots; i++)
            {
                var has = i < view.Types.Count;
                panel.SetClassFor(player, $"sk_type{i}", "hidden", !has);
                if (!has) continue;
                var type = view.Types[i];
                panel.SetVariableFor(player, $"sk_type{i}_name", TypeLabel(view, type.WeaponClass));
                panel.SetVariableFor(player, $"sk_type{i}_n", view.Slot == "agent" ? $"{type.Skins} agents" : $"{type.Skins} skins");
                // The picture is the addon's (knife) or the game's own (gun) class; gloves and agents have none here.
                var icon = view.Slot == "knife" && KnifeIcons.TryGetValue(type.WeaponClass, out var knife) ? "kn-" + knife
                    : view.Slot == "weapon" && type.Model is not null ? "gn-" + type.Model : null;
                SetState(panel, player, $"sk_type{i}_ic", "kn", icon);
            }
            return;
        }
        panel.SetVariableFor(player, "sk_title", view.WeaponClass is null ? "" : TypeLabel(view, view.WeaponClass));
        panel.SetVariableFor(player, "sk_count", view.Slot == "agent" ? $"{view.Total} agents" : $"{view.Total} skins");
        var pages = Math.Max(1, (view.Total + SkinItemSlots - 1) / SkinItemSlots);
        panel.SetVariableFor(player, "sk_page", $"{view.Offset / SkinItemSlots + 1} / {pages}");
        panel.SetClassFor(player, "sk_prev", "disabled", view.Offset <= 0);
        panel.SetClassFor(player, "sk_next", "disabled", view.Offset + SkinItemSlots >= view.Total);
        var model = view.Slot == "weapon" ? view.Model : SkinModel(view.WeaponClass);
        for (var i = 0; i < SkinItemSlots; i++)
        {
            var has = i < view.Items.Count;
            panel.SetClassFor(player, $"sk_item{i}", "hidden", !has);
            panel.SetClassFor(player, $"sk_item{i}", "sel", false);
            if (!has) continue;
            var item = view.Items[i];
            panel.SetVariableFor(player, $"sk_item{i}_name", item.Name);
            // The picture is the one CS2 already has for that skin or agent; the addon's stylesheet maps this class to it (no class = name only).
            var pic = view.Slot == "agent" ? (item.Defindex is { } defindex ? $"ag-{defindex}" : null)
                // A knife without a paint is the plain one (class sp-<model>-0, the game's picture of the unpainted knife).
                : model is not null && (item.PaintId ?? (view.Slot == "knife" ? 0 : (int?)null)) is { } paintId ? $"sp-{model}-{paintId}" : null;
            SetState(panel, player, $"sk_item{i}_pic", "sp", pic);
        }
    }

    /// <summary>Clicks of the Skins tab. Returns true when the id was one of its buttons.</summary>
    private bool SkinsClick(PanelHandle panel, CCSPlayerController player, string id)
    {
        var view = SkinViewFor(player);
        switch (id)
        {
            case "sk_slot_knife": SkinsOpen(panel, player, "knife"); return true;
            case "sk_slot_glove": SkinsOpen(panel, player, "glove"); return true;
            case "sk_slot_weapon": SkinsOpen(panel, player, "weapon"); return true;
            case "sk_slot_agent": SkinsOpen(panel, player, "agent"); return true;
            case "sk_back":
                view.WeaponClass = null;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_prev":
                if (view.WeaponClass is not null && view.Offset > 0 && view.Types.FirstOrDefault(t => t.WeaponClass == view.WeaponClass) is { } back)
                    SkinsLoadItems(panel, player, view, back, view.Offset - SkinItemSlots);
                return true;
            case "sk_next":
                if (view.WeaponClass is not null && view.Offset + SkinItemSlots < view.Total && view.Types.FirstOrDefault(t => t.WeaponClass == view.WeaponClass) is { } forward)
                    SkinsLoadItems(panel, player, view, forward, view.Offset + SkinItemSlots);
                return true;
        }
        if (id.StartsWith("sk_grp_", StringComparison.Ordinal))
        {
            var group = GunGroups.FirstOrDefault(g => id == "sk_grp_" + g.ToLowerInvariant());
            if (group is not null) SkinsOpen(panel, player, "weapon", group);
            return true;
        }
        if (id.StartsWith("sk_type", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var type) && type >= 0 && type < view.Types.Count)
        {
            SkinsLoadItems(panel, player, view, view.Types[type], 0);
            return true;
        }
        if (id.StartsWith("sk_item", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var item))
        {
            SkinsEquip(panel, player, view, item);
            return true;
        }
        return false;
    }
}

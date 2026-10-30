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
    private const int SkinItemSlots = 300;         // every skin of a pick is in one list that scrolls (the most any pick has; the cards are in the menu layout)
    private const int SkinPage = 100;              // skins asked of the backend at a time (MENU_SKIN_PAGE there); the next page is asked for as soon as one arrives
    private const int TileSlots = 35;            // 4 columns of 8 guns, then Agent, Gloves, Knife
    private static readonly int[] ColumnStart = { 0, 8, 16, 24, 32 };
    private static readonly int[] ColumnSize = { 8, 8, 8, 8, 3 };

    // The category list on the left: four gun groups (the API's columns), then the agents, the gloves and the knives.
    private static readonly string[] CategoryNames = { "Pistols", "SMGs", "Rifles", "Heavy", "Agents", "Gloves", "Knives" };
    private const int CategoryAgents = 4, CategoryGloves = 5, CategoryKnives = 6;

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
        public int Cat;                                    // the category picked on the left
        public string Mode = "loadout";                    // loadout | types | items
        public Tile?[] Slots = new Tile?[TileSlots];       // the overview, by pool position
        public Tile? Picked;                               // the tile that was clicked
        public List<SkinType> Types = new();               // knife / glove types
        public string? TypeClass;                          // the picked knife / glove type
        public int LoadId;                                 // which asking of the list is current: an answer of an older one is dropped
        public int Total;
        public bool Loading;                               // the skins of the page were asked for and have not come yet: the count is unknown, not 0
        public List<SkinItem> Items = new();
        public int Selected = -1;
        public bool Busy;
        public bool EquipAgain;                            // a click came while one was being saved: save the latest pick after it
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

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

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

    // Answers that were already fetched: the catalog (types, skins of a gun) changes rarely, a player's loadout only when they pick.
    // A click that finds its answer here is drawn at once, without a round trip to the API.
    private readonly Dictionary<string, (DateTime Until, JsonElement Body)> skinCache = new();
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LoadoutTtl = TimeSpan.FromSeconds(45);

    private void ForgetLoadouts(ulong steamId)
    {
        foreach (var key in skinCache.Keys.Where(k => k.StartsWith($"lo:{steamId}:", StringComparison.Ordinal)).ToList()) skinCache.Remove(key);
    }

    /// <summary>
    /// Reads from the cache, or runs an API read away from the game thread, then hands the answer to <paramref name="done"/> on the
    /// game thread, if the player is still there.
    /// </summary>
    private void SkinsFetch(CCSPlayerController player, string path, string cacheKey, TimeSpan ttl, Action<CCSPlayerController, SkinView, int, JsonElement?> done)
    {
        var slot = player.Slot;
        if (skinCache.TryGetValue(cacheKey, out var hit) && hit.Until > DateTime.UtcNow && skinViews.TryGetValue(slot, out var cachedView))
        {
            done(player, cachedView, 200, hit.Body);
            return;
        }
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
                if (status == 200 && body is { } good) skinCache[cacheKey] = (DateTime.UtcNow + ttl, good);
                // Say why a page is empty: 0 = the API could not be reached, 404 = the API has no such route yet, 401 / 403 = the token.
                else if (status != 200) Console.WriteLine($"[{ModuleName}] Skins API {path.Split('?')[0]} answered {(status == 0 ? "nothing (not reachable)" : status.ToString())}");
                var p = Utilities.GetPlayerFromSlot(slot);
                if (p is not { IsValid: true } || menu is null || !skinViews.TryGetValue(slot, out var view)) return;
                done(p, view, status, body);
            });
        });
    }

    /// <summary>When the menu opens, fetch both teams' overviews in the background so the Skins tab and the team tabs draw at once.</summary>
    private void SkinsWarm(CCSPlayerController player)
    {
        if (!SkinsReady) return;
        var steamId = player.SteamID;
        foreach (var team in new[] { "t", "ct" })
        {
            var key = $"lo:{steamId}:{team}";
            if (skinCache.TryGetValue(key, out var hit) && hit.Until > DateTime.UtcNow) continue;
            _ = Task.Run(async () =>
            {
                try
                {
                    var (status, body) = await SkinsApiAsync(HttpMethod.Get, $"/api/v1/plugin/menu/skins/loadout?steam_id={steamId}&team={team}");
                    if (status == 200 && body is { } good) Server.NextFrame(() => skinCache[key] = (DateTime.UtcNow + LoadoutTtl, good));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{ModuleName}] Skins warm-up failed: {ex.Message}");
                }
            });
        }
    }

    private static bool SkinsFailed(int status, JsonElement? body, out JsonElement json, out string message)
    {
        json = default;
        message = "";
        if (status == 404) message = "Could not load skins. Try again.";
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

    private void SkinsStatus(PanelHandle panel, CCSPlayerController player, string text) => Sv(panel, player, "sk_status", text);

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
        SkinsFetch(player, $"/api/v1/plugin/menu/skins/loadout?steam_id={player.SteamID}&team={wantedTeam}", $"lo:{player.SteamID}:{wantedTeam}", LoadoutTtl, (p, current, status, body) =>
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
            SkinsEnterCategory(menu!, p, current);
        });
    }

    /// <summary>
    /// A category was picked (or the loadout arrived): the guns of a group are shown as cards, the agents go straight to their skins,
    /// and gloves and knives to their types first.
    /// </summary>
    private void SkinsEnterCategory(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        view.Selected = -1;
        view.Picked = null;
        view.TypeClass = null;
        view.Mode = "loadout";
        if (view.Cat < CategoryAgents)
        {
            SkinsRender(panel, player, view);
            return;
        }
        var slotName = view.Cat == CategoryAgents ? "agent" : view.Cat == CategoryGloves ? "glove" : "knife";
        var tile = view.Slots.FirstOrDefault(t => t?.Slot == slotName);
        if (tile is null)
        {
            SkinsStatus(panel, player, "Could not load skins. Try again.");
            SkinsRender(panel, player, view);
            return;
        }
        if (slotName == "agent") SkinsLoadItems(panel, player, view, tile, tile.WeaponClass, 0);
        else SkinsLoadTypes(panel, player, view, tile);
    }

    /// <summary>A knife or glove tile was clicked: list the types first.</summary>
    private void SkinsLoadTypes(PanelHandle panel, CCSPlayerController player, SkinView view, Tile tile)
    {
        view.Picked = tile;
        view.Mode = "types";
        view.Types = new();
        SkinsStatus(panel, player, "Loading ...");
        SkinsRender(panel, player, view);
        SkinsFetch(player, $"/api/v1/plugin/menu/skins/types?steam_id={player.SteamID}&slot={tile.Slot}", $"ty:{tile.Slot}", CatalogTtl, (p, current, status, body) =>
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

    /// <summary>The skins of a pick (a gun, the agents of a team, a knife or glove type): one list that scrolls, filled page after page.</summary>
    private void SkinsLoadItems(PanelHandle panel, CCSPlayerController player, SkinView view, Tile tile, string weaponClass, int offset = 0)
    {
        view.Picked = tile;
        view.Mode = "items";
        view.TypeClass = tile.Slot is "knife" or "glove" ? weaponClass : null;
        view.Selected = -1;
        view.Items = new();
        view.Total = 0;
        view.Loading = true;
        var load = ++view.LoadId;
        SkinsStatus(panel, player, "Loading ...");
        SkinsRender(panel, player, view);       // the list switches now; the skins arrive in a moment (at once from the cache)
        SkinsLoadItemPage(player, view, tile, weaponClass, load);
    }

    private void SkinsLoadItemPage(CCSPlayerController player, SkinView view, Tile tile, string weaponClass, int load)
    {
        var wanted = view.Items.Count;
        var path = $"/api/v1/plugin/menu/skins/items?steam_id={player.SteamID}&slot={tile.Slot}&weapon_class={Uri.EscapeDataString(weaponClass)}&offset={wanted}";
        SkinsFetch(player, path, $"it:{tile.Slot}:{weaponClass}:{wanted}", CatalogTtl, (p, current, status, body) =>
        {
            if (current.Picked != tile || current.Mode != "items" || current.LoadId != load) return;
            current.Loading = false;
            if (SkinsFailed(status, body, out var json, out var message))
            {
                SkinsStatus(menu!, p, message);
                SkinsRender(menu!, p, current);
                return;
            }
            current.Total = json.GetProperty("total").GetInt32();
            var page = json.GetProperty("items").EnumerateArray()
                .Select(i => new SkinItem(Text(i, "id") ?? "", Text(i, "name") ?? "", Number(i, "paintId"), Number(i, "defindex"), Text(i, "rarity"), Text(i, "rarityName") ?? ""))
                .Where(i => i.Id.Length > 0).ToList();
            current.Items.AddRange(page.Take(SkinItemSlots - current.Items.Count));
            SkinsStatus(menu!, p, "");
            SkinsRender(menu!, p, current);
            // The rest of the list follows without a click, so the player only scrolls.
            if (page.Count > 0 && current.Items.Count < current.Total && current.Items.Count < SkinItemSlots) SkinsLoadItemPage(p, current, tile, weaponClass, load);
        });
    }

    private void SkinsEquip(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        if (view.Picked is not { } tile) return;
        if (view.Selected < 0 || view.Selected >= view.Items.Count)
        {
            SkinsStatus(panel, player, "Pick a skin first.");
            return;
        }
        if (view.Busy)
        {
            view.EquipAgain = true;
            return;
        }
        view.Busy = true;
        var item = view.Items[view.Selected];
        // The pick shows at once (the check mark does not wait for the API); it is taken back below if saving fails.
        var optimistic = tile with { Eq = new Equipped(item.Id, view.TypeClass ?? tile.WeaponClass, item.Name, item.PaintId, item.Defindex, item.Rarity) };
        for (var i = 0; i < view.Slots.Length; i++)
            if (view.Slots[i] == tile) view.Slots[i] = optimistic;
        view.Picked = optimistic;
        SkinsRender(panel, player, view);
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
                else if (status == 404) message = "Could not load skins. Try again.";
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
                if (!saved && current is not null)
                {
                    for (var i = 0; i < current.Slots.Length; i++)
                        if (current.Slots[i] == optimistic) current.Slots[i] = tile;
                    if (current.Picked == optimistic) current.Picked = tile;
                    current.EquipAgain = false;
                    SkinsRender(menu, p, current);
                }
                SkinsStatus(menu, p, message);
                // The menu line is easy to miss, so the answer is also in the chat.
                p.PrintToChat(saved ? $" \u0004Equipped \u0001{label}\u0008." : $" \u0002{message}");
                if (!saved || current is null) return;
                // The overview shows the new pick at once (it is read again whenever the tab is opened).
                ForgetLoadouts(steamId);
                var weaponClass = current.TypeClass ?? tile.WeaponClass;
                var newTile = tile with { Eq = new Equipped(item.Id, weaponClass, item.Name, item.PaintId, item.Defindex, item.Rarity) };
                for (var i = 0; i < current.Slots.Length; i++)
                    if (current.Slots[i] == tile || current.Slots[i] == optimistic) current.Slots[i] = newTile;
                if (current.Picked == tile || current.Picked == optimistic) current.Picked = newTile;
                SkinsRender(menu, p, current);
                MenuSound(p, soundPick, LevelPick);
                // Same as typing !rs without its wait between uses: SkinBridge (css_lxapply) reads the loadout from the API and applies it.
                p.ExecuteClientCommandFromServer("css_lxapply");
                // A click came while this one was being saved: save the latest pick now.
                if (current.EquipAgain)
                {
                    current.EquipAgain = false;
                    if (current.Selected >= 0 && current.Selected < current.Items.Count && current.Items[current.Selected].Id != item.Id)
                        SkinsEquip(menu, p, current);
                }
            });
        });
    }

    /// <summary>Takes the equipped skin of the picked gun, knife, glove type or agent off (it falls back to the game's own).</summary>
    private void SkinsRemove(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        if (view.Picked is not { Eq: { } eq } tile || view.Busy) return;
        view.Busy = true;
        var team = view.Team;
        var steamId = player.SteamID;
        var playerSlot = player.Slot;
        SkinsStatus(panel, player, "Removing ...");
        _ = Task.Run(async () =>
        {
            var removed = false;
            string message;
            try
            {
                var json = JsonSerializer.Serialize(new { steam_id = steamId.ToString(), catalog_item_id = eq.ItemId, team });
                var (status, body) = await SkinsApiAsync(HttpMethod.Post, "/api/v1/plugin/menu/skins/unequip", json);
                removed = status == 200;
                message = removed ? "Skin removed." : status == 404 ? "Nothing to remove." : $"Could not remove (error {status}). Tell staff.";
                if (!removed) Console.WriteLine($"[{ModuleName}] Skin remove refused: HTTP {status} {body}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleName}] Skin remove failed: {ex.Message}");
                message = "Could not remove. Try again.";
            }
            Server.NextFrame(() =>
            {
                if (skinViews.TryGetValue(playerSlot, out var current)) current.Busy = false;
                var p = Utilities.GetPlayerFromSlot(playerSlot);
                if (p is not { IsValid: true } || menu is null || current is null) return;
                if (removed)
                {
                    ForgetLoadouts(steamId);
                    var bare = tile with { Eq = null };
                    for (var i = 0; i < current.Slots.Length; i++)
                        if (current.Slots[i] == tile) current.Slots[i] = bare;
                    if (current.Picked == tile) current.Picked = bare;
                    p.ExecuteClientCommandFromServer("css_lxapply");
                    MenuSound(p, soundPick, LevelPick);
                }
                SkinsRender(menu, p, current);
                SkinsStatus(menu, p, message);
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
        if (paintId is { } paint)
        {
            if (SkinPictureKeys.Contains($"{model}-{paint}")) return $"sp-{model}-{paint}";
            // A skin the game has no picture of shows as the plain gun, knife or glove, never an empty card.
            return slot switch { "weapon" => "gn-" + model, "knife" => "kn-" + model, "glove" => "gl-" + model, _ => null };
        }
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
        Sc(panel, player, "sk_team_t", "active", view.Team == "t");
        Sc(panel, player, "sk_team_ct", "active", view.Team == "ct");
        for (var i = 0; i < CategoryNames.Length; i++) Sc(panel, player, $"sk_cat{i}", "active", i == view.Cat);
        Sc(panel, player, "sk_view_loadout", "hidden", view.Mode != "loadout");
        Sc(panel, player, "sk_view_types", "hidden", view.Mode != "types");
        Sc(panel, player, "sk_view_items", "hidden", view.Mode != "items");
        Sc(panel, player, "sk_actions", "hidden", view.Mode != "items");
        if (view.Mode == "loadout") RenderLoadout(panel, player, view);
        else if (view.Mode == "types") RenderTypes(panel, player, view);
        else RenderItems(panel, player, view);
    }

    private void RenderLoadout(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        var first = view.Cat < CategoryAgents ? ColumnStart[view.Cat] : -1;
        var last = view.Cat < CategoryAgents ? first + ColumnSize[view.Cat] : -1;
        Sv(panel, player, "sk_title", CategoryNames[view.Cat]);
        Sv(panel, player, "sk_sub", view.Cat < CategoryAgents ? $"Equipped {view.Slots.Skip(first).Take(last - first).Count(t => t?.Eq is not null)}" : "");
        for (var i = 0; i < TileSlots; i++)
        {
            var tile = view.Slots[i];
            var shown = tile is not null && i >= first && i < last;
            Sc(panel, player, $"sk_w{i}", "hidden", !shown);
            if (!shown || tile is null) continue;
            Sv(panel, player, $"sk_w{i}_name", tile.Label);
            var eq = tile.Eq;
            Sc(panel, player, $"sk_w{i}_ck", "hidden", eq is null);
            Ss(panel, player, $"sk_w{i}_dot", "rc", eq?.Rarity is { } rarity ? "rc-" + rarity : null);
            string? pic;
            if (eq is not null)
                pic = SkinPicture(tile.Slot, tile.Slot == "weapon" ? tile.Model : SkinModel(eq.WeaponClass), eq.PaintId, eq.Defindex);
            else
                pic = tile.Slot == "weapon" && tile.Model is not null ? "gn-" + tile.Model : tile.Slot == "knife" ? "kn-karambit" : null;
            Ss(panel, player, $"sk_w{i}_pic", "sp", pic);
        }
    }

    private void RenderTypes(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        var tile = view.Picked;
        Sv(panel, player, "sk_title", tile?.Slot == "glove" ? "Gloves" : "Knives");
        Sv(panel, player, "sk_sub", tile?.Eq is { } eq ? $"Equipped: {eq.WeaponClass} | {eq.Skin}" : "Pick a type, then a skin.");
        for (var i = 0; i < SkinTypeSlots; i++)
        {
            var has = i < view.Types.Count && tile is not null;
            Sc(panel, player, $"sk_type{i}", "hidden", !has);
            if (!has) continue;
            var type = view.Types[i];
            Sv(panel, player, $"sk_type{i}_name", TypeLabel(tile!, type.WeaponClass));
            Sv(panel, player, $"sk_type{i}_n", $"{type.Skins} skins");
            // A knife type has its grey icon; a glove type shows one skin of that type (glove_<type>.png), like the website.
            Ss(panel, player, $"sk_type{i}_ic", "kn",
                tile!.Slot == "knife" && KnifeIcons.TryGetValue(type.WeaponClass, out var knife) ? "kn-" + knife
                : tile.Slot == "glove" && GloveModels.TryGetValue(type.WeaponClass, out var glove) ? "gl-" + glove
                : null);
        }
    }

    private void RenderItems(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        var tile = view.Picked;
        var agent = tile?.Slot == "agent";
        Sv(panel, player, "sk_title", PickedName(view));
        Sv(panel, player, "sk_sub", view.Loading ? "" : agent ? $"{view.Total} agents" : $"{view.Total} skins");   // no "0 skins" while the page is still on its way
        Sc(panel, player, "sk_back", "hidden", agent);
        Sc(panel, player, "sk_remove", "hidden", tile?.Eq is null);
        var model = tile is null ? null : tile.Slot == "weapon" ? tile.Model : SkinModel(view.TypeClass);
        var eq = tile?.Eq;
        for (var i = 0; i < SkinItemSlots; i++)
        {
            var has = i < view.Items.Count && tile is not null;
            Sc(panel, player, $"sk_item{i}", "hidden", !has);
            Sc(panel, player, $"sk_item{i}", "sel", has && i == view.Selected);
            if (!has) continue;
            var item = view.Items[i];
            Sv(panel, player, $"sk_item{i}_name", item.Name);
            Sv(panel, player, $"sk_item{i}_rar", item.RarityName);
            Ss(panel, player, $"sk_item{i}_ln", "rc", item.Rarity is { } rarity ? "rc-" + rarity : null);
            Ss(panel, player, $"sk_item{i}_pic", "sp", SkinPicture(tile!.Slot, model, item.PaintId, item.Defindex));
            // "Saved": the pick the player already has on this gun / type / agent.
            var saved = eq is not null && (agent ? eq.Defindex is not null && eq.Defindex == item.Defindex
                : eq.PaintId == item.PaintId && (view.TypeClass is null || eq.WeaponClass == view.TypeClass));
            Sc(panel, player, $"sk_item{i}_saved", "hidden", !saved);
        }
        var chosen = view.Selected >= 0 && view.Selected < view.Items.Count ? view.Items[view.Selected] : null;
        Sv(panel, player, "sk_pv_name", chosen is null ? "Pick a skin" : agent ? chosen.Name : $"{(view.TypeClass is { } type ? TypeLabel(tile!, type) : tile?.Label)} | {chosen.Name}");
        Sv(panel, player, "sk_pv_rar", chosen?.RarityName ?? "");
        Ss(panel, player, "sk_pv_pic", "sp", chosen is null || tile is null ? null : SkinPicture(tile.Slot, model, chosen.PaintId, chosen.Defindex));
    }

    /// <summary>Clicks of the Skins tab. Returns true when the id was one of its buttons.</summary>
    private bool SkinsClick(PanelHandle panel, CCSPlayerController player, string id)
    {
        var view = SkinViewFor(player);
        switch (id)
        {
            case "sk_team_t": SkinsOpen(panel, player, "t"); return true;
            case "sk_team_ct": SkinsOpen(panel, player, "ct"); return true;
            case "sk_back":
                view.Selected = -1;
                view.Mode = view.TypeClass is not null ? "types" : "loadout";
                if (view.Mode == "loadout") view.Picked = null;
                view.TypeClass = null;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_remove":
                SkinsRemove(panel, player, view);
                return true;
        }
        if (id.StartsWith("sk_cat", StringComparison.Ordinal) && int.TryParse(id.AsSpan(6), out var cat) && cat >= 0 && cat < CategoryNames.Length)
        {
            view.Cat = cat;
            SkinsStatus(panel, player, "");
            // The overview of this team is already here once it has loaded; otherwise load it (it enters the category on arrival).
            if (view.Slots.Any(t => t is not null)) SkinsEnterCategory(panel, player, view);
            else SkinsOpen(panel, player);
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
            // A click on a skin picks it and equips it at once (the Equip button still does the same for the picked one).
            view.Selected = item;
            SkinsStatus(panel, player, "");
            SkinsRender(panel, player, view);
            SkinsEquip(panel, player, view);
            return true;
        }
        return false;
    }

    /// <summary>The class the current skin list was asked for: the knife / glove type, the gun, or the team of the agents.</summary>
    private static string PickedClass(SkinView view) => view.TypeClass ?? view.Picked?.WeaponClass ?? "";
}

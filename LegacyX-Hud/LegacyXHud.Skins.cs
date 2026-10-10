using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using PanoramaManager;

namespace LegacyXHud;

// The Skins tab of the menu: knives and gloves. The player picks a type (Karambit, Sport Gloves …), then a skin;
// the pick is saved to the website loadout through the plugin API and applied at once through SkinBridge's !rs.
// Everything shown comes from the API (the same catalog the website uses); nothing is listed here by hand.
// Reading and saving use SkinBridge's credentials (LEGACYX_SKINBRIDGE_*), which already carry skinchanger:read and :write.
public sealed partial class LegacyXHud
{
    private const int SkinTypeSlots = 24;
    private const int SkinItemSlots = 12;

    private string skinPluginId = "legacyx-skinbridge";
    private string skinSecret = "";
    private bool SkinsReady => apiBase.Length > 0 && skinSecret.Length > 0;

    private sealed record SkinType(string WeaponClass, int Skins);
    private sealed record SkinItem(string Id, string Name);

    private sealed class SkinView
    {
        public string Slot = "knife";
        public List<SkinType> Types = new();
        public string? WeaponClass;
        public int Offset;
        public int Total;
        public List<SkinItem> Items = new();
        public string Equipped = "";
        public bool Busy;
    }

    private readonly Dictionary<int, SkinView> skinViews = new();

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

    // ---- opening and navigation -------------------------------------------------------------------------

    private SkinView SkinViewFor(CCSPlayerController player)
    {
        if (!skinViews.TryGetValue(player.Slot, out var view)) skinViews[player.Slot] = view = new SkinView();
        return view;
    }

    private void SkinsStatus(PanelHandle panel, CCSPlayerController player, string text) => panel.SetVariableFor(player, "sk_status", text);

    /// <summary>The Skins tab was opened, or a slot (Knives / Gloves) was picked: list the types.</summary>
    private void SkinsOpen(PanelHandle panel, CCSPlayerController player, string? slot = null)
    {
        var view = SkinViewFor(player);
        if (slot is not null && slot != view.Slot)
        {
            view.Slot = slot;
            view.Types = new();
            view.WeaponClass = null;
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
        var wanted = view.Slot;
        _ = Task.Run(async () =>
        {
            try
            {
                var (status, body) = await SkinsApiAsync(HttpMethod.Get, $"/api/v1/plugin/menu/skins/types?steam_id={steamId}&slot={wanted}");
                Server.NextFrame(() =>
                {
                    var p = Utilities.GetPlayerFromSlot(playerSlot);
                    if (p is not { IsValid: true } || menu is null || !skinViews.TryGetValue(playerSlot, out var current) || current.Slot != wanted) return;
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
                        .Select(t => new SkinType(t.GetProperty("weaponClass").GetString() ?? "", t.GetProperty("skins").GetInt32()))
                        .Where(t => t.WeaponClass.Length > 0).Take(SkinTypeSlots).ToList();
                    current.Equipped = json.TryGetProperty("equipped", out var eq) && eq.ValueKind == JsonValueKind.Object
                        ? $"{eq.GetProperty("weaponClass").GetString()} | {eq.GetProperty("skin").GetString()}" : "";
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

    private void SkinsLoadItems(PanelHandle panel, CCSPlayerController player, SkinView view, string weaponClass, int offset)
    {
        view.WeaponClass = weaponClass;
        view.Offset = Math.Max(0, offset);
        SkinsStatus(panel, player, "Loading ...");
        var steamId = player.SteamID;
        var playerSlot = player.Slot;
        var slot = view.Slot;
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
                        .Select(i => new SkinItem(i.GetProperty("id").GetString() ?? "", i.GetProperty("name").GetString() ?? ""))
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
            var saved = false;
            try
            {
                var json = JsonSerializer.Serialize(new { steam_id = steamId.ToString(), catalog_item_id = item.Id });
                var (status, body) = await SkinsApiAsync(HttpMethod.Post, "/api/v1/plugin/menu/skins/equip", json);
                if (status == 200 && body is { } ok)
                {
                    saved = true;
                    message = $"Equipped {ok.GetProperty("weaponClass").GetString()} | {ok.GetProperty("skin").GetString()}.";
                }
                else if (status == 404) message = "Sign in on legacyx.cc with this Steam account to pick skins.";
                else if (status == 409) message = "The server does not see you yet. Try again in a few seconds.";
                else message = "Could not save. Try again.";
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
                if (current is not null) current.Equipped = $"{current.WeaponClass} | {item.Name}";
                menu.SetClassFor(p, $"sk_item{index}", "sel", true);
                // Same as typing !rs: SkinBridge reads the loadout from the API and applies it.
                p.ExecuteClientCommandFromServer("css_rs");
            });
        });
    }

    // ---- drawing ----------------------------------------------------------------------------------------

    private void SkinsRender(PanelHandle panel, CCSPlayerController player, SkinView view)
    {
        panel.SetClassFor(player, "sk_slot_knife", "active", view.Slot == "knife");
        panel.SetClassFor(player, "sk_slot_glove", "active", view.Slot == "glove");
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
                panel.SetVariableFor(player, $"sk_type{i}_name", view.Types[i].WeaponClass);
                panel.SetVariableFor(player, $"sk_type{i}_n", view.Types[i].Skins.ToString());
            }
            return;
        }
        panel.SetVariableFor(player, "sk_title", view.WeaponClass ?? "");
        panel.SetVariableFor(player, "sk_count", $"{view.Total} skins");
        var pages = Math.Max(1, (view.Total + SkinItemSlots - 1) / SkinItemSlots);
        panel.SetVariableFor(player, "sk_page", $"{view.Offset / SkinItemSlots + 1} / {pages}");
        panel.SetClassFor(player, "sk_prev", "disabled", view.Offset <= 0);
        panel.SetClassFor(player, "sk_next", "disabled", view.Offset + SkinItemSlots >= view.Total);
        for (var i = 0; i < SkinItemSlots; i++)
        {
            var has = i < view.Items.Count;
            panel.SetClassFor(player, $"sk_item{i}", "hidden", !has);
            panel.SetClassFor(player, $"sk_item{i}", "sel", false);
            if (has) panel.SetVariableFor(player, $"sk_item{i}_name", view.Items[i].Name);
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
            case "sk_back":
                view.WeaponClass = null;
                SkinsStatus(panel, player, "");
                SkinsRender(panel, player, view);
                return true;
            case "sk_prev":
                if (view.WeaponClass is not null && view.Offset > 0) SkinsLoadItems(panel, player, view, view.WeaponClass, view.Offset - SkinItemSlots);
                return true;
            case "sk_next":
                if (view.WeaponClass is not null && view.Offset + SkinItemSlots < view.Total) SkinsLoadItems(panel, player, view, view.WeaponClass, view.Offset + SkinItemSlots);
                return true;
        }
        if (id.StartsWith("sk_type", StringComparison.Ordinal) && int.TryParse(id.AsSpan(7), out var type) && type >= 0 && type < view.Types.Count)
        {
            SkinsLoadItems(panel, player, view, view.Types[type].WeaponClass, 0);
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

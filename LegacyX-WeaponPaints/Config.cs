using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace WeaponPaints
{
	public class Additional
	{
		[JsonPropertyName("KnifeEnabled")]
		public bool KnifeEnabled { get; set; } = true;

		[JsonPropertyName("GloveEnabled")]
		public bool GloveEnabled { get; set; } = true;

		[JsonPropertyName("MusicEnabled")]
		public bool MusicEnabled { get; set; } = true;

		[JsonPropertyName("AgentEnabled")]
		public bool AgentEnabled { get; set; } = true;

		[JsonPropertyName("SkinEnabled")]
		public bool SkinEnabled { get; set; } = true;

		[JsonPropertyName("PinsEnabled")]
		public bool PinsEnabled { get; set; } = true;

		[JsonPropertyName("CommandWpEnabled")]
		public bool CommandWpEnabled { get; set; } = false;

		[JsonPropertyName("CommandKillEnabled")]
		public bool CommandKillEnabled { get; set; } = true;

		[JsonPropertyName("CommandKnife")]
		public List<string> CommandKnife { get; set; } = [];

		[JsonPropertyName("CommandMusic")]
		public List<string> CommandMusic { get; set; } = [];
		
		[JsonPropertyName("CommandPin")]
		public List<string> CommandPin { get; set; } = [];

		[JsonPropertyName("CommandGlove")]
		public List<string> CommandGlove { get; set; } = [];

		[JsonPropertyName("CommandAgent")]
		public List<string> CommandAgent { get; set; } = [];
		
		[JsonPropertyName("CommandStattrak")]
		public List<string> CommandStattrak { get; set; } = [];

		[JsonPropertyName("CommandSkin")]
		public List<string> CommandSkin { get; set; } = [];

		[JsonPropertyName("CommandSkinSelection")]
		public List<string> CommandSkinSelection { get; set; } = [];

		[JsonPropertyName("CommandRefresh")]
		public List<string> CommandRefresh { get; set; } = [];

		[JsonPropertyName("CommandKill")]
		public List<string> CommandKill { get; set; } = [];

		[JsonPropertyName("GiveRandomKnife")]
		public bool GiveRandomKnife { get; set; } = false;

		[JsonPropertyName("GiveRandomSkin")]
		public bool GiveRandomSkin { get; set; } = false;

		[JsonPropertyName("ShowSkinImage")]
		public bool ShowSkinImage { get; set; } = true;
	}

	public class WeaponPaintsConfig : BasePluginConfig
	{
        [JsonPropertyName("ConfigVersion")] public override int Version { get; set; } = 10;

        [JsonPropertyName("SkinsLanguage")]
		public string SkinsLanguage { get; set; } = "en";

		[JsonPropertyName("ApiBaseUrl")]
		public string ApiBaseUrl { get; set; } = "";

		[JsonPropertyName("PluginId")]
		public string PluginId { get; set; } = "legacyx-skinbridge";

		[JsonPropertyName("PluginSecret")]
		public string PluginSecret { get; set; } = "";

		[JsonPropertyName("ServerId")]
		public string ServerId { get; set; } = "";

		[JsonPropertyName("ApiPollSeconds")]
		public int ApiPollSeconds { get; set; } = 3;

		[JsonPropertyName("EnableInGameMenus")]
		public bool EnableInGameMenus { get; set; } = false;

		[JsonPropertyName("Website")]
		public string Website { get; set; } = "https://legacyx.cc/skinchanger";

		[JsonPropertyName("CmdRefreshCooldownSeconds")]
		public int CmdRefreshCooldownSeconds { get; set; } = 3;

		[JsonPropertyName("Additional")]
		public Additional Additional { get; set; } = new();
		
		[JsonPropertyName("MenuType")]
		public string MenuType { get; set; } = "selectable";
	}
}

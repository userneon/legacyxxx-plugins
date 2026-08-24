using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using LegacyX.Shared.Configuration;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

[MinimumApiVersion(338)]
	public partial class WeaponPaints : BasePlugin, IPluginConfig<WeaponPaintsConfig>
	{
		private CounterStrikeSharp.API.Modules.Timers.Timer? _apiPollTimer;
		private bool _centralEnabled = true;
	internal static WeaponPaints Instance { get; private set; } = new();

	public WeaponPaintsConfig Config { get; set; } = new();
    private static WeaponPaintsConfig _config { get; set; } = new();
		public override string ModuleAuthor => "Nereziel & daffyy; LEGACY-X fork";
		public override string ModuleDescription => "LEGACY-X website-controlled CS2 cosmetics bridge";
		public override string ModuleName => "LEGACY-X SkinBridge";
		public override string ModuleVersion => "3.3a-legacyx.1";

		public override void Load(bool hotReload)
		{
			if (!_centralEnabled)
			{
				Logger.LogInformation("SkinBridge is disabled by central environment.");
				return;
			}
			// Hardcoded hotfix needs to be changed later (Not needed 17.09.2025)
		//if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		//	Patch.PerformPatch("0F 85 ? ? ? ? 31 C0 B9 ? ? ? ? BA ? ? ? ? 66 0F EF C0 31 F6 31 FF 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 0F 29 45 ? 48 C7 45 ? ? ? ? ? C7 45 ? ? ? ? ? 66 89 45 ? E8 ? ? ? ? 41 89 C5 85 C0 0F 8E", "90 90 90 90 90 90");
		//else
		//	Patch.PerformPatch("74 ? 48 8D 0D ? ? ? ? FF 15 ? ? ? ? EB ? BA", "EB");
		
		Instance = this;

		if (hotReload)
		{
			OnMapStart(string.Empty);
			
			GPlayerWeaponsInfo.Clear();
			GPlayersKnife.Clear();
			GPlayersGlove.Clear();
			GPlayersAgent.Clear();
			GPlayersPin.Clear();
			GPlayersMusic.Clear();

			foreach (var player in Enumerable
				         .OfType<CCSPlayerController>(Utilities.GetPlayers().TakeWhile(_ => WeaponSync != null))
				         .Where(player => player.IsValid &&
					         !string.IsNullOrEmpty(player.IpAddress) && player is
						         { IsBot: false, Connected: PlayerConnectedState.Connected }))
			{
				var playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player?.SteamID.ToString(),
					Name = player?.PlayerName,
					IpAddress = player?.IpAddress?.Split(":")[0]
				};

				_ = Task.Run(async () =>
				{
					if (WeaponSync != null) await WeaponSync.GetPlayerData(playerInfo);
				});
			}
		}

		Utility.LoadSkinsFromFile(ModuleDirectory + $"/data/skins_{_config.SkinsLanguage}.json", Logger);
		Utility.LoadGlovesFromFile(ModuleDirectory + $"/data/gloves_{_config.SkinsLanguage}.json", Logger);
		Utility.LoadAgentsFromFile(ModuleDirectory + $"/data/agents_{_config.SkinsLanguage}.json", Logger);
		Utility.LoadMusicFromFile(ModuleDirectory + $"/data/music_{_config.SkinsLanguage}.json", Logger);
		Utility.LoadPinsFromFile(ModuleDirectory + $"/data/collectibles_{_config.SkinsLanguage}.json", Logger);

			RegisterListeners();
			_apiPollTimer = AddTimer(Config.ApiPollSeconds, () => _ = WeaponSync?.PollAndApplyAsync(), TimerFlags.REPEAT);
		}

		public override void Unload(bool hotReload)
		{
			_apiPollTimer?.Kill();
			_apiPollTimer = null;
		}

		public void OnConfigParsed(WeaponPaintsConfig config)
		{
			var environment = LegacyXEnvironmentLoader.Load();
			_centralEnabled = environment.GetModuleBoolean("SKINBRIDGE", "ENABLED", true);
			config.ApiBaseUrl = environment.Get("LEGACYX_API_BASE_URL", config.ApiBaseUrl);
			config.PluginId = environment.GetModule("SKINBRIDGE", "PLUGIN_ID", config.PluginId);
			config.PluginSecret = environment.GetModule("SKINBRIDGE", "PLUGIN_TOKEN", config.PluginSecret);
			config.ServerId = environment.Get("LEGACYX_SERVER_ID", config.ServerId);
			config.ApiPollSeconds = environment.GetModuleInt("SKINBRIDGE", "POLL_SECONDS", config.ApiPollSeconds, 1, 30);
			Config = config;
		_config = config;

			config.ApiBaseUrl = config.ApiBaseUrl.TrimEnd('/');
			config.PluginId = config.PluginId.Trim();
			config.ServerId = config.ServerId.Trim();
			config.ApiPollSeconds = Math.Clamp(config.ApiPollSeconds, 1, 30);
			if (config.ApiBaseUrl.Length < 1 || config.PluginSecret.Length < 24 || config.ServerId.Length < 1)
			{
				Logger.LogError("ApiBaseUrl, PluginSecret, and ServerId are required. Database credentials are intentionally unsupported in LEGACY-X SkinBridge.");
				Unload(false);
				return;
		}

		if (!File.Exists(Path.GetDirectoryName(Path.GetDirectoryName(ModuleDirectory)) + "/gamedata/weaponpaints.json"))
		{
			Logger.LogError("You need to upload \"weaponpaints.json\" to \"gamedata directory\"!");
			Unload(false);
			return;
		}
		
			WeaponSync = new WeaponSynchronization(new LegacyXSkinApiClient(config), config);
			_localizer = Localizer;

		Utility.Config = config;
		Utility.ShowAd(ModuleVersion);
		Task.Run(async () => await Utility.CheckVersion(ModuleVersion, Logger));
	}

		public override void OnAllPluginsLoaded(bool hotReload)
		{
			if (!_centralEnabled) return;
			try
		{
				if (!Config.EnableInGameMenus) return;
				MenuApi = MenuCapability.Get();
				if (Config.Additional.KnifeEnabled) SetupKnifeMenu();
				if (Config.Additional.SkinEnabled) SetupSkinsMenu();
				if (Config.Additional.GloveEnabled) SetupGlovesMenu();
				if (Config.Additional.AgentEnabled) SetupAgentsMenu();
				if (Config.Additional.MusicEnabled) SetupMusicMenu();
				if (Config.Additional.PinsEnabled) SetupPinsMenu();
				RegisterCommands();
		}
		catch (Exception)
		{
			MenuApi = null;
			Logger.LogError("Error while loading required plugins");
			throw;
		}
	}
}

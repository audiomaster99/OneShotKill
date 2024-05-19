namespace OneHitKill
{
    using CounterStrikeSharp.API;
    using CounterStrikeSharp.API.Core;
    using CounterStrikeSharp.API.Modules.Admin;
    using CounterStrikeSharp.API.Modules.Cvars;
    using Microsoft.Extensions.Logging;
    using System.Text.Json.Serialization;

    public sealed class PluginConfig : BasePluginConfig
    {
        [JsonPropertyName("victim-spawn-immunity-time")]
        public int DelaySeconds { get; set; } = 10;

        [JsonPropertyName("allowed-weapons")]
        public List<string> WeaponsList { get; set; } = new List<string>
        {
            "weapon_awp",
            "weapon_ak47",
            "leave empty for all weapons"
        };

        [JsonPropertyName("allowed-flags")]
        public List<string> AdminFlags { get; set; } = new List<string>
        {
            "@css/admin",
            "#css/admin",
            "leave empty to allow for everyone"
        };

        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = 1;
    }

    public sealed partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
    {

        public override string ModuleName => "OneShotKill";
        public override string ModuleAuthor => "audio_brutalci";
        public override string ModuleDescription => "https://github.com/audiomaster99/OneShotKill";
        public override string ModuleVersion => "0.0.1";


        public required PluginConfig Config { get; set; } = new PluginConfig();
        public static readonly bool[] isDelayActive = new bool[64];
        public int FreezeTime;

        public void OnConfigParsed(PluginConfig config)
        {
            if (config.Version < Config.Version)
            {
                base.Logger.LogWarning("Plugin configuration is outdated. Please consider updating the configuration file. [Expected: {0} | Current: {1}]", this.Config.Version, config.Version);
            }

            this.Config = config;
        }

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterListener<Listeners.OnMapStart>(name =>
            {
                AddTimer(1.0F, () => { FreezeTime = ConVar.Find("mp_freezetime")!.GetPrimitiveValue<int>(); });
            });
        }

        public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (player is null || !player.IsValid)
                return HookResult.Continue;

            if (isDelayActive[player.Index] == true)
                return HookResult.Continue;

            CCSPlayerController? attacker = @event.Attacker;

            if (attacker is null || !attacker.IsValid || attacker.Team == player.Team || !ClientHasPermissions(attacker) && !(@event.DmgHealth > 0 || @event.DmgArmor > 0))
                return HookResult.Continue;

            var playerPawn = player.PlayerPawn;
            var attackerPawn = attacker?.PlayerPawn;

            if (playerPawn is null || playerPawn.Value is null || attackerPawn is null || attackerPawn.Value is null)
                return HookResult.Continue;

            var designerName = attackerPawn.Value.WeaponServices?.ActiveWeapon.Value?.DesignerName;

            if (designerName is null)
                return HookResult.Continue;

            if (Config.WeaponsList.Count == 0)
            {
                playerPawn.Value.Health -= 100;
            }
            else
            {
                foreach (string weapon in Config.WeaponsList)
                {
                    if (designerName.Contains(weapon))
                        playerPawn.Value.Health -= 100;
                    break;
                }
            }
            return HookResult.Continue;
        }

        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (player is not null)
            {
                HandlePlayerDelay(player);
            }

            return HookResult.Continue;
        }

        public void HandlePlayerDelay(CCSPlayerController player)
        {
            isDelayActive[player.Index] = true;
            AddTimer(Config.DelaySeconds + FreezeTime, () => { isDelayActive[player.Index] = false; });
        }

        public bool ClientHasPermissions(CCSPlayerController player)
        {
            if (Config.AdminFlags.Count == 0)
                return true;

            bool isAdmin = false;

            foreach (string adminFlags in Config.AdminFlags)
            {
                switch (adminFlags[0])
                {
                    case '@':
                        if (AdminManager.PlayerHasPermissions(player, adminFlags))
                            isAdmin = true;
                        break;
                    case '#':
                        if (AdminManager.PlayerInGroup(player, adminFlags))
                            isAdmin = true;
                        break;
                    default:
                        if (AdminManager.PlayerHasCommandOverride(player, adminFlags))
                            isAdmin = true;
                        break;
                }
            }
            return isAdmin;
        }
    }
}


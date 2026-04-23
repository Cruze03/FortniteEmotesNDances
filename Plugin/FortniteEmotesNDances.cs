using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;
using FortniteEmotes.API;

namespace FortniteEmotes;

[MinimumApiVersion(361)]
public partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Fortnite Emotes & Dances";
    public override string ModuleDescription => "CS2 Port of Fortnite Emotes & Dances";
    public override string ModuleAuthor => "Cruze (https://github.com/cruze03)";
    public override string ModuleVersion => "1.1.7-fix";

    public required PluginConfig Config { get; set; } = new();

    private List<string> g_CancelButtons = new();

    private Dictionary<ulong, PlayerSettings> g_PlayerSettings = new();

    private Dictionary<Emote, List<string>> g_ChatTriggers = new();

    private List<string> g_ListChatTriggers = new();

    private Dictionary<string, string> g_EmoteTransMap = new();

    private bool g_bRoundEnd = false;

    private CCSGameRules? g_GameRules = null;

    private Dictionary<int, Dictionary<string, List<(int, int)>>> playerWeapons = [];

    private Dictionary<int, Dictionary<int, ushort>> playerItems = [];

    public FakeConVar<bool> EmotesEnable = new("css_fortnite_emotes_enable", "ConVar to toggle emotes and dances", true);

    private static CRayTrace? g_RayTraceApi;

    public void OnConfigParsed(PluginConfig config)
    {
        if (config.Version < Config.Version)
        {
            Logger.LogWarning("Configuration version mismatch (Expected: {0} | Current: {1})", Config.Version, config.Version);
        }

        Config = config;

        g_EmoteTransMap = new();
        g_ChatTriggers = new();
        g_ListChatTriggers = new();
        g_CancelButtons = new();

        foreach (var emote in Config.EmoteDances)
        {
            if (!g_ChatTriggers.ContainsKey(emote))
                g_ChatTriggers.Add(emote, emote.Trigger);
            foreach (var trigger in emote.Trigger)
            {
                if (!g_ListChatTriggers.Contains(trigger))
                {
                    g_ListChatTriggers.Add(trigger);
                }
            }
            if (!g_EmoteTransMap.ContainsKey(emote.Name))
                g_EmoteTransMap.Add(emote.Name, Localizer[emote.Name]);
        }

        foreach (var emoteCommand in Config.EmoteCommand)
        {
            RegisterCommand(emoteCommand, "List all the available emotes", (CCSPlayerController? player, CommandInfo command) =>
            {
                if (player == null)
                    return;

                bool bAccess = HasPermision(player, Config.EmoteDanceCommandPerm);

                if (!bAccess)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.command.no-permission")}");
                    return;
                }

                if (command.ArgCount == 1)
                {
                    switch (Config.EmoteMenuType)
                    {
                        case 0:
                            ShowChatMenu(player, false);
                            break;
                        case 1:
                            ShowCenterMenu(player, false);
                            break;
                        default:
                            ShowKitsuneMenu(player, false);
                            break;
                    }
                    return;
                }

                string emote = command.GetArg(1);

                var emoteObj = GetEmoteByName(emote, true, true);

                if (emoteObj == null)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.not-found")}");
                    return;
                }
                string error = "";
                if (!PlayEmote(player, emoteObj, ref error))
                {
                    player.PrintToChat(error);
                }
            }, CommandUsage.CLIENT_ONLY);
        }

        foreach (var danceCommand in Config.DanceCommand)
        {
            RegisterCommand(danceCommand, "List all the available dances", (CCSPlayerController? player, CommandInfo command) =>
            {
                if (player == null)
                    return;

                bool bAccess = HasPermision(player, Config.EmoteDanceCommandPerm);

                if (!bAccess)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.command.no-permission")}");
                    return;
                }

                if (command.ArgCount == 1)
                {
                    switch (Config.EmoteMenuType)
                    {
                        case 0:
                            ShowChatMenu(player, true);
                            break;
                        case 1:
                            ShowCenterMenu(player, true);
                            break;
                        default:
                            ShowKitsuneMenu(player, true);
                            break;
                    }
                    return;
                }

                string dance = command.GetArg(1);

                var danceObj = GetEmoteByName(dance, false, true);

                if (danceObj == null)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.not-found")}");
                    return;
                }

                string error = "";
                if (!PlayEmote(player, danceObj, ref error))
                {
                    if (!string.IsNullOrEmpty(error))
                        player.PrintToChat(error);
                }
            }, CommandUsage.CLIENT_ONLY);
        }

        foreach (var setemotecommand in Config.AdminSetEmoteCommand)
        {
            RegisterCommand(setemotecommand, "Set emote to player", (CCSPlayerController? player, CommandInfo command) =>
            {
                if (player != null)
                {
                    bool bAccess = HasPermision(player, Config.AdminSetEmoteDanceCommandPerm);
                    if (!bAccess)
                    {
                        player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.command.no-permission")}");
                        return;
                    }
                }

                if (command.ArgCount != 3)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.setemote-usage", setemotecommand)}");
                    return;
                }

                string dance = command.GetArg(2);

                var emoteObj = GetEmoteByName(dance, true, true);

                if (emoteObj == null)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.not-found")}");
                    return;
                }

                var targets = GetTarget(command);
                if (targets == null)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.target-not-found")}");
                    return;
                }

                int count = 0;
                var playersToTarget = targets.Players.Where(player =>
                player.IsValidPlayer() && player.PlayerPawn.IsValidPawnAlive()).ToList();

                playersToTarget.ForEach(target =>
                {
                    if (CanTarget(player, target))
                    {
                        string error = "";
                        if (PlayEmote(target, emoteObj, ref error, player))
                            count++;
                    }
                });
                if (count > 0)
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.played-emote-on-target", count)}");
                else
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.failplayed-emote-on-target")}");
            }, CommandUsage.CLIENT_AND_SERVER);
        }

        foreach (var setdancecommand in Config.AdminSetDanceCommand)
        {
            RegisterCommand(setdancecommand, "Set dance to player", (CCSPlayerController? player, CommandInfo command) =>
            {
                if (player != null)
                {
                    bool bAccess = HasPermision(player, Config.AdminSetEmoteDanceCommandPerm);
                    if (!bAccess)
                    {
                        player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.command.no-permission")}");
                        return;
                    }
                }

                if (command.ArgCount != 3)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.setdance-usage", setdancecommand)}");
                    return;
                }

                string dance = command.GetArg(2);

                var danceObj = GetEmoteByName(dance, false, true);

                if (danceObj == null)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.not-found")}");
                    return;
                }

                var targets = GetTarget(command);
                if (targets == null)
                {
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.target-not-found")}");
                    return;
                }

                int count = 0;
                var playersToTarget = targets.Players.Where(player =>
                player.IsValidPlayer() && player.PlayerPawn.IsValidPawnAlive()).ToList();

                playersToTarget.ForEach(target =>
                {
                    if (CanTarget(player, target))
                    {
                        string error = "";
                        if (PlayEmote(target, danceObj, ref error, player))
                            count++;
                    }
                });
                if (count > 0)
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.played-dance-on-target", count)}");
                else
                    command.ReplyToCommand($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, $"emote.failplayed-dance-on-target")}");
            }, CommandUsage.CLIENT_AND_SERVER);
        }

        var buttons = Config.EmoteCancelButtons.Split(',');

        foreach (var button in buttons)
        {
            if (button.Equals("w", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Forward.ToString());
            else if (button.Equals("s", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Back.ToString());
            else if (button.Equals("a", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Moveleft.ToString());
            else if (button.Equals("d", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Moveright.ToString());
            else if (button.Equals("use", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Use.ToString());
            else if (button.Equals("speed", StringComparison.CurrentCultureIgnoreCase))
            {
                g_CancelButtons.Add(PlayerButtons.Speed.ToString());
                g_CancelButtons.Add(PlayerButtons.Walk.ToString());
            }
            else if (button.Equals("jump", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Jump.ToString());
            else if (button.Equals("leftclick", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Attack.ToString());
            else if (button.Equals("crouch", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Duck.ToString());
            else if (button.Equals("scoreboard", StringComparison.CurrentCultureIgnoreCase) && Config.EmoteMenuType != 2)
                g_CancelButtons.Add(PlayerButtons.Scoreboard.ToString());
            else if (button.Equals("inspect", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Inspect.ToString());
        }

        if (Config.StopDamageWhenInEmote && IsCS2FixesInstalled())
        {
            Config.StopDamageWhenInEmote = false;
            Logger.LogWarning("CS2Fixes DETECTED. Setting value of 'StopDamageWhenInEmote' as false.");
        }

        string error = "";

        if (!AreAllDependaciesInstalled(ref error))
        {
            Logger.LogError($"Plugin failed to load because {error}");
            Server.ExecuteCommand($"css_plugins unload {Path.GetFileNameWithoutExtension(ModulePath)}");
        }
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterListener<Listeners.OnMetamodAllPluginsLoaded>(OnMetamodAllPluginsLoaded);

        Transmit_OnLoad();

        // AddCommandListener("say", OnSay, HookMode.Pre);
        // AddCommandListener("say_team", OnSay, HookMode.Pre);
        HookUserMessage(118, OnMessage, HookMode.Pre);

        if (Config.StopDamageWhenInEmote)
            RegisterListener<Listeners.OnEntityTakeDamagePre>(OnTakeDamage);

        Menu_OnLoad();
        API_OnLoad();

        EmotesEnable.ValueChanged += (sender, value) =>
        {
            StopAllEmotes();
        };

        if (hotReload)
        {
            OnMapStart(Server.MapName);
            OnMetamodAllPluginsLoaded();
        }
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);

        Transmit_OnUnload();

        StopAllEmotes();

        RemoveAllCommands();

        RemoveListener<Listeners.OnMapStart>(OnMapStart);
        RemoveListener<Listeners.OnTick>(OnTick);
        RemoveListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RemoveListener<Listeners.OnMetamodAllPluginsLoaded>(OnMetamodAllPluginsLoaded);

        // RemoveCommandListener("say", OnSay, HookMode.Pre);
        // RemoveCommandListener("say_team", OnSay, HookMode.Pre);

        UnhookUserMessage(118, OnMessage, HookMode.Pre);

        if (Config.StopDamageWhenInEmote)
            RemoveListener<Listeners.OnEntityTakeDamagePre>(OnTakeDamage);
    }

    private void OnMetamodAllPluginsLoaded()
    {
        if (!RayTraceBridge.Initialize())
        {
            Logger.LogError("RayTrace initialization failed. Is RayTrace-MM module loaded?");
            g_RayTraceApi = null;
            return;
        }
        g_RayTraceApi = new CRayTrace();
    }

    private HookResult OnTakeDamage(CBaseEntity victim, CTakeDamageInfo damageinfo)
    {
        if (g_GameRules == null) return HookResult.Continue;

        if (victim == null || !victim.IsValid) return HookResult.Continue;

        if (victim.DesignerName != "player") return HookResult.Continue;

        var pawn = victim.As<CCSPlayerPawn>();

        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        if (pawn.OriginalController.Value is not { } victimController) return HookResult.Continue;

        if (victimController.IsBot || victimController.IsHLTV) return HookResult.Continue;

        var steamID = victimController.SteamID;

        if (g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing && damageinfo.Damage > 0)
        {
            damageinfo.Damage = 0;
        }

        return HookResult.Continue;
    }

    public HookResult OnMessage(UserMessage um)
    {
        if (Utilities.GetPlayerFromIndex(um.ReadInt("entityindex")) is not CCSPlayerController player || player.IsBot)
        {
            return HookResult.Continue;
        }

        if (!Config.ChatTriggersEnabled || player == null)
            return HookResult.Continue;

        string message = um.ReadString("param2");

        if (string.IsNullOrEmpty(message) || message.StartsWith("!") || message.StartsWith("/") || message.StartsWith("."))
            return HookResult.Continue;

        return OnPlayerSay(player, message);
    }

    /*
    Not using this since RemoveCommandListener("say") doesn't actually remove listener and was causing issue when hot-reloading plugin
    */
    // public HookResult OnSay(CCSPlayerController? player, CommandInfo command)
    // {
    //     if (!Config.ChatTriggersEnabled || player == null)
    //         return HookResult.Continue;

    //     string message = command.GetArg(1);

    //     if (string.IsNullOrEmpty(message) || message.StartsWith("!") || message.StartsWith("/") || message.StartsWith("."))
    //         return HookResult.Continue;

    //     OnPlayerSay(player, message);

    //     return HookResult.Continue;
    // }

    private HookResult OnPlayerSay(CCSPlayerController player, string message)
    {
        foreach (var trigger in g_ChatTriggers)
        {
            if (trigger.Value.Any(message.Equals))
            {
                bool hasPerm = HasPermision(player, Config.EmoteDanceCommandPerm);
                if (!hasPerm)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.no-access")}");
                    return HookResult.Stop;
                }

                hasPerm = HasPermision(player, trigger.Key.Permission);

                if (!hasPerm)
                {
                    player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.no-access")}");
                    return HookResult.Stop;
                }

                string error = "";
                if (!PlayEmote(player, trigger.Key, ref error))
                {
                    if (!string.IsNullOrEmpty(error))
                        player.PrintToChat(error);
                }
                return HookResult.Stop;
            }
        }
        return HookResult.Continue;
    }

    public void OnMapStart(string map)
    {
        g_PlayerSettings = new();

        g_GameRules = null;

        EmitSoundExtension.ClearSounds();

        playerWeapons.Clear();

        playerItems.Clear();

        AddTimer(1.0f, () =>
        {
            g_GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        });
    }

    public void OnTick()
    {
        foreach (var player in Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot))
        {
            var steamID = player.SteamID;

            if (!g_PlayerSettings.ContainsKey(steamID))
            {
                continue;
            }

            if (g_PlayerSettings[steamID].IsDancing)
            {
                /*
                if(player.PlayerPawn.IsValidPawnAlive() && !player.PlayerPawn.Value!.OnGroundLastTick)
                {
                    StopEmote(player);
                    continue;
                }
                */

                if (Config.ForcePlayerVisibility)
                {
                    SetPlayerInvisible(player);
                }

                if (player.PlayerPawn.IsValidPawnAlive() && GetPlayerSpeed(player) > 350 && Config.EmoteFreezePlayer)
                {
                    StopEmote(player);
                    continue;
                }

                if (Config.EmoteMenuType != 2 || (Config.EmoteMenuType == 2 && (Menu.GetMenus(player) == null || Menu.GetMenus(player)?.Count <= 0)))
                {
                    if (g_CancelButtons.Any(button => player.Buttons.ToString().Contains(button)))
                    {
                        StopEmote(player);
                        continue;
                    }
                }

                if (g_PlayerSettings[steamID].CameraProp != null && Config.CameraMoveable)
                {
                    UpdateCamera(g_PlayerSettings[steamID].CameraProp!, player, Config.CameraBlockObject);
                }

                if (!Config.EmoteFreezePlayer)
                {
                    if (g_PlayerSettings[steamID].CloneProp != null)
                    {
                        UpdateAnimProp(g_PlayerSettings[steamID].CloneProp!, player);
                    }
                }

                if (player.PlayerPawn.IsValidPawnAlive())
                {
                    var activeWeapon = player.PlayerPawn.Value!.WeaponServices!.ActiveWeapon.Value;

                    if (activeWeapon != null && activeWeapon.IsValid)
                    {
                        var tickThreshold = Server.TickCount + (64 * 60);
                        var tickNextAttack = Server.TickCount + (64 * 120);

                        var resetNextAttack = activeWeapon.NextSecondaryAttackTick <= tickThreshold;

                        if (resetNextAttack)
                        {
                            activeWeapon.NextPrimaryAttackTick = tickNextAttack;
                            Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");
                            activeWeapon.NextSecondaryAttackTick = tickNextAttack;
                            Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_nNextSecondaryAttackTick");
                        }
                    }
                }
            }
        }
    }

    public void OnServerPrecacheResources(ResourceManifest resource)
    {
        foreach (var sefile in Config.EmoteSoundEventFiles)
        {
            DebugLogs("Precaching sef: " + sefile);
            resource.AddResource(sefile);
        }

        List<string> precachedModels = new();

        foreach (var emote in Config.EmoteDances)
        {
            if (precachedModels.Contains(emote.Model))
                continue;

            DebugLogs("Precaching model: " + emote.Model);
            resource.AddResource(emote.Model);
            precachedModels.Add(emote.Model);
        }

        resource.AddResource("models/chicken/chicken.vmdl"); // Needs precache in non-competitive maps
    }

    [ConsoleCommand("css_et", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_etrigger", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_etriggers", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_dtrigger", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_dtriggers", "Displays all chat triggers for emotes/dance")]
    public void OnCommandListChatTriggers(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !Config.ChatTriggersEnabled)
            return;

        player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} {Localizer.ForPlayer(player, "emote.list-chat-triggers")}");

        string batch = "";

        int count = 0;
        foreach (var trigger in g_ListChatTriggers)
        {
            if (batch.Length + trigger.Length > 507)
            {
                player.PrintToChat($" {ChatColors.LightPurple}{batch},");
                batch = "";
            }

            if (string.IsNullOrEmpty(batch))
            {
                batch = trigger;
            }
            else
            {
                batch += ", " + trigger;
            }
            count++;
        }


        if (!string.IsNullOrEmpty(batch))
        {
            player.PrintToChat($" {ChatColors.LightPurple}{batch}");
        }
    }
}
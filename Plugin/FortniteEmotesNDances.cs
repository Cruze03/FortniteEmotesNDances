using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using System.Text.Json;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using FortniteEmotes.API;

namespace FortniteEmotes;

[MinimumApiVersion(260)]
public partial class Plugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Fortnite Emotes & Dances";
    public override string ModuleDescription => "CS2 Port of Fortnite Emotes & Dances";
    public override string ModuleAuthor => "Cruze";
    public override string ModuleVersion => "1.0.0";

    public required PluginConfig Config { get; set; } = new();

    private List<string> g_CancelButtons = new();

    private Dictionary<ulong, PlayerSettings> g_PlayerSettings = new();

    private Dictionary<Emote, List<string>> g_ChatTriggers = new();
    
    private List<string> g_ListChatTriggers = new();

    private Dictionary<string, string> g_EmoteTransMap = new();

    private bool g_bRoundEnd = false;

    private CCSGameRules? g_GameRules = null;

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
        
        foreach(var emote in Config.EmoteDances)
        {
            if(!g_ChatTriggers.ContainsKey(emote))
                g_ChatTriggers.Add(emote, emote.Trigger);
            foreach (var trigger in emote.Trigger)
            {
                if (!g_ListChatTriggers.Contains(trigger))
                {
                    g_ListChatTriggers.Add(trigger);
                }
            }
            if(!g_EmoteTransMap.ContainsKey(emote.Name))
                g_EmoteTransMap.Add(emote.Name, Localizer[emote.Name]);
        }
        
        foreach(var emoteCommand in Config.EmoteCommand)
        {
            RegisterCommand(emoteCommand, "List all the available emotes", (CCSPlayerController? player, CommandInfo command) =>
            {
                if(player == null)
                    return;
                
                if(command.ArgCount == 1)
                {
                    switch(Config.EmoteMenuType)
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

                if(emoteObj == null)
                {
                    player.PrintToChat($" {Localizer["emote.prefix"]} {Localizer["emote.not-found"]}");
                    return;
                }
                string error = "";
                if(!PlayEmote(player, emoteObj, ref error))
                {
                    player.PrintToChat(error);
                }
            }, CommandUsage.CLIENT_ONLY, permission: Config.EmoteDanceCommandPerm);
        }

        foreach(var danceCommand in Config.DanceCommand)
        {
            RegisterCommand(danceCommand, "List all the available dances", (CCSPlayerController? player, CommandInfo command) =>
            {
                if(player == null)
                    return;
                
                if(command.ArgCount == 1)
                {
                    switch(Config.EmoteMenuType)
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

                if(danceObj == null)
                {
                    player.PrintToChat($" {Localizer["emote.prefix"]} {Localizer["emote.not-found"]}");
                    return;
                }

                string error = "";
                if(!PlayEmote(player, danceObj, ref error))
                {
                    player.PrintToChat(error);
                }
            }, CommandUsage.CLIENT_ONLY, permission: Config.EmoteDanceCommandPerm);
        }

        foreach(var setemotecommand in Config.AdminSetEmoteCommand)
        {
            RegisterCommand(setemotecommand, "Set emote to player", (CCSPlayerController? player, CommandInfo command) =>
            {
                if(command.ArgCount != 3)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.setemote-usage", setemotecommand]}");
                    return;
                }

                string dance = command.GetArg(2);

                var emoteObj = GetEmoteByName(dance, true, true);

                if(emoteObj == null)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.not-found"]}");
                    return;
                }
                
                var targets = GetTarget(command);
                if (targets == null)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.target-not-found"]}");
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
                        if(PlayEmote(target, emoteObj, ref error, player))
                            count++;
                    }
                });
                if(count > 0)
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.played-emote-on-target", count]}");
                else
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.failplayed-emote-on-target"]}");
            }, CommandUsage.CLIENT_AND_SERVER, permission: Config.AdminSetEmoteDanceCommandPerm);
        }

        foreach(var setdancecommand in Config.AdminSetDanceCommand)
        {
            RegisterCommand(setdancecommand, "Set dance to player", (CCSPlayerController? player, CommandInfo command) =>
            {
                if(command.ArgCount != 3)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.setdance-usage", setdancecommand]}");
                    return;
                }

                string dance = command.GetArg(2);

                var danceObj = GetEmoteByName(dance, false, true);

                if(danceObj == null)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer["emote.not-found"]}");
                    return;
                }
                
                var targets = GetTarget(command);
                if (targets == null)
                {
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.target-not-found"]}");
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
                        if(PlayEmote(target, danceObj, ref error, player))
                            count++;
                    }
                });
                if(count > 0)
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.played-dance-on-target", count]}");
                else
                    command.ReplyToCommand($" {Localizer["emote.prefix"]} {Localizer[$"emote.failplayed-dance-on-target"]}");
            }, CommandUsage.CLIENT_AND_SERVER, permission: Config.AdminSetEmoteDanceCommandPerm);
        }

        var buttons = Config.EmoteCancelButtons.Split(',');

        foreach(var button in buttons)
        {
            if(button.Equals("w", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Forward.ToString());
            else if(button.Equals("s", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Back.ToString());
            else if(button.Equals("a", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Moveleft.ToString());
            else if(button.Equals("d", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Moveright.ToString());
            else if(button.Equals("use", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Use.ToString());
            else if(button.Equals("speed", StringComparison.CurrentCultureIgnoreCase))
            {
                g_CancelButtons.Add(PlayerButtons.Speed.ToString());
                g_CancelButtons.Add(PlayerButtons.Walk.ToString());
            }
            else if(button.Equals("jump", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Jump.ToString());
            else if(button.Equals("leftclick", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Attack.ToString());
            else if(button.Equals("crouch", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add(PlayerButtons.Duck.ToString());
            else if(button.Equals("scoreboard", StringComparison.CurrentCultureIgnoreCase) && Config.EmoteMenuType != 2)
                g_CancelButtons.Add("8589934592");
            else if(button.Equals("inspect", StringComparison.CurrentCultureIgnoreCase))
                g_CancelButtons.Add("34359738368");
        }
    }

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);

        AddCommandListener("say", OnSay, HookMode.Pre);
        AddCommandListener("say_team", OnSay, HookMode.Pre);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);

        Menu_OnLoad();
        API_OnLoad();

        if(hotReload)
        {
            OnMapStart(Server.MapName);
        }
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);

        StopAllEmotes();

        RemoveAllCommands();

        RemoveListener<Listeners.OnMapStart>(OnMapStart);
        RemoveListener<Listeners.OnTick>(OnTick);
        RemoveListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);

        RemoveCommandListener("say", OnSay, HookMode.Pre);
        RemoveCommandListener("say_team", OnSay, HookMode.Pre);
        VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Unhook(OnTakeDamage, HookMode.Pre);
    }

    private HookResult OnTakeDamage(DynamicHook h)
    {
        if (g_GameRules == null) return HookResult.Continue;

        if (!Config.StopDamageWhenInEmote) return HookResult.Continue;
        
        var victim = h.GetParam<CEntityInstance>(0); 

        if (victim == null || !victim.IsValid) return HookResult.Continue;

        if (victim.DesignerName != "player") return HookResult.Continue;

        var pawn = victim.As<CCSPlayerPawn>();

        if (pawn == null || !pawn.IsValid) return HookResult.Continue;
        
        if (pawn.OriginalController.Value is not { } victimController) return HookResult.Continue;

        if (victimController.IsBot || victimController.IsHLTV) return HookResult.Continue;

        var steamID = victimController.SteamID;

        var damageinfo = h.GetParam<CTakeDamageInfo>(1);

        if (g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing && damageinfo.Damage > 0)
        {
            damageinfo.Damage = 0;
        }

        return HookResult.Continue;
    }

    public HookResult OnSay(CCSPlayerController? player, CommandInfo command)
    {
        if(!Config.ChatTriggersEnabled || player == null)
            return HookResult.Continue;
            
        string message = command.GetArg(1);

        if(string.IsNullOrEmpty(message) || message.StartsWith("!") || message.StartsWith("/") || message.StartsWith("."))
            return HookResult.Continue;

        foreach(var trigger in g_ChatTriggers)
        {
            if(trigger.Value.Any(message.Equals))
            {
                bool hasPerm = (string.IsNullOrEmpty(Config.EmoteDanceCommandPerm) || AdminManager.PlayerHasPermissions(player, Config.EmoteDanceCommandPerm))
                && (string.IsNullOrEmpty(trigger.Key.Permission) || AdminManager.PlayerHasPermissions(player, trigger.Key.Permission));

                if(!hasPerm)
                {
                    player.PrintToChat($" {Localizer["emote.prefix"]} {Localizer["emote.no-access"]}");
                    return HookResult.Stop;
                }
                
                string error = "";
                if(!PlayEmote(player, trigger.Key, ref error))
                {
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

        AddTimer(1.0f, () =>
        {
            g_GameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        });
    }

    public void OnTick()
    {
        foreach(var player in Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot))
        {
            var steamID = player.SteamID;

            if(!g_PlayerSettings.ContainsKey(steamID))
            {
                continue;
            }

            if(g_PlayerSettings[steamID].IsDancing)
            {
                /*
                if(player.PlayerPawn.IsValidPawnAlive() && !player.PlayerPawn.Value!.OnGroundLastTick)
                {
                    StopEmote(player);
                    continue;
                }
                */
                
                if(player.PlayerPawn.IsValidPawnAlive() && GetPlayerSpeed(player) > 350 && Config.EmoteFreezePlayer)
                {
                    StopEmote(player);
                    continue;
                }
                
                if(Config.EmoteMenuType != 2 || (Config.EmoteMenuType == 2 && (Menu.GetMenus(player) == null || Menu.GetMenus(player)?.Count <= 0)))
                {
                    if (g_CancelButtons.Any(button => player.Buttons.ToString().Contains(button)))
                    {
                        StopEmote(player);
                        continue;
                    }
                }

                if(g_PlayerSettings[steamID].CameraProp != null)
                {
                    UpdateCamera(g_PlayerSettings[steamID].CameraProp!, player);
                }

                if(Config.SmoothCamera && Config.FixedCamera)
                {
                    if(g_PlayerSettings[steamID].AnimProp != null)
                    {
                        UpdateAnimProp(g_PlayerSettings[steamID].AnimProp!, player);
                    }
                }

                if(player.PlayerPawn.IsValidPawnAlive())
                {
                    var activeWeapon = player.PlayerPawn.Value!.WeaponServices!.ActiveWeapon.Value;

                    if(activeWeapon != null && activeWeapon.IsValid)
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
        foreach(var sefile in Config.EmoteSoundEventFiles)
        {
            DebugLogs("Precaching sef: " + sefile);
            resource.AddResource(sefile);
        }
        
        List<string> precachedModels = new();
        
        foreach(var emote in Config.EmoteDances)
        {
            if(precachedModels.Contains(emote.Model))
                continue;

            DebugLogs("Precaching model: " + emote.Model);
            resource.AddResource(emote.Model);
            precachedModels.Add(emote.Model);
        }
    }

    [ConsoleCommand("css_etrigger", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_etriggers", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_dtrigger", "Displays all chat triggers for emotes/dance")]
    [ConsoleCommand("css_dtriggers", "Displays all chat triggers for emotes/dance")]
    public void OnCommandListChatTriggers(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if(player == null || !Config.ChatTriggersEnabled)
            return;
        
        player.PrintToChat($" {Localizer["emote.prefix"]} {Localizer["emote.list-chat-triggers"]}");

        string batch = "";
        
        int count = 0;
        foreach (var trigger in g_ListChatTriggers)
        {
            if(batch.Length+trigger.Length > 507)
            {
                player.PrintToChat($" {ChatColors.LightPurple}{batch},");
                batch = "";
            }
            
            if(string.IsNullOrEmpty(batch))
            {
                batch = trigger;
            }
            else
            {
                batch += ", " + trigger;
            }
            count++;
        }


        if(!string.IsNullOrEmpty(batch))
        {
            player.PrintToChat($" {ChatColors.LightPurple}{batch}");
        }
    }
}
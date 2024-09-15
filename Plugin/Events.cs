using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace FortniteEmotes;
public partial class Plugin
{
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo @info)
    {
        g_bRoundEnd = false;
        foreach(var player in g_PlayerSettings)
        {
            if(player.Value.IsDancing)
            {
                player.Value.Reset();
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo @info)
    {
        g_bRoundEnd = true;
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo @info)
    {
        if(!Config.StopEmoteAfterFreezetimeEnd)
            return HookResult.Continue;
        
        foreach(var settings in g_PlayerSettings)
        {
            if(settings.Value.IsDancing)
            {
                var player = Utilities.GetPlayerFromSteamId(settings.Key);

                if(player != null)
                {
                    StopEmote(player);
                }
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if(player == null || !player.IsValid)
            return HookResult.Continue;
        
        StopEmote(player);
        
        if(Config.SmoothCamera)
        {
            Server.NextWorldUpdate(()=> 
            {
                if(Config.EmoteMenuType == 2 && Menu.GetMenus(player) != null && Menu.GetMenus(player)?.Count > 0)
                    SetPlayerMoveType(player, MoveType_t.MOVETYPE_OBSOLETE);
            });
        }
        return HookResult.Continue;
    }
    
    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if(player == null || !player.IsValid)
            return HookResult.Continue;
        
        var steamID = player.SteamID;
        if(g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing)
        {
            StopEmote(player);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if(player == null || !player.IsValid)
            return HookResult.Continue;
        
        var steamID = player.SteamID;
        if(g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing)
        {
            StopEmote(player);
        }
        return HookResult.Continue;
    }
}
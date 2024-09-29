using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using FortniteEmotes.API;

namespace FortniteEmotes;

public partial class Plugin
{
    public PluginCapability<IFortniteEmotesAPI> g_PluginCapability = new("FortniteEmotes");
    public FortniteEmotesApi FortniteEmotesApi { get; private set; } = null!;

    public void API_OnLoad()
    {
        FortniteEmotesApi = new FortniteEmotesApi(this);
        Capabilities.RegisterPluginCapability(g_PluginCapability, () => FortniteEmotesApi);
    }
}

public class FortniteEmotesApi : IFortniteEmotesAPI
{
    public Plugin plugin;

    public event IFortniteEmotesAPI.OnPlayerEmoteFunc? OnPlayerEmotePre;
    
    public FortniteEmotesApi(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public HookResult InvokeOnPlayerEmote(CCSPlayerController player, Emote emote)
    {
        if(OnPlayerEmotePre == null)
            return HookResult.Continue;
        
        return OnPlayerEmotePre.Invoke(player, emote);
    }
    
    public bool PlayEmote(CCSPlayerController player, string emote, ref string error)
    {
        var emoteObj = plugin.GetEmoteByName(emote, true);

        if(emoteObj == null)
        {
            throw new Exception($"PlayEmote failed because emote name '{emote}' not found.");
        }
        
        return plugin.PlayEmote(player, emoteObj, ref error, null);
    }

    public bool PlayDance(CCSPlayerController player, string dance, ref string error)
    {
        var danceObj = plugin.GetEmoteByName(dance, false);

        if(danceObj == null)
        {
            throw new Exception($"PlayDance failed because dance name '{dance}' not found.");
        }
        
        return plugin.PlayEmote(player, danceObj, ref error, null);
    }

    public void StopEmote(CCSPlayerController player)
    {
        plugin.StopEmote(player);
    }

    public bool IsDancing(CCSPlayerController player)
    {
        return plugin.IsDancing(player);
    }

    public bool IsReadyForDancing(CCSPlayerController player)
    {
        return plugin.IsReadyForDancing(player);
    }

    public List<Emote> GetDanceList()
    {
        return plugin.GetDanceList();
    }

    public List<Emote> GetEmoteList()
    {
        return plugin.GetEmoteList();
    }
}
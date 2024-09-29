using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

namespace FortniteEmotes.API;

public class Emote
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("Model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("AnimationName")]
    public string AnimationName { get; set; } = "";

    [JsonPropertyName("IsEmote")]
    public bool IsEmote { get; set; } = true;

    [JsonPropertyName("DefaultAnimationName")]
    public string DefaultAnimationName { get; set; } = "";

    [JsonPropertyName("Sound")]
    public string Sound { get; set; } = "";

    [JsonPropertyName("SoundVolume")]
    public float SoundVolume { get; set; } = 0.7f;

    [JsonPropertyName("LoopSoundAfterSeconds")]
    public float LoopSoundAfterSeconds { get; set; } = -1;

    [JsonPropertyName("AnimationDuration")]
    public float AnimationDuration { get; set; } = -1;

    [JsonPropertyName("SetToDefaultAnimationDuration")]
    public float SetToDefaultAnimationDuration { get; set; } = -1;

    [JsonPropertyName("Permission")]
    public List<string> Permission { get; set; } = new(){""};

    [JsonPropertyName("Trigger")]
    public List<string> Trigger { get; set; } = new() {"emote", "emotes"};
}

/// <summary>
/// API for FortniteEmotes.
/// </summary>
public interface IFortniteEmotesAPI
{
	delegate HookResult OnPlayerEmoteFunc(CCSPlayerController player, Emote emote);

    /// <summary>
    /// Is called when a player tries to play an emote/dance
    /// HookResult.Handled || HookResult.Stop will stop emote/dance from playing
    /// </summary>
    /// <param name="handler">Forward when emote/dance is about to be played</param>
    public event OnPlayerEmoteFunc? OnPlayerEmotePre;
    
    /// <summary>
    /// if emote is unable to be played, will return false
    /// and error will have the reason.
    /// </summary>
    /// <param name="handler">Play emote for player</param>
    public bool PlayEmote(CCSPlayerController player, string name, ref string error);
	
	/// <summary>
    /// if dance is unable to be played, will return false
    /// and error will have the reason.
    /// </summary>
    /// <param name="handler">Play dance for player</param>
    public bool PlayDance(CCSPlayerController player, string name, ref string error);
	
	/// <summary>
    /// if not dancing, does nothing
    /// </summary>
    /// <param name="handler">Stops emote / dance for player</param>
    public void StopEmote(CCSPlayerController player);
	
	/// <summary>
    /// returns true if player is in emote / dance
    /// </summary>
    /// <param name="handler">Dance status of player</param>
    public bool IsDancing(CCSPlayerController player);

    /// <summary>
    /// returns true if player is ready for emote/dance
    /// </summary>
    /// <param name="handler">If player can dance</param>
    public bool IsReadyForDancing(CCSPlayerController player);
	
	/// <summary>
    /// returns emote list
    /// </summary>
    /// <param name="handler">Get list of all emotes</param>
    public List<Emote> GetEmoteList();
	
	/// <summary>
    /// returns dance list
    /// </summary>
    /// <param name="handler">Get list of all dances</param>
    public List<Emote> GetDanceList();
}
using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using FortniteEmotes.API;

namespace FortniteEmotes;
public partial class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("EmoteAllowedPeriod")]
    public int EmoteAllowedPeriod { get; set; } = 0;

    [JsonPropertyName("StopEmoteAfterFreezetimeEnd")]
    public bool StopEmoteAfterFreezetimeEnd { get; set; } = false;

    [JsonPropertyName("StopDamageWhenInEmote")]
    public bool StopDamageWhenInEmote { get; set; } = false;

    [JsonPropertyName("EmoteMenuType")]
    public int EmoteMenuType { get; set; } = 2;

    [JsonPropertyName("EmoteHidePlayers")]
    public int EmoteHidePlayers { get; set; } = 0;

    [JsonPropertyName("KitsuneMenuDeveloperDisplay")]
    public bool KitsuneMenuDeveloperDisplay { get; set; } = true;

    [JsonPropertyName("SmoothCamera")]
    public bool SmoothCamera { get; set; } = true;

    [JsonPropertyName("FixedCamera")]
    public bool FixedCamera { get; set; } = false;

    [JsonPropertyName("EmoteFreezePlayer")]
    public bool EmoteFreezePlayer { get; set; } = true;

    [JsonPropertyName("EmoteCooldown")]
    public int EmoteCooldown { get; set; } = 20;

    [JsonPropertyName("EmoteVIPCooldown")]
    public int EmoteVIPCooldown { get; set; } = 15;

    [JsonPropertyName("EmoteCancelButtons")]
    public string EmoteCancelButtons { get; set; } = "w,s,a,d,jump,crouch,leftclick";

    [JsonPropertyName("EmoteSoundEventFiles")]
    public List<string> EmoteSoundEventFiles { get; set; } = new();

    [JsonPropertyName("EmoteDanceCommandPerm")]
    public List<string> EmoteDanceCommandPerm { get; set; } = new(){""};

    [JsonPropertyName("AdminSetEmoteDanceCommandPerm")]
    public List<string> AdminSetEmoteDanceCommandPerm { get; set; } = new() {"@css/root"};

    [JsonPropertyName("VIPPerm")]
    public List<string> VIPPerm { get; set; } = new() {"@css/vip"};

    [JsonPropertyName("SoundModuleEnabled")]
    public bool SoundModuleEnabled { get; set; } = true;

    [JsonPropertyName("ChatTriggersEnabled")]
    public bool ChatTriggersEnabled { get; set; } = true;

    [JsonPropertyName("EmoteCommand")]
    public List<string> EmoteCommand { get; set; } = new() {"emote","emotes"};

    [JsonPropertyName("DanceCommand")]
    public List<string> DanceCommand { get; set; } = new() {"dance","dances"};

    [JsonPropertyName("AdminSetEmoteCommand")]
    public List<string> AdminSetEmoteCommand { get; set; } = new() {"setemote","setemotes"};

    [JsonPropertyName("AdminSetDanceCommand")]
    public List<string> AdminSetDanceCommand { get; set; } = new() {"setdance","setdances"};

    [JsonPropertyName("EmoteDances")]
    public List<Emote> EmoteDances { get; set; } = new List<Emote>();

    [JsonPropertyName("DebugLogs")]
    public bool DebugLogs { get; set; } = false;
    
    [JsonPropertyName("ConfigVersion")]
	public override int Version { get; set; } = 3;
}
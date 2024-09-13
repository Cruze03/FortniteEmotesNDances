using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using FortniteEmotes.API;

namespace FortniteEmotes;
public partial class PluginConfig : BasePluginConfig
{
    public int EmoteAllowedPeriod { get; set; } = 0;
    public bool StopEmoteAfterFreezetimeEnd { get; set; } = false;
    public bool StopDamageWhenInEmote { get; set; } = false;
    public int EmoteMenuType { get; set; } = 2;
    public bool KitsuneMenuDeveloperDisplay { get; set; } = true;
    public bool SmoothCamera { get; set; } = true;
    public bool FixedCamera { get; set; } = false;
    public bool EmoteFreezePlayer { get; set; } = true;
    public int EmoteCooldown { get; set; } = 20;
    public int EmoteVIPCooldown { get; set; } = 15;
    public string EmoteCancelButtons { get; set; } = "w,s,a,d,jump,crouch,leftclick";
    public List<string> EmoteSoundEventFiles { get; set; } = new();
    public string EmoteDanceCommandPerm { get; set; } = "";
    public string AdminSetEmoteDanceCommandPerm { get; set; } = "@css/root";
    public string VIPPerm { get; set; } = "@css/vip";
    public bool SoundModuleEnabled { get; set; } = true;
    public bool ChatTriggersEnabled { get; set; } = true;
    public List<string> EmoteCommand { get; set; } = new() {"emote","emotes"};
    public List<string> DanceCommand { get; set; } = new() {"dance","dances"};
    public List<string> AdminSetEmoteCommand { get; set; } = new() {"setemote","setemotes"};
    public List<string> AdminSetDanceCommand { get; set; } = new() {"setdance","setdances"};
    public List<Emote> EmoteDances { get; set; } = new List<Emote>();
    public bool DebugLogs { get; set; } = false;
}
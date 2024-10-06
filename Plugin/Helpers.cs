using System.Diagnostics.Metrics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;
using FortniteEmotes.API;

namespace FortniteEmotes;
public partial class Plugin
{
    private static readonly MemoryFunctionWithReturn<nint, string, int, int> SetBodygroupFunc = new(GameData.GetSignature("CBaseModelEntity_SetBodygroup"));

	private static readonly Func<nint, string, int, int> SetBodygroup = SetBodygroupFunc.Invoke;
    
    public class PlayerSettings
    {
        public uint CloneModelIndex { get; set; } = 0;
        public uint EmoteModelIndex { get; set; } = 0;
        public int PlayerAlpha { get; set; } = 255;
        public MoveType_t PlayerMoveType { get; set; } = MoveType_t.MOVETYPE_WALK;
        public CDynamicProp? CameraProp { get; set; } = null;
        public CDynamicProp? AnimProp { get; set; } = null;
        public uint CameraPropIndex { get; set; } = 0;
        public int Cooldown { get; set; } = 0;
        public CSSTimer? Timer { get; set; } = null;
        public CSSTimer? DefaultAnimTimer { get; set; } = null;
        public CSSTimer? SoundTimer { get; set; } = null;
        public bool IsDancing { get; set; } = false;
        public string CurrentSound { get; set; } = "";

        public PlayerSettings()
        {
            CloneModelIndex = 0;
            AnimProp = null;
            EmoteModelIndex = 0;
            PlayerAlpha = 255;
            PlayerMoveType = MoveType_t.MOVETYPE_WALK;
            CameraProp = null;
            CameraPropIndex = 0;
            Cooldown = 0;
            Timer = null;
            DefaultAnimTimer = null;
            SoundTimer = null;
            IsDancing = false;
            CurrentSound = "";
        }

        public void Reset()
        {
            AnimProp = null;
            CloneModelIndex = 0;
            EmoteModelIndex = 0;
            PlayerAlpha = 255;
            PlayerMoveType = MoveType_t.MOVETYPE_WALK;
            CameraProp = null;
            CameraPropIndex = 0;
            IsDancing = false;
            Timer?.Kill();
            Timer = null;
            DefaultAnimTimer?.Kill();
            DefaultAnimTimer = null;
            SoundTimer?.Kill();
            SoundTimer = null;
            CurrentSound = "";
        }
    }

    public bool PlayEmote(CCSPlayerController target, Emote emote, ref string error, CCSPlayerController? player = null)
    {
        switch(Config.EmoteAllowedPeriod)
        {
            case 1:
                if(g_GameRules == null || (!g_GameRules.WarmupPeriod && !g_GameRules.FreezePeriod))
                {
                    error = $" {Localizer["emote.prefix"]} {Localizer["emote.notallowed.warmupftcheck"]}";
                    return false;
                }
                break;
            case 2:
                if(g_GameRules == null || (!g_GameRules.WarmupPeriod && !g_GameRules.FreezePeriod && !g_bRoundEnd))
                {
                    error = $" {Localizer["emote.prefix"]} {Localizer["emote.notallowed.warmupftrecheck"]}";
                    return false;
                }
                break;
            default:
                break;
        }
        
        if(!target.IsValidPlayer() || !target.PlayerPawn.IsValidPawnAlive() || target.ControllingBot || target.AbsOrigin == null || target.PlayerPawn.Value!.CameraServices == null)
        {
            error = $" {Localizer["emote.prefix"]} {Localizer[$"emote{(player == null ? "":".player")}.alivecheck"]}";
            return false;
        }

        if(target.PlayerPawn.Value.IsScoped)
        {
            error = $" {Localizer["emote.prefix"]} {Localizer[$"emote{(player == null ? "":".player")}.scopecheck"]}";
            return false;
        }
        
        var steamID = target.SteamID;
        
        if(!g_PlayerSettings.ContainsKey(steamID))
            g_PlayerSettings[steamID] = new PlayerSettings();
        
        int time = GetTime();
        
        if(g_PlayerSettings[steamID].Cooldown > time && player == null)
        {
            error = $" {Localizer["emote.prefix"]} {Localizer["emote.cooldowncheck", g_PlayerSettings[steamID].Cooldown - time]}";
            return false;
        }

        if(target.Pawn.IsValidPawn())
        {
            if(((PlayerFlags)target.Pawn.Value!.Flags & PlayerFlags.FL_ONGROUND) != PlayerFlags.FL_ONGROUND || (target.PlayerPawn.Value.GroundEntity != null && target.PlayerPawn.Value.GroundEntity.IsValid && target.PlayerPawn.Value.GroundEntity.Index != 0))
            {
                error = $" {Localizer["emote.prefix"]} {Localizer[$"emote{(player == null ? "":".player")}.groundcheck"]}";
                return false;
            }
            if(((PlayerFlags)target.Pawn.Value!.Flags & PlayerFlags.FL_DUCKING) == PlayerFlags.FL_DUCKING)
            {
                error = $" {Localizer["emote.prefix"]} {Localizer[$"emote{(player == null ? "":".player")}.duckcheck"]}";
                return false;
            }
        }

        // DebugLogs($"GroundEntity Info: {(target.PlayerPawn.Value.GroundEntity == null ? "null" : "not-null")} | {target.PlayerPawn.Value.GroundEntity?.IsValid ?? false} | {target.PlayerPawn.Value.GroundEntity?.Index ?? 1337420}");

        var result = FortniteEmotesApi.InvokeOnPlayerEmote(target, emote);
        if(result == HookResult.Handled || result == HookResult.Stop)
        {
            string message = $"{Localizer[$"emote.stoppedbyapi"]}";
            
            if(string.IsNullOrEmpty(message))
            {
                error = "";
            }
            else
            {
                error = $" {Localizer["emote.prefix"]} {Localizer[$"emote.stoppedbyapi"]}";
            }
            return false;
        }

        if(!EmotesEnable.Value)
        {
            error = $" {Localizer["emote.prefix"]} {Localizer[$"emote{(player == null ? "":".player")}.disabledbyadmin"]}";
            return false;
        }

        if(g_PlayerSettings[steamID].IsDancing)
        {
            DebugLogs("Player already dancing, stopping emote");
            StopEmote(target);
        }
        
        CDynamicProp? prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
            
        if (prop == null || prop.Entity == null  || prop.Entity.Handle == IntPtr.Zero || !prop.IsValid)
        {
            error = $" {Localizer["emote.prefix"]} {Localizer["emote.unknownerror"]}";
            return false;
        }

        string propName = "emoteEnt_" + new Random().Next(1000000, 9999999).ToString();
        prop.Entity.Name = propName;

        prop.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        
        prop.SetModel(emote.Model);

        SetCollision(prop, CollisionGroup.COLLISION_GROUP_NEVER, SolidType_t.SOLID_NONE, 12);

        SetPropInvisible(prop);

        prop.DispatchSpawn();
        prop.Teleport(target.PlayerPawn.Value.AbsOrigin, target.PlayerPawn.Value.AbsRotation, null);
        prop.UseAnimGraph = false;

        var cloneprop = CreateClone(target, prop, propName);
        g_PlayerSettings[steamID].CloneModelIndex = cloneprop?.Index ?? 0;
        g_PlayerSettings[steamID].AnimProp = prop;
        
        if(g_PlayerSettings[steamID].CloneModelIndex == 0 && !Config.SmoothCamera)
        {
            SetPlayerEffects(target, true);
            target.PlayerPawn.Value.AcceptInput("FollowEntity", target.PlayerPawn.Value, target.PlayerPawn.Value, propName);
        }

        SetPlayerWeaponInvisible(target);

        RefreshPlayerGloves(target);

        g_PlayerSettings[steamID].EmoteModelIndex = prop.Index;
   
        prop.AcceptInput("SetAnimation", value: emote.AnimationName);

        if(!string.IsNullOrEmpty(emote.DefaultAnimationName))
        {
            if(emote.SetToDefaultAnimationDuration > 0)
            {
                g_PlayerSettings[steamID].DefaultAnimTimer?.Kill();
                g_PlayerSettings[steamID].DefaultAnimTimer = AddTimer(emote.SetToDefaultAnimationDuration, () =>
                {
                    if(!target.IsValidPlayer() || !g_PlayerSettings[steamID].IsDancing)
                    {
                        g_PlayerSettings[steamID].Reset();
                        return;
                    }
                    
                    prop.AcceptInput("SetAnimation", value: emote.DefaultAnimationName);
                    
                    if(emote.AnimationDuration > 0)
                    {
                        g_PlayerSettings[steamID].Timer?.Kill();
                        g_PlayerSettings[steamID].Timer = AddTimer(emote.AnimationDuration, () => StopEmote(target));
                    }
                });
            }
            else
            {
                HookSingleEntityOutput(prop, "OnAnimationDone", EndAnimation);
            }
        }
        else if(emote.AnimationDuration > 0)
        {
            g_PlayerSettings[steamID].Timer?.Kill();
            g_PlayerSettings[steamID].Timer = AddTimer(emote.AnimationDuration, () => StopEmote(target));
        }
        else
        {
            HookSingleEntityOutput(prop, "OnAnimationDone", EndAnimation);
            // HookSingleEntityOutput(prop, "OnAnimationLoopCycleDone", Test);
            // HookSingleEntityOutput(prop, "OnAnimationReachedEnd", Test);
        }

        g_PlayerSettings[steamID].CameraProp = SetCam(target);
        g_PlayerSettings[steamID].CameraPropIndex = g_PlayerSettings[steamID].CameraProp?.Index ?? 0;
        
        Server.RunOnTick(Server.TickCount + 4, () =>
        {
            g_PlayerSettings[steamID].IsDancing = true;
        });

        if(player == null)
        {
            bool hasVIP = false;
            foreach(var perm in Config.VIPPerm)
            {
                if(string.IsNullOrEmpty(perm))
                {
                    hasVIP = true;
                    break;
                }
                if(perm[0] == '@' && AdminManager.PlayerHasPermissions(target, perm))
                {
                    hasVIP = true;
                    break;
                }
                else if(perm[0] == '#' && AdminManager.PlayerInGroup(target, perm))
                {
                    hasVIP = true;
                    break;
                }
            }
            
            g_PlayerSettings[steamID].Cooldown = time + (hasVIP ? Config.EmoteVIPCooldown : Config.EmoteCooldown);
        }
        
        string emoteName = $"{Localizer[$"{emote.Name}"]}";
        
        target.PrintToChat($" {Localizer["emote.prefix"]} {Localizer["emote.playing", emoteName]}");

        if(Config.SoundModuleEnabled)
        {
            g_PlayerSettings[steamID].SoundTimer?.Kill();
            if(!string.IsNullOrEmpty(emote.Sound))
            {
                DebugLogs("SoundPlayed: " + emote.Sound);
                EmitSound(target, emote.Sound, emote.SoundVolume);
                if(emote.LoopSoundAfterSeconds > 0)
                {
                    g_PlayerSettings[steamID].SoundTimer = AddTimer(emote.LoopSoundAfterSeconds, () =>
                    {
                        if(!target.IsValidPlayer() || !g_PlayerSettings[steamID].IsDancing)
                        {
                            g_PlayerSettings[steamID].Reset();
                            return;
                        }
                        
                        DebugLogs("SoundPlayed: " + emote.Sound);
                        EmitSound(target, emote.Sound, emote.SoundVolume);
                    }, TimerFlags.REPEAT|TimerFlags.STOP_ON_MAPCHANGE);
                }
            }
        }

        return true;
    }

    private CDynamicProp? CreateClone(CCSPlayerController player, CDynamicProp prop, string propName)
    {
        if(!Config.SmoothCamera)
        {
            return null;
        }

        string model = player.PlayerPawn.Value?.CBodyComponent?.SceneNode?.GetSkeletonInstance().ModelState.ModelName ?? string.Empty;

        DebugLogs("Model: " + model);

        if(string.IsNullOrEmpty(model))
        {
            return null;
        }
        
        CDynamicProp? clone = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (clone == null || clone.Entity == null || clone.Entity.Handle == IntPtr.Zero || !clone.IsValid)
        {
            return null;
        }

        clone.Entity.Name = propName+"_clone";
        clone.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));
        clone.SetModel(model);
        SetCollision(clone, CollisionGroup.COLLISION_GROUP_NEVER, SolidType_t.SOLID_NONE, 12);
        clone.DispatchSpawn();
        clone.Teleport(player.PlayerPawn.Value!.AbsOrigin, player.PlayerPawn.Value.AbsRotation, null);
        clone.UseAnimGraph = false;

        clone.AcceptInput("FollowEntity", prop, prop, propName);
        
        var steamID = player.SteamID;
        
        if(Config.EmoteFreezePlayer)
        {
            if(g_PlayerSettings.ContainsKey(steamID))
                g_PlayerSettings[steamID].PlayerMoveType = Config.EmoteMenuType == 2 && Menu.GetMenus(player) != null && Menu.GetMenus(player)?.Count > 0 ? MoveType_t.MOVETYPE_WALK :  player.PlayerPawn.Value.ActualMoveType;
            SetPlayerMoveType(player, MoveType_t.MOVETYPE_OBSOLETE);
        }
        
        clone.Render = Color.FromArgb(255, 255, 255, 255);
        Utilities.SetStateChanged(clone, "CBaseModelEntity", "m_clrRender");

        Server.NextWorldUpdate(() =>
        {
            if(g_PlayerSettings.ContainsKey(steamID))
                g_PlayerSettings[steamID].PlayerAlpha = player.PlayerPawn.Value!.Render.A;
            SetPlayerInvisible(player);
        });
        return clone;
    } 

    public HookResult Test(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        if(caller == null || !caller.IsValid) return HookResult.Continue;

        DebugLogs(name);
        
        return HookResult.Continue;
    }

    public HookResult EndAnimation(CEntityIOOutput output, string name, CEntityInstance activator, CEntityInstance caller, CVariant value, float delay)
    {
        if(caller == null || !caller.IsValid) return HookResult.Continue;

        DebugLogs("OnAnimationDone");
        
        foreach(var player in g_PlayerSettings)
        {
            if(player.Value.EmoteModelIndex == caller.Index)
            {
                var play = Utilities.GetPlayerFromSteamId(player.Key);
                if(play != null && play.IsValidPlayer())
                {
                    StopEmote(play);
                }
                else
                {
                    caller.Remove();
                }
                break;
            }
        }
        
        return HookResult.Continue;
    }

    public bool IsDancing(CCSPlayerController player)
    {
        if(!player.IsValidPlayer())
            return false;
        
        var steamID = player.SteamID;
        
        return g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing;
    }

    public bool IsReadyForDancing(CCSPlayerController player)
    {
        switch(Config.EmoteAllowedPeriod)
        {
            case 1:
                if(g_GameRules == null || (!g_GameRules.WarmupPeriod && !g_GameRules.FreezePeriod))
                {
                    return false;
                }
                break;
            case 2:
                if(g_GameRules == null || (!g_GameRules.WarmupPeriod && !g_GameRules.FreezePeriod && !g_bRoundEnd))
                {
                    return false;
                }
                break;
            default:
                break;
        }
        
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return false;
        
        if(player.Pawn.IsValidPawn())
        {
            if(((PlayerFlags)player.Pawn.Value!.Flags & PlayerFlags.FL_ONGROUND) != PlayerFlags.FL_ONGROUND || (player.PlayerPawn.Value!.GroundEntity != null && player.PlayerPawn.Value.GroundEntity.IsValid && player.PlayerPawn.Value.GroundEntity.Index != 0))
                return false;
            if(((PlayerFlags)player.Pawn.Value!.Flags & PlayerFlags.FL_DUCKING) == PlayerFlags.FL_DUCKING)
                return false;
        }

        if(player.PlayerPawn.Value!.IsScoped)
            return false;
        
        var steamID = player.SteamID;
        
        if(!g_PlayerSettings.ContainsKey(steamID))
            g_PlayerSettings[steamID] = new PlayerSettings();
        
        if(g_PlayerSettings[steamID].IsDancing)
            return false;

        return true;
    }

    public List<Emote> GetDanceList()
    {
        return Config.EmoteDances.Where(x => !x.IsEmote).ToList();
    }

    public List<Emote> GetEmoteList()
    {
        return Config.EmoteDances.Where(x => x.IsEmote).ToList();
    }

    public void StopAllEmotes()
    {
        foreach(var player in Utilities.GetPlayers().Where(p => !p.IsHLTV && !p.IsBot && p.PawnIsAlive))
        {
            var steamID = player.SteamID;

            if(!g_PlayerSettings.ContainsKey(steamID))
            {
                continue;
            }

            if(g_PlayerSettings[steamID].IsDancing)
            {
                StopEmote(player);
            }
        }
    }

    public void StopEmote(CCSPlayerController player, bool force = false)
    {
        if(!player.IsValidPlayer())
            return;
        
        DebugLogs("StopEmote");
        
        var steamID = player.SteamID;

        if(!g_PlayerSettings.TryGetValue(steamID, out var settings))
            return;
        
        if(!g_PlayerSettings[steamID].IsDancing && !force)
            return;
        
        g_PlayerSettings[steamID].IsDancing = false;
        
        if(g_PlayerSettings[steamID].Timer != null)
        {
            g_PlayerSettings[steamID].Timer?.Kill();
        }
        if(g_PlayerSettings[steamID].SoundTimer != null)
        {
            g_PlayerSettings[steamID].SoundTimer?.Kill();
        }
        if(g_PlayerSettings[steamID].DefaultAnimTimer != null)
        {
            g_PlayerSettings[steamID].DefaultAnimTimer?.Kill();
        }
        g_PlayerSettings[steamID].Timer = null;
        g_PlayerSettings[steamID].SoundTimer = null;
        g_PlayerSettings[steamID].DefaultAnimTimer = null;

        if(Config.SoundModuleEnabled)
        {
            if(!string.IsNullOrEmpty(g_PlayerSettings[steamID].CurrentSound))
            {
                player.StopSound(g_PlayerSettings[steamID].CurrentSound);
                g_PlayerSettings[steamID].CurrentSound = "";
            }
        }
        
        var emoteModels = Utilities.FindAllEntitiesByDesignerName<CDynamicProp>("prop_dynamic").Where(p => p != null
        && p.IsValid
        && ((settings.EmoteModelIndex != 0 && p.Index == settings.EmoteModelIndex) || (settings.CloneModelIndex != 0 && p.Index == settings.CloneModelIndex) || (settings.CameraPropIndex != 0 && p.Index == settings.CameraPropIndex))
        ).ToList();

        ResetCam(player);

        SetPlayerWeaponVisible(player);

        if(!Config.SmoothCamera)
        {
            SetPlayerEffects(player, false);
        }
        else
        {
            SetPlayerVisible(player);
            
            if(g_PlayerSettings.ContainsKey(steamID)
            && (Config.EmoteMenuType != 2 || (Config.EmoteMenuType == 2 && (Menu.GetMenus(player) == null || Menu.GetMenus(player)?.Count <= 0)))
            && player.PlayerPawn.IsValidPawnAlive()
            && g_PlayerSettings[steamID].PlayerMoveType != player.PlayerPawn.Value!.ActualMoveType)
                SetPlayerMoveType(player, g_PlayerSettings[steamID].PlayerMoveType);
        }

        RefreshPlayerGloves(player, true);

        var activeWeapon = player.PlayerPawn.Value!.WeaponServices!.ActiveWeapon.Value;

        if(activeWeapon != null && activeWeapon.IsValid)
        {
            activeWeapon.NextPrimaryAttackTick = -1;
            Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_nNextPrimaryAttackTick");
            activeWeapon.NextSecondaryAttackTick = -1;
            Utilities.SetStateChanged(activeWeapon, "CBasePlayerWeapon", "m_nNextSecondaryAttackTick");
        }

        g_PlayerSettings[steamID].PlayerAlpha = 255;
        g_PlayerSettings[steamID].PlayerMoveType = MoveType_t.MOVETYPE_WALK;
        g_PlayerSettings[steamID].CameraProp = null;
        g_PlayerSettings[steamID].AnimProp = null;

        foreach (var model in emoteModels)
        {
            if(model != null && model.IsValid &&  model.Entity != null && (g_PlayerSettings[steamID].EmoteModelIndex == model.Index || g_PlayerSettings[steamID].CloneModelIndex == model.Index || g_PlayerSettings[steamID].CameraPropIndex == model.Index))
            {
                // player.PlayerPawn.Value?.AcceptInput("ClearParent", player.PlayerPawn.Value, player.PlayerPawn.Value, model.Entity.Name);
                // player.PlayerPawn.Value?.AcceptInput("StopFollowingEntity", player.PlayerPawn.Value, player.PlayerPawn.Value, model.Entity.Name);
                model.Remove();
            }
        }
        g_PlayerSettings[steamID].EmoteModelIndex = 0;
        g_PlayerSettings[steamID].CloneModelIndex = 0;
        g_PlayerSettings[steamID].CameraPropIndex = 0;
    }

    private int GetTime()
    {
        return (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private int GetPlayerSpeed(CCSPlayerController player)
    {
        return (int)Math.Round(player.PlayerPawn.Value!.AbsVelocity.Length2D());
    }

    private void SetCollision(CBaseEntity entity, CollisionGroup collisionGroup, SolidType_t solidType, byte solidFlags)
    {
        if(entity.Collision == null) return;
        
        entity.Collision.CollisionAttribute.CollisionGroup = (byte)collisionGroup;
        entity.Collision.CollisionGroup = (byte)collisionGroup;
        entity.Collision.SolidType = solidType;
        entity.Collision.SolidFlags = solidFlags;

        Utilities.SetStateChanged(entity, "CCollisionProperty", "m_CollisionGroup");
        Utilities.SetStateChanged(entity, "CCollisionProperty", "m_collisionAttribute");
        Utilities.SetStateChanged(entity, "CCollisionProperty", "m_nSolidType");
        Utilities.SetStateChanged(entity, "CCollisionProperty", "m_usSolidFlags");

        VirtualFunctionVoid<nint> collisionRulesChanged = new VirtualFunctionVoid<nint>(entity.Handle, GameData.GetOffset("CBaseEntity_CollisionRulesChanged"));

        // Invokes the updated CollisionRulesChanged information to ensure the player's collision is correctly set
        collisionRulesChanged.Invoke(entity.Handle);
    }

    private CDynamicProp? SetCam(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive() || player.AbsOrigin == null || player.PlayerPawn.Value!.CameraServices == null)
            return null;

        CDynamicProp? prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic");
        if (prop == null)
            return null;
        
        prop.Teleport(CalculatePositionInFront(player, -110, 75), player.PlayerPawn.Value.V_angle, new Vector());

        prop.Entity!.Name = "cameraProp_" + new Random().Next(1000000, 9999999).ToString();
        prop.CBodyComponent!.SceneNode!.Owner!.Entity!.Flags &= unchecked((uint)~(1 << 2));

        prop.SetModel("models/chicken/chicken.vmdl");

        SetPropInvisible(prop);
        
        prop.DispatchSpawn();
        prop.Teleport(player.PlayerPawn.Value.AbsOrigin, player.PlayerPawn.Value.V_angle, new Vector());

        SetCollision(prop, CollisionGroup.COLLISION_GROUP_NEVER, SolidType_t.SOLID_VPHYSICS, 12);
        
        Server.NextWorldUpdate(() =>
        {
            player.PlayerPawn.Value.CameraServices.ViewEntity.Raw = prop.EntityHandle.Raw;

            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices");
        });

        return prop;
    }

    public void ResetCam(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive() || player.AbsOrigin == null || player.PlayerPawn.Value!.CameraServices == null)
            return;

        player.PlayerPawn.Value.CameraServices.ViewEntity.Raw = uint.MaxValue;

        Server.NextWorldUpdate(() => Utilities.SetStateChanged(player.PlayerPawn.Value, "CBasePlayerPawn", "m_pCameraServices"));

        var steamID = player.SteamID;
        
        g_PlayerSettings[steamID].CameraPropIndex = 0;
    }

    private void SetPlayerEffects(CCSPlayerController player, bool set)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;
        
        if(set)
        {
            // DebugLogs("Add effects Pre: " + player.PlayerPawn.Value!.Effects);
            var enteffects = player.PlayerPawn.Value!.Effects;
            
            enteffects |= 1; // This is EF_BONEMERGE
            //enteffects |= 16; // This is EF_NOSHADOW
            //enteffects |= 64; // This is EF_NORECEIVESHADOW
            enteffects |= 128; // This is EF_BONEMERGE_FASTCULL
            enteffects |= 512; // This is EF_PARENT_ANIMATES
            player.PlayerPawn.Value.Effects = enteffects;
            // DebugLogs("Add effects Post: " + player.PlayerPawn.Value!.Effects);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_fEffects");
        }
        else
        {
            // DebugLogs("Remove effects Pre: " + player.PlayerPawn.Value!.Effects);
            
            int enteffects = (int)player.PlayerPawn.Value!.Effects;
            
            enteffects &= ~1; // This is EF_BONEMERGE
            //enteffects &= ~16; // This is EF_NOSHADOW
            //enteffects &= ~64; // This is EF_NORECEIVESHADOW
            enteffects &= ~128; // This is EF_BONEMERGE_FASTCULL
            enteffects &= ~512; // This is EF_PARENT_ANIMATES
            player.PlayerPawn.Value.Effects = (uint)enteffects;
            // DebugLogs("Remove effects Post: " + player.PlayerPawn.Value!.Effects);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_fEffects");
        }
    }

    private static void SetPropInvisible(CDynamicProp entity)
    {
        if (entity == null || !entity.IsValid)
        {
            return;
        }

        entity.Render = Color.FromArgb(0, 255, 255, 255);
        Utilities.SetStateChanged(entity, "CBaseModelEntity", "m_clrRender");
    }

    public static void SetPlayerInvisible(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;
        
        player.PlayerPawn.Value!.Render = Color.FromArgb(0, 255, 255, 255);
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
    }

    public void SetPlayerVisible(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;

        var steamID = player.SteamID;
        if(g_PlayerSettings.ContainsKey(steamID))
        {
            player.PlayerPawn.Value!.Render = Color.FromArgb(g_PlayerSettings[steamID].PlayerAlpha, 255, 255, 255);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
        }
        else
        {
            player.PlayerPawn.Value!.Render = Color.FromArgb(255, 255, 255, 255);
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseModelEntity", "m_clrRender");
        }
    }

    public static void SetPlayerMoveType(CCSPlayerController player, MoveType_t type)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;

        player.PlayerPawn.Value!.MoveType = type;
        player.PlayerPawn.Value.ActualMoveType = type;
        Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_MoveType");
    }

    private static void SetPlayerWeaponVisible(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;
        
        var playerPawnValue = player.PlayerPawn.Value;
        
        var activeWeapon = playerPawnValue!.WeaponServices?.ActiveWeapon.Value;
        if (activeWeapon != null && activeWeapon.IsValid)
        {
            activeWeapon.Render = Color.FromArgb(255, 255, 255, 255);
            activeWeapon.ShadowStrength = 1.0f;
            Utilities.SetStateChanged(activeWeapon, "CBaseModelEntity", "m_clrRender");
        }

        var myWeapons = playerPawnValue.WeaponServices?.MyWeapons;
        if (myWeapons != null)
        {
            foreach (var gun in myWeapons)
            {
                var weapon = gun.Value;
                if (weapon != null)
                {
                    weapon.Render = Color.FromArgb(255, 255, 255, 255);
                    weapon.ShadowStrength = 1.0f;
                    Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }

    private static void SetPlayerWeaponInvisible(CCSPlayerController player)
    {
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;
        
        var playerPawnValue = player.PlayerPawn.Value;

        var activeWeapon = playerPawnValue!.WeaponServices?.ActiveWeapon.Value;
        if (activeWeapon != null && activeWeapon.IsValid)
        {
            activeWeapon.Render = Color.FromArgb(0, 255, 255, 255);
            activeWeapon.ShadowStrength = 0.0f;
            Utilities.SetStateChanged(activeWeapon, "CBaseModelEntity", "m_clrRender");
        }

        var myWeapons = playerPawnValue.WeaponServices?.MyWeapons;
        if (myWeapons != null)
        {
            foreach (var gun in myWeapons)
            {
                var weapon = gun.Value;
                if (weapon != null)
                {
                    weapon.Render = Color.FromArgb(0, 255, 255, 255);
                    weapon.ShadowStrength = 0.0f;
                    Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }

    private void RefreshPlayerGloves(CCSPlayerController player, bool update = false)
    {
        if(!Config.EmoteGlovesFix)
            return;
        
        if(!player.IsValidPlayer() || !player.PlayerPawn.IsValidPawnAlive())
            return;

        var playerPawnValue = player.PlayerPawn.Value;
        if(playerPawnValue == null)
            return;

        var model = playerPawnValue.CBodyComponent?.SceneNode?.GetSkeletonInstance()?.ModelState.ModelName ?? string.Empty;
        if (!string.IsNullOrEmpty(model))
        {
            playerPawnValue.SetModel("characters/models/tm_jumpsuit/tm_jumpsuit_varianta.vmdl");
            playerPawnValue.SetModel(model);
        }

        if(update)
        {
            Server.NextWorldUpdate(() =>
            {
                if(playerPawnValue == null)
                    return;
                
                SetBodygroup(playerPawnValue.Handle, "default_gloves", 1);
            });
        }
    }

    public static void UpdateCamera(CDynamicProp cameraProp, CCSPlayerController target)
    {
        if(target.IsValidPlayer() && target.PlayerPawn.IsValidPawnAlive() && target.AbsOrigin != null)
        {
            Vector positionBehind = CalculatePositionInFront(target, -110, 75f); //130 90
            Vector position = Lerp(GetPosition(cameraProp), positionBehind, 0.1f);
            cameraProp.Teleport(position, target.PlayerPawn.Value!.V_angle, new Vector());

            // Vector velocity = CalculateVelocity(cameraProp.AbsOrigin!, CalculatePositionInFront(target, -110, 75), 0.01f);
            // cameraProp.Teleport(null, target.PlayerPawn.Value!.V_angle, velocity);
        }
    }

    public static void UpdateAnimProp(CDynamicProp animProp, CCSPlayerController target)
    {
        if(target.IsValidPlayer() && target.PlayerPawn.IsValidPawnAlive() && target.AbsOrigin != null)
        {
            animProp.Teleport(target.PlayerPawn.Value!.AbsOrigin, target.PlayerPawn.Value.AbsRotation, new Vector());
        }
    }

    public static Vector CalculatePositionInFront(CCSPlayerController player, float offSetXY, float offSetZ = 0)
    {
        var pawn = player.PlayerPawn.Value;
        // Extract yaw angle from player's rotation QAngle
        float yawAngle = pawn!.EyeAngles!.Y;

        // Convert yaw angle from degrees to radians
        float yawAngleRadians = (float)(yawAngle * Math.PI / 180.0);

        // Calculate offsets in x and y directions
        float offsetX = offSetXY * (float)Math.Cos(yawAngleRadians);
        float offsetY = offSetXY * (float)Math.Sin(yawAngleRadians);

        // Calculate position in front of the player
        var positionInFront = new Vector
        {
            X = pawn!.AbsOrigin!.X + offsetX,
            Y = pawn!.AbsOrigin!.Y + offsetY,
            Z = pawn!.AbsOrigin!.Z + offSetZ
        };

        return positionInFront;
    }

    public static Vector GetPosition(CDynamicProp prop)
    {
        Vector position = new Vector
        {
            X = prop.AbsOrigin!.X,
            Y = prop.AbsOrigin.Y,
            Z = prop.AbsOrigin.Z
        };

        return position;
    }

    public static Vector Lerp(Vector from, Vector to, float t)
    {
        Vector vector = new Vector
        {
            X = from.X + (to.X - from.X) * t,
            Y = from.Y + (to.Y - from.Y) * t,
            Z = from.Z + (to.Z - from.Z) * t
        };

        return vector;
    }

    /*public static Vector CalculateVelocity(Vector positionA, Vector positionB, float timeDuration)
    {
        // Step 1: Determine direction from A to B
        Vector directionVector = positionB - positionA;

        // Step 2: Calculate distance between A and B
        float distance = directionVector.Length();

        // Step 3: Choose a desired time duration for the movement
        // Ensure that timeDuration is not zero to avoid division by zero
        if (timeDuration == 0)
        {
            timeDuration = 1;
        }

        // Step 4: Calculate velocity magnitude based on distance and time
        float velocityMagnitude = distance / timeDuration;

        // Step 5: Normalize direction vector
        if (distance != 0)
        {
            directionVector /= distance;
        }

        // Step 6: Scale direction vector by velocity magnitude to get velocity vector
        Vector velocityVector = directionVector * velocityMagnitude;

        return velocityVector;
    }

    public static Vector CalculatePositionBehind(CCSPlayerController player, float offSetXY, float offSetZ = 0.0f)
    {
        CCSPlayerPawn ccsPlayerPawn = player.PlayerPawn.Value!;

        float num1 = (float)((double)ccsPlayerPawn.EyeAngles.Y * Math.PI / 180.0);
        float num2 = -offSetXY * (float)Math.Cos((double)num1);
        float num3 = -offSetXY * (float)Math.Sin((double)num1);

        Vector positionBehind = new Vector
        {
            X = ccsPlayerPawn.AbsOrigin!.X + num2,
            Y = ccsPlayerPawn.AbsOrigin.Y + num3,
            Z = ccsPlayerPawn.AbsOrigin.Z + offSetZ
        };

        return positionBehind;
    }*/

    private void EmitSound(CCSPlayerController player, string sound, float volume = 1f, float pitch = 1f)
	{
        if(g_PlayerSettings.ContainsKey(player.SteamID))
        {
            if(!string.IsNullOrEmpty(g_PlayerSettings[player.SteamID].CurrentSound))
            {
                player.StopSound(g_PlayerSettings[player.SteamID].CurrentSound);
            }
            g_PlayerSettings[player.SteamID].CurrentSound = sound;
        }
        
        Dictionary<string, float> parameters = new Dictionary<string, float>
		{
			{ "volume", volume },
			{ "pitch", pitch }
		};
        player.PlayerPawn.Value?.EmitSound(sound, parameters);
	}

    private void DebugLogs(string message)
    {
        if(Config.DebugLogs)
            Logger.LogInformation(message);
    }
}

internal static class CCSPlayerControllerEx
{
	internal static bool IsValidPlayer(this CCSPlayerController? controller)
	{
		return controller != null
        && controller.Entity != null
        && controller.Entity.Handle != IntPtr.Zero
        && controller.IsValid
        && controller.Connected == PlayerConnectedState.PlayerConnected
        && !controller.IsHLTV
        && !controller.IsBot;
	}
}

internal static class CHandleCCSPlayerPawnEx
{
	internal static bool IsValidPawn(this CHandle<CCSPlayerPawn>? pawn)
	{
		return pawn != null
        && pawn.IsValid
        && pawn.Value != null
        && pawn.Value.IsValid
        && pawn.Value.WeaponServices != null
        && pawn.Value.WeaponServices.MyWeapons != null;
	}

    internal static bool IsValidPawnAlive(this CHandle<CCSPlayerPawn>? pawn)
    {
        return IsValidPawn(pawn) && pawn!.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE && pawn.Value.Health > 0;
    }
}

internal static class CHandleCBasePlayerPawnEx
{
	internal static bool IsValidPawn(this CHandle<CBasePlayerPawn>? pawn)
	{
		return pawn != null
        && pawn.IsValid
        && pawn.Value != null
        && pawn.Value.IsValid;
	}

    internal static bool IsValidPawnAlive(this CHandle<CBasePlayerPawn>? pawn)
    {
        return IsValidPawn(pawn) && pawn!.Value!.LifeState == (byte)LifeState_t.LIFE_ALIVE && pawn.Value.Health > 0;
    }
}

internal static class CHandleCBasePlayerWeaponEx
{
    internal static bool IsValidWeapon(this CHandle<CBasePlayerWeapon>? weapon)
    {
        return weapon != null
        && weapon.IsValid
        && weapon.Value != null
        && weapon.Value.IsValid
        && weapon.Value.Entity != null
        && !string.IsNullOrEmpty(weapon.Value.DesignerName);
    }
}

public static class EmitSoundExtension
{
    // Search string with "DeathCry" and the function using this argument is EmitSoundParams.
    private static MemoryFunctionVoid<CBaseEntity, string, int, float, float> CBaseEntity_EmitSoundParamsFunc = new(GameData.GetSignature("CBaseEntity_EmitSoundParams"));
    private static MemoryFunctionWithReturn<nint, nint, uint, uint, short, ulong, ulong> CSoundOpGameSystem_StartSoundEventFunc = new(GameData.GetSignature("CSoundOpGameSystem_StartSoundEvent"));
    private static MemoryFunctionVoid<nint, nint, ulong, nint, nint, short, byte> CSoundOpGameSystem_SetSoundEventParamFunc = new(GameData.GetSignature("CSoundOpGameSystem_SetSoundEventParam"));
    private static MemoryFunctionVoid<nint, string> StopSoundEvent = new(GameData.GetSignature("StopSoundEvent"));

    internal static void Init()
    {
        CSoundOpGameSystem_StartSoundEventFunc.Hook(CSoundOpGameSystem_StartSoundEventFunc_PostHook, HookMode.Post);
    }

    internal static void CleanUp()
    {
        CSoundOpGameSystem_StartSoundEventFunc.Unhook(CSoundOpGameSystem_StartSoundEventFunc_PostHook, HookMode.Post);
    }

    [ThreadStatic]
    private static IReadOnlyDictionary<string, float>? CurrentParameters;

    /// <summary>
    /// Emit a sound event by name (e.g., "Weapon_AK47.Single").
    /// TODO: parameters passed in here only seem to work for sound events shipped with the game, not workshop ones.
    /// </summary>
    public static void EmitSound(this CBaseEntity entity, string soundName, IReadOnlyDictionary<string, float>? parameters = null)
    {
        if (!entity.IsValid)
        {
            throw new ArgumentException( "Entity is not valid." );
        }

        try
        {
            // We call CBaseEntity::EmitSoundParams,
            // which calls a method that returns an ID that we can use
            // to modify the playing sound.

            CurrentParameters = parameters;

            // Pitch, volume etc aren't actually used here
            CBaseEntity_EmitSoundParamsFunc.Invoke(entity, soundName, 100, 1f, 0f);
        }
        finally
        {
            CurrentParameters = null;
        }
    }

    public static void StopSound(this CBaseEntity entity, string soundName)
    {
        if (entity == null || !entity.IsValid || entity.Entity == null)
        {
            throw new ArgumentException("Entity is not valid.");
        }
        
        StopSoundEvent.Invoke(entity.Entity.Handle, soundName);
    }

    private static HookResult CSoundOpGameSystem_StartSoundEventFunc_PostHook(DynamicHook hook)
    {
        if (CurrentParameters is not { Count: > 0 })
        {
            return HookResult.Continue;
        }

        var pSoundOpGameSystem = hook.GetParam<nint>(0);
        var pFilter = hook.GetParam<nint>(1);
        var soundEventId = hook.GetReturn<ulong>();

        foreach (var parameter in CurrentParameters)
        {
            CSoundOpGameSystem_SetSoundEventParam(pSoundOpGameSystem, pFilter,
                soundEventId, parameter.Key, parameter.Value);
        }

        return HookResult.Continue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FloatParamData
    {
        // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
        private readonly uint _type1;
        private readonly uint _type2;

        private readonly uint _size1;
        private readonly uint _size2;

        private readonly float _value;
        private readonly uint _padding;
        // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable

        public FloatParamData(float value)
        {
            _type1 = 1;
            _type2 = 8;

            _size1 = 4;
            _size2 = 4;

            _value = value;
            _padding = 0;
        }
    }

    private static unsafe void CSoundOpGameSystem_SetSoundEventParam(nint pSoundOpGameSystem, nint pFilter,
        ulong soundEventId, string paramName, float value)
    {
        var data = new FloatParamData(value);
        var nameByteCount = Encoding.UTF8.GetByteCount(paramName);

        var pData = Unsafe.AsPointer(ref data);
        var pName = stackalloc byte[nameByteCount + 1];

        Encoding.UTF8.GetBytes( paramName, new Span<byte>(pName, nameByteCount ));

        CSoundOpGameSystem_SetSoundEventParamFunc.Invoke(pSoundOpGameSystem, pFilter, soundEventId, (nint)pName, (nint)pData, 0, 0);
    }
}
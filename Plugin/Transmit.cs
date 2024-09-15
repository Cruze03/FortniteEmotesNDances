using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace FortniteEmotes;
public partial class Plugin
{
    /*
        * Transmit Manager
        * Credits of this whole transmit manager goes to xstage's HidePlayers plugin
    */
    
    #region CCheckTransmitInfo
    [StructLayout(LayoutKind.Sequential)]
    public struct CCheckTransmitInfo
    {
        public CFixedBitVecBase m_pTransmitEntity;
    };

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CFixedBitVecBase
    {
        private const int LOG2_BITS_PER_INT = 5;
        private const int MAX_EDICT_BITS = 14;
        private const int BITS_PER_INT = 32;
        private const int MAX_EDICTS = 1 << MAX_EDICT_BITS;

        private uint* m_Ints;

        public void Clear(int bitNum)
        {
            if (!(bitNum >= 0 && bitNum < MAX_EDICTS))
                return;

            uint* pInt = m_Ints + BitVec_Int(bitNum);
            *pInt &= ~(uint)BitVec_Bit(bitNum);
        }

        public bool IsBitSet(int bitNum)
        {
            if (!(bitNum >= 0 && bitNum < MAX_EDICTS))
                return false;

            uint* pInt = m_Ints + BitVec_Int(bitNum);
            return  ( *pInt & BitVec_Bit( bitNum ) ) != 0 ;
        }

        private int BitVec_Int(int bitNum) => bitNum >> LOG2_BITS_PER_INT;
        private int BitVec_Bit(int bitNum) => 1 << ((bitNum) & (BITS_PER_INT - 1));
    }
    #endregion
    
    private readonly CSPlayerState[] _oldPlayerState = new CSPlayerState[65];
    private readonly INetworkServerService networkServerService = new();
    private static readonly MemoryFunctionVoid<nint, nint, int, nint, int, short, int, bool> CheckTransmit = new(GameData.GetSignature("CheckTransmit"));
    private static readonly MemoryFunctionVoid<nint, CSPlayerState> StateTransition = new(GameData.GetSignature("StateTransition"));


    public void Transmit_OnLoad()
    {
        if(Config.EmoteHidePlayers != 0)
        {
            StateTransition.Hook(Hook_StateTransition, HookMode.Post);
            CheckTransmit.Hook(Hook_CheckTransmit, HookMode.Post);
        }
    }

    public void Transmit_OnUnload()
    {
        if(Config.EmoteHidePlayers != 0)
        {
            StateTransition.Unhook(Hook_StateTransition, HookMode.Post);
            CheckTransmit.Unhook(Hook_CheckTransmit, HookMode.Post);
        }
    }

    private void ForceFullUpdate(CCSPlayerController? player)
    {
        if (player is null || !player.IsValid) return;

        var networkGameServer = networkServerService.GetIGameServer();
        networkGameServer.GetClientBySlot(player.Slot)?.ForceFullUpdate();

        player.PlayerPawn.Value?.Teleport(null, player.PlayerPawn.Value.EyeAngles, null);
    }

    private unsafe HookResult Hook_CheckTransmit(DynamicHook hook)
    {
        nint* ppInfoList = (nint*)hook.GetParam<nint>(1);
        int infoCount = hook.GetParam<int>(2);

        for (int i = 0; i < infoCount; i++)
        {
            nint pInfo = ppInfoList[i];
            byte slot = *(byte*)(pInfo + GameData.GetOffset("CheckTransmitPlayerSlot"));

            var player = Utilities.GetPlayerFromSlot(slot);
            var info = Marshal.PtrToStructure<CCheckTransmitInfo>(pInfo);

            if (!player.IsValidPlayer() || !player!.PlayerPawn.IsValidPawnAlive())
                continue;
            
            var steamID = player.SteamID;

            foreach (var target in Utilities.GetPlayers()
                .Where(p => p != null && p.PlayerPawn.Value != null))
            {
                var pawn = target.PlayerPawn.Value!;

                #region fix client crash
                if (target.Slot == slot && ((LifeState_t)pawn.LifeState != LifeState_t.LIFE_DEAD || pawn.PlayerState.HasFlag(CSPlayerState.STATE_DEATH_ANIM)))
                    continue;

                if (player.PlayerPawn.Value!.PlayerState.HasFlag(CSPlayerState.STATE_DORMANT) && target.Slot != slot)
                    continue;

                if ((LifeState_t)pawn.LifeState != LifeState_t.LIFE_ALIVE)
                {
                    info.m_pTransmitEntity.Clear((int)pawn.Index);
                    continue;
                }
                #endregion

                if(!g_PlayerSettings.ContainsKey(steamID))
                    continue;
                
                if(!g_PlayerSettings[steamID].IsDancing)
                    continue;

                switch(Config.EmoteHidePlayers)
                {
                    case 1:
                        if(player.Team != target.Team)
                            info.m_pTransmitEntity.Clear((int)pawn.Index);
                        break;
                    case 2:
                        if(player.Team == target.Team)
                            info.m_pTransmitEntity.Clear((int)pawn.Index);
                        break;
                    case 3:
                        info.m_pTransmitEntity.Clear((int)pawn.Index);
                        break;
                }
            }
        }

        return HookResult.Continue;
    }

    private HookResult Hook_StateTransition(DynamicHook hook)
    {
        var pawn = new CCSPlayerPawn(hook.GetParam<nint>(0));

        if (!pawn.IsValid) return HookResult.Continue;

        var player = pawn.OriginalController.Value;
        var state = hook.GetParam<CSPlayerState>(1);

        if (player is null) return HookResult.Continue;

        if (_oldPlayerState[player.Index] != CSPlayerState.STATE_OBSERVER_MODE && state == CSPlayerState.STATE_OBSERVER_MODE ||
            _oldPlayerState[player.Index] == CSPlayerState.STATE_OBSERVER_MODE && state != CSPlayerState.STATE_OBSERVER_MODE)
        {
            ForceFullUpdate(player);
        }

        _oldPlayerState[player.Index] = state;

        return HookResult.Continue;
    }
}

[StructLayout(LayoutKind.Sequential)]
struct CUtlMemory
{
    public unsafe nint* m_pMemory;
    public int m_nAllocationCount;
    public int m_nGrowSize;
}

[StructLayout(LayoutKind.Sequential)]
struct CUtlVector
{
    public unsafe nint this[int index]
    {
        get => this.m_Memory.m_pMemory[index];
        set => this.m_Memory.m_pMemory[index] = value;
    }

    public int m_iSize;
    public CUtlMemory m_Memory;
    
    public nint Element(int index) => this[index];
}

// thx https://discord.com/channels/1160907911501991946/1175947333880524962/1231712355784851497

class INetworkServerService : NativeObject
{
    private readonly VirtualFunctionWithReturn<nint, nint> GetIGameServerFunc;

    public INetworkServerService() : base(NativeAPI.GetValveInterface(0, "NetworkServerService_001"))
    {
        this.GetIGameServerFunc = new VirtualFunctionWithReturn<nint, nint>(this.Handle, GameData.GetOffset("INetworkServerService_GetIGameServer"));
    }

    public INetworkGameServer GetIGameServer()
    {
        return new INetworkGameServer(this.GetIGameServerFunc.Invoke(this.Handle));
    }
}

public class INetworkGameServer : NativeObject
{
    private static int SlotsOffset = GameData.GetOffset("INetworkGameServer_Slots");

    private CUtlVector Slots;

    public INetworkGameServer(nint ptr) : base(ptr)
    {
        this.Slots = Marshal.PtrToStructure<CUtlVector>(base.Handle + SlotsOffset);
    }

    public CServerSideClient? GetClientBySlot(int playerSlot)
    {
        if (playerSlot >= 0 && playerSlot < this.Slots.m_iSize)
            return this.Slots[playerSlot] == IntPtr.Zero ? null : new CServerSideClient(this.Slots[playerSlot]);
        
        return null;
    }
}

public class CServerSideClient : NativeObject
{
    private static int m_nForceWaitForTick = GameData.GetOffset("CServerSideClient_m_nForceWaitForTick");

    public unsafe int ForceWaitForTick
    {
        get { return *(int*)(base.Handle + m_nForceWaitForTick); }
        set { *(int*)(base.Handle + m_nForceWaitForTick) = value; }
    }

    public CServerSideClient(nint ptr) : base(ptr)
        { }

    public void ForceFullUpdate()
    {
        this.ForceWaitForTick = -1;
    }
}
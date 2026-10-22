using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public interface ISimbyConstructionHost
{
    ActorBodyMotion Body { get; }
    ushort ItemFlags { get; set; }
    void InitializeModel(string name);
    void AttachToSourceBaddyList();
    void ReadPosePacket();
    void ParseAuthoredScript();
    int NextRandom(int bound);
    /// <summary>MakeVertexWibbler (0x4A2790) on the first live CSimby.</summary>
    void EnsureVertexWibbler();
}

/// <summary>Authored CSimby(i32*, i32) at 0x4A7870 after SquirtPos/SquirtAngles have placed the body.</summary>
public static class SimbyConstruction
{
    public static void Construct(SimbyState state, SimbyWorldState world, ISimbyConstructionHost host, int attackClock)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(host);
        host.InitializeModel(state.ModelName);
        host.AttachToSourceBaddyList();
        state.CombatFlags |= 0x201;
        state.SurfaceClearance = 100;
        state.MinorRadius = 140;
        state.WaitUpdates = 0;
        state.GeneralParameter = 32;
        state.PushValue = 64;
        state.Stage = 0;
        state.Substate = 0;
        state.Health = 800;
        state.SetupLow = SimbyState.Setup[0];
        state.SetupHigh = SimbyState.Setup[1];
        state.AttackRelatedSnapshot = unchecked(attackClock - 155);
        state.AlertNoiseTimer = host.NextRandom(300);
        state.Field3B8 = 0xDAC;
        state.Field3BC = 0x190;
        state.Field3C0 = 0x555;
        state.Field30C = 0x64;
        host.ReadPosePacket();
        host.ParseAuthoredScript();
        // Rnd runs before ReadLinks in the original; kept above.
        if ((state.BehaviorFlags & 0x100000) != 0)
        {
            state.Field3A0 |= 0x10;
            host.ItemFlags |= 1;
        }
        if (state.LevelId == SimbyState.SpecialBehaviorLevel)
            state.BehaviorFlags |= 0x400000;
        if (world.Count == 0) host.EnsureVertexWibbler();
        world.Count++;
        if (attackClock < 60) world.CountResetFlag = 0;
    }
}

/// <summary>CSimby::Flash (0x4AF260) and FlashUpdate (0x4AF3B0).</summary>
public static class SimbyFlash
{
    public static void Flash(SimbyState state, ref ushort itemFlags, int frames, byte r, byte g, byte b)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (frames == 0)
        {
            state.FlashFrames = 0;
            itemFlags |= 0x400;
            state.Rgb = r | (g << 8) | (b << 16);
            return;
        }
        if (frames < 0)
        {
            state.Rgb = 0x202020;
            int steps = unchecked((ushort)-frames);
            state.FlashFrames = unchecked((short)-frames);
            state.FlashDeltaR = unchecked((ushort)((r - 32) / steps));
            state.FlashDeltaG = unchecked((ushort)((g - 32) / steps));
            state.FlashDeltaB = unchecked((short)((b - 32) / steps));
            itemFlags |= 0x400;
            return;
        }
        state.FlashFrames = unchecked((short)(frames + 1));
        state.FlashDeltaR = unchecked((ushort)((32 - r) / frames));
        state.FlashDeltaG = unchecked((ushort)((32 - g) / frames));
        state.FlashDeltaB = unchecked((short)((32 - b) / frames));
        if ((state.FlashRestore & 0x1000000) == 0)
        {
            state.FlashRestore = (state.Rgb & 0xFFFFFF) | 0x1000000;
            if ((itemFlags & 0x400) != 0) state.FlashRestore |= 0x2000000;
        }
        itemFlags |= 0x400;
        state.Rgb = r | (g << 8) | (b << 16);
    }

    public static void Update(SimbyState state, ref ushort itemFlags)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FlashFrames == 0) return;
        itemFlags |= 0x400;
        int rgb = state.Rgb;
        state.Rgb = (rgb + state.FlashDeltaR) & 0xFF
            | ((((rgb >> 8) + state.FlashDeltaG) & 0xFF) << 8)
            | ((((rgb >> 16) + state.FlashDeltaB) & 0xFF) << 16);
        if (--state.FlashFrames != 0) return;
        if ((state.FlashRestore & 0x2000000) != 0) itemFlags |= 0x400;
        else itemFlags = unchecked((ushort)(itemFlags & ~0x400));
        state.Rgb = state.FlashRestore;
        state.FlashRestore = 0;
    }
}

/// <summary>CSimby::ExplosionReaction (0x4A7E70) and ClearAttackData (inlined into AI at 0x4AE6D7).</summary>
public static class SimbyReactions
{
    public static void ExplosionReaction(SimbyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.BehaviorFlags |= 0x8000;
    }

    public static void ClearAttackData(SimbyState state, SimbyWorldState world)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(world);
        world.AttackData &= ~state.AttackMask;
        state.AttackMask = 0;
    }

    /// <summary>CSimby::SetAlertModeTimer raises field_348 only.</summary>
    public static void SetAlertModeTimer(SimbyState state, int timer)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.AlertModeTimer < timer) state.AlertModeTimer = timer;
    }
}

namespace OpenSpidey.Core.Player;

/// <summary>Source lookaround angle globals and input/transition fields; activation shares the player's actual field8EA.</summary>
public sealed class PlayerLookaroundState(PlayerCombatState combat)
{
    public bool Active { get => combat.LookaroundActive; set => combat.LookaroundActive = value; }
    public byte Allowed { get; set; } = 1;
    public byte JumpReleased { get; set; }
    public byte SwingHeldLatch { get; set; }
    public byte ActivateTarget { get; set; }
    public int HoldTicks { get; set; }
    public int EntranceTicks { get; set; }
    public int ExitTicks { get; set; }
    public int TransitionFade { get; set; }
    public int HeadingTurnTicksRemaining { get => combat.HeadingTurnTicksRemaining; set => combat.HeadingTurnTicksRemaining = value; }
    public int FloorPitch { get; set; } = 170;
    public int WallPitch { get; set; } = 170;
    public int CeilingPitch { get; set; } = 170;
    public int Pitch { get; set; }
    public int Yaw { get; set; }
    public int SmoothedPitch { get; set; }
    public int SmoothedYaw { get; set; }
    public short PreviousHeading { get; set; }
    public short NeckPitch { get; set; }
    public short NeckYaw { get; set; }
    public short HeadPitch { get; set; }
    public short HeadYaw { get; set; }

    /// <summary>Original4B9740: signed initial pitch for floor0/wall1/ceiling2; parameter0 is the only supported source write.</summary>
    public void SetInitialCameraPitch(ushort surface, ushort parameter, short pitch)
    {
        if (parameter != 0) return; // The shipped source assertion is a bare return and leaves the globals unchanged.
        switch (surface)
        {
            case 0: FloorPitch = pitch; break;
            case 1: WallPitch = pitch; break;
            case 2: CeilingPitch = pitch; break;
        }
    }

    /// <summary>The scalar part of EnterLookaroundMode; the camera owner retains its quaternion path/mode stack.</summary>
    public bool InitializeAnglesOnEnter(short heading, bool wallProfile, bool ceilingProfile)
    {
        if (ExitTicks != 0) return false;
        PreviousHeading = heading; Active = true; EntranceTicks = 24; ExitTicks = 0; HeadingTurnTicksRemaining = 0;
        Pitch = wallProfile ? WallPitch : ceilingProfile ? CeilingPitch : FloorPitch;
        return true;
    }
    public void ClearAnglesOnExit()
    {
        if (!Active) return;
        Active = false; EntranceTicks = ExitTicks = HoldTicks = TransitionFade = 0;
        NeckPitch = NeckYaw = HeadPitch = HeadYaw = 0;
    }
}

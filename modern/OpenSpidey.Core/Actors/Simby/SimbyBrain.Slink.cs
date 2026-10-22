using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Simby;

public sealed partial class SimbyBrain
{
    private const byte RibbonRed = 106, RibbonGreen = 17, RibbonBlue = 152;

    /// <summary>CSimby::SlinkToTarget (0x4AABF0): shrink into a sym_base, ribbon across, regrow at TargetPosition.</summary>
    public void SlinkToTarget()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.CycleAnimation(unchecked((byte)state.SetupHigh));
                host.AttachLookAtYaw(state.AimYaw, 2, 80, 20);
                state.Substate++;
                break;
            case 1:
                if ((host.Actor.CompletionFlags & 2) == 0) break;
                host.Actor.CompletionFlags &= ~2u;
                BeginSlink();
                state.Substate++;
                break;
            case 2:
                state.SlinkScaleTimer = unchecked(state.SlinkScaleTimer - host.ElapsedTicks);
                if (state.SlinkScaleTimer < 0) state.SlinkScaleTimer = 0;
                ShrinkForSlink();
                if (state.SlinkScaleTimer > 0) break;
                if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, 6000);
                state.BehaviorFlags |= 0x40000;
                state.Ribbon = host.CreateRibbon(6, RibbonRed, RibbonGreen, RibbonBlue, 10);
                ReleaseSlime();
                host.PlayPositionalSound(33122);
                state.Substate++;
                ExtendSlink();
                break;
            case 3:
                ExtendSlink();
                break;
            case 4:
                RetractSlink();
                break;
            case 5:
                if (!ScaleUpFromSlink()) break;
                state.Stage = state.Stage == 24 ? 2 : 4;
                state.Substate = 0;
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby SlinkToTarget substate {state.Substate}.");
        }
    }

    private void BeginSlink()
    {
        FixedVector3 position = host.Body.Position;
        state.StretchStart = position;
        state.ShadowGroundY = unchecked(position.Y + (state.SurfaceClearance << 12));
        state.SlinkDelta = state.TargetPosition - position;
        state.SlinkLength = OriginalLength(state.SlinkDelta);
        state.SlinkDelta >>= 12;
        int spans = state.SlinkLength / 35;
        if (spans == 0) spans = 5;
        state.SlinkRate = 4096 / spans;
        ClearJointTranslations();
        Array.Clear(state.SlinkSegments, 0, 7);
        host.ItemFlags |= 0x200;
        state.Scale = 4096;
        state.SlinkProgress = 0;
        state.BehaviorFlags |= 0x10000;
        state.SlinkScaleTimer = 30;
        if (state.SymBase != 0) return;
        state.SymBase = SpawnSymBase(position);
        state.BehaviorFlags |= 0x20000;
        host.StartSymBaseFollowing(state.SymBase, 4096);
    }

    private void ShrinkForSlink()
    {
        short scale = unchecked((short)(136 * state.SlinkScaleTimer));
        state.Scale = scale;
        short clearance = unchecked((short)((100 * scale) >> 12));
        state.SurfaceClearance = clearance;
        int y = unchecked(state.ShadowGroundY - scale * clearance);
        state.StretchStart = state.StretchStart with { Y = y };
        host.Body.Position = host.Body.Position with { Y = y };
        if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, ((1904 * (4096 - scale)) >> 12) + 4096);
    }

    private void ExtendSlink()
    {
        state.Field3A0 |= 0x10;
        bool active = false;
        int carry = 0;
        for (int i = 0; i < 7; i++)
        {
            if (state.SlinkSegments[i] >= 585) continue;
            active = true;
            int step = carry != 0 ? carry : unchecked(host.ElapsedTicks * state.SlinkRate);
            carry = 0;
            int filled = unchecked(state.SlinkSegments[i] + step);
            state.SlinkSegments[i] = filled;
            if (filled <= 585)
            {
                if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, (6000 * (4681 - 585 * i - filled)) >> 12);
                break;
            }
            carry = filled - 585;
            state.SlinkSegments[i] = 585;
        }
        DrawSlinky();
        if (active) return;
        state.Substate++;
        if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, 0);
        state.SymBase = SpawnSymBase(state.TargetPosition);
        host.StartSymBaseFollowing(state.SymBase, 100);
        host.SetSymBaseItemScale(state.SymBase, 100);
        state.SurfaceClearance = 0;
        state.SlinkScaleTimer = 0;
        host.Body.Position = state.TargetPosition;
        state.ShadowGroundY = state.TargetPosition.Y;
    }

    private void RetractSlink()
    {
        state.Field3A0 |= 0x10;
        bool active = false;
        int carry = 0;
        for (int i = 6; i >= 0; i--)
        {
            int before = state.SlinkSegments[i];
            if (before <= 0) continue;
            active = true;
            if (carry == 0) carry = unchecked(host.ElapsedTicks * state.SlinkRate);
            int after = unchecked(state.SlinkSegments[i] - carry);
            carry = 0;
            state.SlinkSegments[i] = after;
            if (after >= 0)
            {
                state.SlinkProgress = unchecked(state.SlinkProgress + before - after);
                break;
            }
            carry = -after;
            state.SlinkSegments[i] = 0;
            state.SlinkProgress = unchecked(state.SlinkProgress + before);
        }
        if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, (6000 * state.SlinkProgress) >> 12);
        DrawSlinky();
        if (active) return;
        host.RunAnimation(0);
        if (state.SlimeHandle == 0)
        {
            host.CreateSlimeQuad(state.TargetPosition, 0, true);
            state.SlimeHandle = 1;
        }
        state.BehaviorFlags &= ~0x40000u;
        if (state.Ribbon != 0) host.DeleteRibbon(state.Ribbon);
        state.Ribbon = 0;
        state.Substate++;
    }

    /// <summary>sub_4AA9B0 (DrawTheSlinkySomBitch): seven ribbon points on a sine arc from StretchStart to TargetPosition.</summary>
    private void DrawSlinky()
    {
        int progress = state.SlinkProgress, previous = state.SlinkSegments[6];
        FixedVector3 start = state.StretchStart, end = state.TargetPosition;
        for (int i = 6; i >= 0; i--)
        {
            progress = unchecked(progress + state.SlinkSegments[i]);
            int z = unchecked(progress * ((end.Z - start.Z) >> 12) + start.Z);
            int y = unchecked(progress * ((end.Y - start.Y) >> 12) + start.Y
                - state.JumpHeight * GameAngle.Heading(progress >> 1).Sine);
            if (state.Ribbon == 0) continue;
            int x = unchecked(progress * ((end.X - start.X) >> 12) + start.X);
            host.SetRibbonPoint(state.Ribbon, i, new FixedVector3(x, y, z));
            host.SetRibbonWidth(state.Ribbon, i, 30 - ((175 * ((previous + state.SlinkSegments[i]) >> 1)) >> 12));
            previous = state.SlinkSegments[i];
        }
    }

    /// <summary>sub_4AAAE0 (ScaleUpFromSlinkPlease); true once the 30-frame regrow is complete.</summary>
    private bool ScaleUpFromSlink()
    {
        int timer = unchecked(host.ElapsedTicks + state.SlinkScaleTimer);
        state.SlinkScaleTimer = timer;
        if (timer <= 30)
        {
            int scale = 136 * timer;
            state.Scale = unchecked((short)scale);
            short clearance = unchecked((short)((13600 * timer) >> 12));
            state.SurfaceClearance = clearance;
            host.Body.Position = host.Body.Position with { Y = unchecked(state.ShadowGroundY - scale * clearance) };
            host.SetHeight(false, 100, 600);
            if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, ((1904 * scale) >> 12) + 4096);
            return false;
        }
        host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~0x200));
        state.SurfaceClearance = 100;
        state.BehaviorFlags &= ~0x20000u;
        state.SlinkScaleTimer = 30;
        if (state.SymBase != 0) host.DeleteSymBase(state.SymBase);
        state.SymBase = 0;
        state.Scale = 4096;
        state.BehaviorFlags &= ~0x10000u;
        return true;
    }

    /// <summary>CSimby::AI 0x70000 cleanup for leaves that left a slink half-done.</summary>
    private void CleanUpSlink()
    {
        if ((state.BehaviorFlags & 0x10000) != 0) ScaleUpFromSlink();
        if ((state.BehaviorFlags & 0x20000) != 0)
        {
            if (state.SymBase != 0)
            {
                host.ReleaseSymBase(state.SymBase);
                state.SymBase = 0;
            }
            state.BehaviorFlags &= ~0x20000u;
        }
        if ((state.BehaviorFlags & 0x40000) != 0)
        {
            if (state.Ribbon != 0)
            {
                host.DeleteRibbon(state.Ribbon);
                state.Ribbon = 0;
            }
            state.BehaviorFlags &= ~0x40000u;
        }
    }

    /// <summary>CSimby::ClimbToTarget (0x4AB570): ribbon up to ClimbTarget and hang inverted from the ceiling.</summary>
    public void ClimbToTarget()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.StretchStart = host.Body.Position;
                state.StretchStart = state.StretchStart with { Y = unchecked(state.StretchStart.Y + (state.SurfaceClearance << 12)) };
                host.ItemFlags |= 0x200;
                state.Scale = 4096;
                host.CycleAnimation(unchecked((byte)state.SetupHigh));
                if (state.SymBase == 0)
                {
                    state.SymBase = SpawnSymBase(host.Body.Position);
                    host.AttachStateSwitchMessage(17);
                    host.StartSymBaseFollowing(state.SymBase, 4096);
                }
                state.Substate++;
                break;
            case 1:
                ShrinkForClimb();
                break;
            case 2:
                StretchUp();
                break;
            case 3:
                PullUp();
                break;
            case 4:
                GrowInverted();
                break;
            case 5:
                HangFromCeiling();
                break;
            default:
                throw new InvalidDataException($"Unknown CSimby ClimbToTarget substate {state.Substate}.");
        }
    }

    private void ShrinkForClimb()
    {
        short scale = unchecked((short)(state.Scale - 250));
        state.Scale = scale;
        host.Body.Position = host.Body.Position with { Y = unchecked(state.StretchStart.Y - scale * state.SurfaceClearance) };
        if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, ((1904 * (4096 - scale)) >> 12) + 4096);
        if (state.Scale > 250) return;
        state.Field3A0 |= 0x10;
        if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, 6000);
        state.Ribbon = host.CreateRibbon(9, RibbonRed, RibbonGreen, RibbonBlue, 30);
        for (int i = 0; i < 10; i++) host.SetRibbonPoint(state.Ribbon, i, host.Body.Position);
        state.SlinkSegments[0] = 900;
        state.Field230 = 0;
        Array.Clear(state.SlinkSegments, 1, 9);
        state.SlinkLength = unchecked(state.StretchStart.Y - state.ClimbTarget.Y) >> 12;
        state.Substate++;
        host.PlayPositionalSound((363 - (host.Random(2) != 0 ? 1 : 0)) | 0x8000);
    }

    private void StretchUp()
    {
        bool moved = false;
        state.Field3A0 |= 0x10;
        int[] segments = state.SlinkSegments;
        for (int i = 0; i < 8; i++)
        {
            int here = segments[i], next = segments[i + 1];
            if (here <= next) continue;
            int flow;
            if (here < 100) flow = (400 * here) >> 12;
            else
            {
                int excess = here - 100;
                flow = unchecked(excess * ((40 * state.SlinkLength * ((455 * excess) >> 12)) >> 12) / (state.SlinkLength + 3));
                if (flow < 20) flow = 20;
                if (flow > excess) flow = excess;
            }
            segments[i + 1] = unchecked(flow + next);
            segments[i] = unchecked(segments[i] - flow);
            if (flow != 0) moved = true;
        }
        state.Field230 = unchecked(host.ElapsedTicks + state.Field230);
        if (state.Field230 < 20)
        {
            if (state.SymBase != 0) host.SetSymBaseTarget(state.SymBase, 6000 - ((388416 * state.Field230) >> 12));
        }
        else
        {
            state.Field230 = 20;
            if (state.ClimbSymBase == 0)
            {
                state.ClimbSymBase = SpawnSymBase(state.ClimbTarget);
                host.SetSymBaseAngleX(state.ClimbSymBase, -2048);
                host.SetSymBaseItemScale(state.ClimbSymBase, 100);
                host.StartSymBaseFollowing(state.ClimbSymBase, 1024);
            }
            if (!moved)
            {
                if (state.SymBase != 0)
                {
                    host.StopSymBaseFollowing(state.SymBase);
                    state.SymBase = 0;
                }
                ReleaseSlime();
                state.Substate++;
            }
        }
        int reach = segments[9], previous = reach;
        int lift = (204 * state.Field230 * state.SlinkLength) >> 12;
        for (int i = 8; i >= 0; i--)
        {
            if (state.Ribbon == 0) continue;
            reach = unchecked(reach + segments[i]);
            host.SetRibbonPointY(state.Ribbon, i, unchecked(host.Body.Position.Y - ((40 * lift * ((455 * reach) >> 12)) & unchecked((int)0xFFFFF000))));
            if (i == 0 && state.ClimbSymBase != 0) host.SetRibbonPointY(state.Ribbon, 0, state.ClimbTarget.Y);
            int middle = (previous + segments[i]) >> 1;
            host.SetRibbonWidth(state.Ribbon, i, middle <= 100 ? 30 - ((1000 * middle) >> 12) : 5);
            previous = segments[i];
        }
    }

    private void PullUp()
    {
        if (state.Ribbon == 0) throw new InvalidOperationException("CSimby::ClimbToTarget substate 3 dereferences its ribbon unconditionally.");
        int climbed = 0;
        for (int i = 0; i < 9; i++)
        {
            int filled = unchecked(state.SlinkSegments[i] - 6 * host.ElapsedTicks);
            state.SlinkSegments[i] = filled < 0 ? 0 : filled;
            host.SetRibbonPointY(state.Ribbon, i + 1,
                unchecked(state.ClimbTarget.Y + ((40 * state.SlinkLength * ((455 * climbed) >> 12)) & unchecked((int)0xFFFFF000))));
            climbed = unchecked(climbed + state.SlinkSegments[i]);
        }
        host.SetRibbonWidth(state.Ribbon, 8, 17);
        host.SetRibbonWidth(state.Ribbon, 9, 1);
        if (state.ClimbSymBase != 0)
            host.SetSymBaseTarget(state.ClimbSymBase, ((199040 * (100 - ((455 * climbed) >> 12))) >> 12) + 1024);
        state.Field3A0 |= 0x10;
        if (climbed != 0) return;
        host.Body.Position = host.Body.Position with { Y = state.ClimbTarget.Y };
        host.ItemFlags |= 0x200;
        state.BehaviorFlags |= 0x80;
        state.Scale = 0;
        state.SetupLow = host.CeilingSetupLow;
        state.SetupHigh = host.CeilingSetupHigh;
        if (state.SlimeHandle == 0)
        {
            host.CreateSlimeQuad(host.Body.Position, 0, true);
            state.SlimeHandle = 1;
        }
        host.RunAnimation(0x1F);
        state.ClipScaleRate = unchecked((int)(0x1000u / (ushort)host.ClipFrameCount(host.AnimationId)));
        if (state.Ribbon != 0) host.DeleteRibbon(state.Ribbon);
        state.Ribbon = 0;
        state.Substate++;
    }

    private void GrowInverted()
    {
        int scale = unchecked(state.ClipScaleRate * host.AnimationFrame);
        state.Scale = unchecked((short)scale);
        host.Body.Position = host.Body.Position with { Y = unchecked(state.ClimbTarget.Y + scale * state.SurfaceClearance) };
        if (state.ClimbSymBase != 0) host.SetSymBaseTarget(state.ClimbSymBase, ((1904 * scale) >> 12) + 4096);
        if (!host.AnimationFinished) return;
        host.Body.Position = host.Body.Position with { Y = unchecked(state.ClimbTarget.Y + (state.SurfaceClearance << 12)) };
        host.ItemFlags = unchecked((ushort)(host.ItemFlags & ~0x200));
        if (state.ClimbSymBase != 0) host.SetSymBaseTarget(state.ClimbSymBase, 0);
        state.ClimbSymBase = 0;
        state.Scale = 4096;
        host.CycleAnimation(unchecked((byte)state.SetupHigh));
        state.TargetPosition = state.TargetPosition with { Y = host.Body.Position.Y };
        host.AttachMoveTo(state.TargetPosition, 200, 80, 500, 1);
        state.Substate++;
        state.Field230 = 10;
        state.Field1F8 = 25;
        state.BehaviorFlags |= 0x80;
    }

    private void HangFromCeiling()
    {
        RunAppropriateAnim();
        int timer = state.Field1F8;
        host.RunTimer(ref timer);
        state.Field1F8 = timer;
        if (host.Random(5) == 0)
        {
            host.Random(state.MinorRadius);
            host.Random(state.MinorRadius);
            host.SpawnDrop(host.Body.Position, default, host.WebGroundY(host.Body.Position), 0);
        }
        if (state.Field1F8 == 0 && planner.DistanceToPlayer(2) < 300 && planner.DistanceToPlayer(2) > 100)
        {
            state.Stage = 8;
            state.Substate = 0;
            return;
        }
        if ((host.Actor.CompletionFlags & 1) != 0)
        {
            host.Actor.CompletionFlags &= ~1u;
            state.Stage = 8;
            state.Substate = 0;
            return;
        }
        RunField230();
        if (state.Field230 != 0) return;
        state.Field230 = 10;
        if (StillTouchingCeiling())
            host.Body.Position = host.Body.Position with { Y = unchecked(host.Body.Position.Y + ((state.CeilingY - host.Body.Position.Y + 327680) >> 3)) };
        else
        {
            state.Stage = 8;
            state.Substate = 0;
        }
    }

    /// <summary>sub_4AB490 (StillTouchingCeiling): a vertical LineToItem through MinorRadius above and below.</summary>
    private bool StillTouchingCeiling()
    {
        FixedVector3 position = host.Body.Position;
        int reach = state.MinorRadius << 12;
        if (!host.LineToItem(position with { Y = unchecked(position.Y + reach) }, position with { Y = unchecked(position.Y - reach) }, out int y))
            return false;
        state.CeilingY = y;
        return true;
    }

    /// <summary>CSimby sym_base spawn: InitItem("sym_base") on the ground within 150 units below <paramref name="position"/>.</summary>
    private int SpawnSymBase(FixedVector3 position)
    {
        int ground = host.GroundHeight(position, 0, 150, 0);
        if (ground != -1) position = position with { Y = ground };
        return host.SpawnSymBase(position, host.Body.Angles, false);
    }

    /// <summary>CVector::Length (0x4E74A0).</summary>
    private static int OriginalLength(FixedVector3 vector)
    {
        FixedVector3 whole = vector >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
    }
}

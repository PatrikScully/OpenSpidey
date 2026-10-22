using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom node-travel AI states: LookingToAttack (0x4ED790), Walkabout (0x4EDBC0) and GoToSwitch (0x4EDFF0).</summary>
public static class VenomNodeStates
{
    /// <summary>Redbook bank/track words (low/high) at 0x55A4EC, 0x55A500, 0x55A514, 0x55A528 and 0x55A53C.</summary>
    public static readonly int[] AttackSpeech = [0x00000032, 0x00010032, 0x00020032, 0x00030032];
    public static readonly int[] WalkaboutSpeech = [0x00050032, 0x00060032, 0x00070032, 0x00080032];
    public static readonly int[] SwitchSpeech = [0x000A0032, 0x000B0032, 0x000C0032];
    public static readonly int[] FlickSpeech = [0x000F0032, 0x00000033, 0x00010033];
    public static readonly int[] ArrivalSpeech = [0x00040033, 0x00050033, 0x00060033];

    public const ushort TeleportSound = 0x80F0;

    public static void LookingToAttack(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (SpotPlayer(state, host)) return;
        switch (state.Substate)
        {
            case 0:
            {
                host.RunAnimation(12);
                state.Field218 |= 0x80;
                PlaySpeech(state, host, AttackSpeech, 0);
                int node = host.LevelId == 0x502 ? RandomAttackNode(host) : NearestAttackNode(host);
                BeginLeg(state, host, node);
                break;
            }
            case 1:
                WaitForFadeIn(state);
                break;
            case 2:
                if (unchecked((uint)FixedVectorMath.OriginalDistance(host.Body.Position, state.TargetPosition)) <
                    unchecked((uint)(12 * host.ElapsedTicks)))
                    AdvanceLeg(state, host, groundTarget: true);
                break;
        }
    }

    public static void Walkabout(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (SpotPlayer(state, host)) return;
        switch (state.Substate)
        {
            case 0:
            {
                host.RunAnimation(12);
                state.Field218 |= 0x80;
                PlaySpeech(state, host, WalkaboutSpeech, 1);
                if (state.Field3B4 != 0) state.WaypointNode = unchecked((short)state.Field3B4);
                else state.WaypointNode = unchecked((short)FarthestWalkaboutNode(host));
                BeginLeg(state, host, state.WaypointNode);
                break;
            }
            case 1:
                WaitForFadeIn(state);
                break;
            case 2:
                if (unchecked((uint)CrapDistance(host.Body.Position, state.TargetPosition)) < 0x80)
                    AdvanceLeg(state, host, groundTarget: true);
                break;
        }
    }

    public static void GoToSwitch(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        switch (state.Substate)
        {
            case 0:
            {
                host.RunAnimation(12);
                state.Field218 |= 0x80;
                host.PlayPositionalSound(TeleportSound);
                PlaySpeech(state, host, SwitchSpeech, 2);
                state.Field3B8 = 0;
                for (int slot = 0; slot < state.SwitchNodeCount; slot++)
                    if (host.SwitchIsOn(state.SwitchTargets[slot])) state.Field3B8 |= 1 << slot;
                int pick = host.Random(state.SwitchNodeCount);
                while (((1 << pick) & state.Field3B8) == 0) pick = host.Random(state.SwitchNodeCount);
                state.Field3B8 &= ~(1 << pick);
                state.WaypointNode = unchecked((short)state.SwitchNodeIndices[pick]);
                state.Field224 = unchecked((short)pick);
                host.Body.Position = PlaceOnGround(state, host, host.TriggerPosition(state.WaypointNode));
                FollowLink(state, host, groundTarget: true);
                FaceTarget(state, host);
                state.Substate++;
                break;
            }
            case 1:
                WaitForFadeIn(state);
                break;
            case 2:
                if (unchecked((uint)CrapDistance(host.Body.Position, state.TargetPosition)) >= 0x80) break;
                if (LinkCount(host, state.WaypointNode) != 0)
                {
                    FollowLink(state, host, groundTarget: false);
                    break;
                }
                host.Body.Position = state.TargetPosition;
                state.Field218 &= ~1u;
                host.Body.Velocity = default;
                host.RunAnimation(0x26);
                state.Substate++;
                break;
            case 3:
                if (host.AnimationFrame < 20) break;
                host.FlickSwitch(state.SwitchTargets[state.Field224]);
                PlaySpeech(state, host, FlickSpeech, 3);
                state.Substate++;
                break;
            case 4:
                if (!host.AnimationFinished) break;
                state.AiState = 1;
                state.Substate = 0;
                break;
        }
    }

    /// <summary>Unnamed 0x4E6630: CalcAim range plus |yaw| and |pitch| deltas against the body angles.</summary>
    public static int AimCone(ActorBodyMotion body, FixedVector3 target, int yawLimit, int pitchLimit, int range)
    {
        ArgumentNullException.ThrowIfNull(body);
        AimAngles aim = FixedAim.Calculate(body.Position, target);
        FixedVector3 delta = (target - body.Position) >> 12;
        int distance = FixedVectorMath.OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Z * delta.Z));
        if (distance > range) return 0;
        if (Abs(Wrap(aim.Yaw - body.Angles.Y)) > yawLimit) return 0;
        if (Abs(Wrap(aim.Pitch - body.Angles.X)) > pitchLimit) return 0;
        return distance == 0 ? 1 : distance;
    }

    private static bool SpotPlayer(VenomState state, IVenomHost host)
    {
        if (state.Substate <= 1 || AimCone(host.Body, host.PlayerPosition, 1024, 1024, 2048) == 0 ||
            !host.LineOfSight(host.Body.Position, host.PlayerPosition))
            return false;
        state.Field218 &= ~1u;
        host.Body.Velocity = default;
        state.AiState = 32;
        state.Substate = 0;
        state.Field3E0 = host.TimerRelated;
        return true;
    }

    private static void WaitForFadeIn(VenomState state)
    {
        if ((state.Field218 & 0x80) != 0) return;
        state.Field218 |= 1;
        state.Substate = 2;
    }

    /// <summary>Teleport onto <paramref name="node"/>, take its single link as the target and face it.</summary>
    private static void BeginLeg(VenomState state, IVenomHost host, int node)
    {
        host.Body.Position = PlaceOnGround(state, host, host.TriggerPosition(node));
        host.PlayPositionalSound(TeleportSound);
        state.WaypointNode = unchecked((short)node);
        FollowLink(state, host, groundTarget: true);
        FaceTarget(state, host);
        state.Substate++;
    }

    private static void AdvanceLeg(VenomState state, IVenomHost host, bool groundTarget)
    {
        if (LinkCount(host, state.WaypointNode) != 0)
        {
            FollowLink(state, host, groundTarget);
            return;
        }
        host.Body.Position = state.TargetPosition;
        host.Body.Velocity = default;
        state.Field218 &= ~1u;
        PlaySpeech(state, host, ArrivalSpeech, 4);
        state.AiState = 1;
        state.Substate = 0;
    }

    private static void FollowLink(VenomState state, IVenomHost host, bool groundTarget)
    {
        (ushort count, ushort next) = VenomResolveSwitchNodes.ReadLinkHead(host, state.WaypointNode);
        if (count == 0)
            throw new InvalidDataException($"Venom node {state.WaypointNode} has no linked node for its path.");
        state.WaypointNode = unchecked((short)next);
        FixedVector3 target = host.TriggerPosition(next);
        state.TargetPosition = groundTarget ? PlaceOnGround(state, host, target) : target;
    }

    private static int LinkCount(IVenomHost host, int node) => VenomResolveSwitchNodes.ReadLinkHead(host, node).Kind;

    private static FixedVector3 PlaceOnGround(VenomState state, IVenomHost host, FixedVector3 position)
    {
        int ground = host.GroundHeight(position, 0, 0x2000, 0);
        return ground == -1 ? position : position with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
    }

    private static void FaceTarget(VenomState state, IVenomHost host)
    {
        AimAngles aim = FixedAim.Calculate(host.Body.Position, state.TargetPosition);
        host.Body.Angles = host.Body.Angles with { Y = aim.Yaw };
    }

    private static void PlaySpeech(VenomState state, IVenomHost host, int[] table, int slot)
    {
        int entry;
        do entry = table[host.Random(table.Length)];
        while (entry == state.LastSpeech[slot]);
        state.LastSpeech[slot] = entry;
        host.PlayPositionSpeech(entry & 0xFFFF, (int)((uint)entry >> 16), host.Body.Position);
    }

    private static int RandomAttackNode(IVenomHost host)
    {
        while (true)
        {
            int node = host.Random(host.TriggerNodeCount);
            if (host.TriggerNodeTypeCode(node) == 1002 && host.TriggerNodeSubtype(node) == 1) return node;
        }
    }

    private static int NearestAttackNode(IVenomHost host)
    {
        uint best = uint.MaxValue;
        int found = -1;
        for (int node = 1; node < host.TriggerNodeCount; node++)
        {
            if (host.TriggerNodeTypeCode(node) != 1002 || host.TriggerNodeSubtype(node) != 1) continue;
            uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(host.TriggerPosition(node), host.PlayerPosition));
            if (distance >= best) continue;
            best = distance;
            found = node;
        }
        // The original keeps an uninitialized node when no ID_LOOKING_TO_ATTACK point exists.
        return found >= 0 ? found : throw new InvalidDataException("No ID_LOOKING_TO_ATTACK node found for Venom.");
    }

    private static int FarthestWalkaboutNode(IVenomHost host)
    {
        uint best = 0;
        int found = -1;
        for (int node = 1; node < host.TriggerNodeCount; node++)
        {
            if (host.TriggerNodeTypeCode(node) != 1002 || host.TriggerNodeSubtype(node) != 2) continue;
            uint distance = unchecked((uint)FixedVectorMath.OriginalDistance(host.TriggerPosition(node), host.PlayerPosition));
            if (distance <= best) continue;
            best = distance;
            found = node;
        }
        return found >= 0 ? found : throw new InvalidDataException("No suitable ID_WALKABOUT_TO IDSCRIPTPOINT found");
    }

    /// <summary>Utils_CrapDist (0x4E6220).</summary>
    public static int CrapDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = Abs(unchecked(first.X - second.X));
        int middle = Abs(unchecked(first.Y - second.Y));
        int smallest = Abs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
    }

    private static int Wrap(int delta)
    {
        if (delta < -2048) delta += 4096;
        if (delta > 2048) delta -= 4096;
        return delta;
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}

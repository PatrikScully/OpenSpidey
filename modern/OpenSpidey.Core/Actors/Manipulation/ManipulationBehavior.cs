using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Manipulation;

/// <summary>CManipOb construction, attachment, pickup and source ballistic/trajectory update.</summary>
public static class ManipulationBehavior
{
    public static void Construct(ManipulationState state, TriggerActorDefinition definition, int sourceNodeOffset, IManipulationHost host)
    {
        if (definition.ActorType != ManipulationState.Type || definition.NodeId != state.Node)
            throw new InvalidDataException("Manipulation body needs its original type401 definition.");
        host.InitializeObjectLibrary(); host.AttachToEnvironmentalList(state);
        state.ItemFlags = (ushort)((state.ItemFlags & 0xFFFD) | 0x10);
        state.Body.LinearDamping = new(12, 12, 12);
        state.Body.Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12;
        state.Body.Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z);
        var cursor = new ActorScriptCursor(definition.BehaviorData, checked(sourceNodeOffset + definition.ObjectDataOffset + 18));
        state.ModelChecksum = cursor.ReadAlignedDword(); host.SelectObjectModel(state.ModelChecksum);
        uint shadow = cursor.ReadAlignedDword();
        if (shadow != 0) state.ShadowItem = host.FindEnvironmentItem(shadow);
        var chunks = new List<uint>();
        if (shadow != 0)
            for (uint checksum = cursor.ReadAlignedDword(); checksum != 0; checksum = cursor.ReadAlignedDword()) chunks.Add(checksum);
        state.ChunkModels = chunks.AsReadOnly();
        ushort flags = cursor.ReadWord();
        if ((flags & 1) != 0) state.Flags |= 8;
        if ((flags & 2) != 0) state.Flags |= 0x20;
        state.Parameter108 = unchecked((short)cursor.ReadWord());
        state.ImpactSound = unchecked((short)cursor.ReadWord());
    }

    public static FixedVector3 GetAttachPoint(ManipulationState state) => state.Body.Position + state.AttachOffset;
    public static bool TugImpulse(ManipulationState state, FixedVector3 point, FixedVector3 velocity, Func<int, int> random)
    {
        state.AttachOffset = point - state.Body.Position;
        state.Body.Velocity = velocity; state.Body.Acceleration = state.Body.Acceleration with { Y = 4096 };
        state.Body.AngularVelocity = state.Body.AngularVelocity with { X = unchecked((short)(64 - random(128))), Y = unchecked((short)(64 - random(128))) };
        state.Flags |= 5; return true;
    }
    public static void Pickup(ManipulationState state, IManipulationHost host)
    {
        state.ItemFlags |= 0x820;
        TurnOffShadow(state, host); SendPulse(state, host);
    }
    public static void Drop(ManipulationState state, FixedVector3 velocity, Func<int, int> random)
    {
        state.ItemFlags &= 0xF7FF; state.Flags = (state.Flags | 1) & ~8;
        state.Body.Velocity = velocity; state.Body.Acceleration = state.Body.Acceleration with { Y = 4096 };
        state.Body.AngularVelocity = state.Body.AngularVelocity with { X = unchecked((short)(random(32) + 64)) };
    }
    public static void Throw(ManipulationState state, FixedVector3 velocity, Func<int, int> random)
    {
        state.ItemFlags &= 0xF7FF; state.Flags |= 1;
        state.Body.Velocity = velocity; state.Body.Acceleration = state.Body.Acceleration with { Y = 4096 };
        state.Body.AngularVelocity = state.Body.AngularVelocity with { X = unchecked((short)(random(32) + 64)) };
    }
    public static void CalculateThrowPositions(ManipulationState state, FixedVector3 target, int count)
    {
        // The original indexes the final two points and divides by count-1; reject its invalid crash domain explicitly.
        if (count <= 1) throw new ArgumentOutOfRangeException(nameof(count), "Original throw trajectory needs at least two points.");
        state.ThrowPositionCount = count;
        FixedVector3 step = (target - state.Body.Position) / count, position = state.Body.Position + step;
        var positions = new FixedVector3[count]; int angleStep = 2048 / (count - 1), angle = 0;
        for (int index = 0; index < count; index++)
        {
            positions[index] = position with { Y = unchecked(position.Y - (GameAngle.Heading(angle).Sine << 8)) };
            position += step; angle = unchecked(angle + angleStep);
        }
        state.ThrowPositions = positions; state.ThrowPositionIndex = 0;
    }
    public static void ThrowAt(ManipulationState state, FixedVector3 target, int count, Func<int, int> random)
    {
        CalculateThrowPositions(state, target, count);
        state.ItemFlags &= 0xF7FF; state.Flags |= 1;
        state.Body.AngularVelocity = state.Body.AngularVelocity with { X = unchecked((short)(random(32) + 64)) };
    }

    /// <summary>Actual original vtable AI slot0x4574D0, absent from the recovered CManipOb header.</summary>
    public static void Advance(ManipulationState state, int elapsedTicks, IManipulationHost host, Func<int, int> random)
    {
        if ((state.Flags & 1) == 0) return;
        ActorBodyMotion body = state.Body; FixedVector3 previous = body.Position;
        if (state.ThrowPositions is { } positions)
        {
            state.ThrowPositionIndex = unchecked(state.ThrowPositionIndex + elapsedTicks);
            if ((uint)state.ThrowPositionIndex < (uint)state.ThrowPositionCount) body.Position = positions[state.ThrowPositionIndex];
            else
            {
                body.Velocity = positions[state.ThrowPositionCount - 1] - positions[state.ThrowPositionCount - 2];
                int beyond = unchecked(state.ThrowPositionIndex - state.ThrowPositionCount + 1);
                body.Position = positions[state.ThrowPositionCount - 1] + body.Velocity * beyond;
                state.ThrowPositions = null;
            }
        }
        else
            for (int tick = 0; tick < elapsedTicks; tick++)
            {
                body.Velocity += body.Acceleration;
                body.Velocity = new(Damp(body.Velocity.X, body.LinearDamping.X), Damp(body.Velocity.Y, body.LinearDamping.Y), Damp(body.Velocity.Z, body.LinearDamping.Z));
                body.Position += body.Velocity;
            }
        for (int tick = 0; tick < elapsedTicks; tick++) body.Angles += body.AngularVelocity;
        FixedVector3 direction = FixedVectorMath.Normalize((body.Position - previous) >> 12), endpoint = body.Position + direction * 128;
        if ((state.Flags & 0x10) == 0)
        {
            IReadOnlyList<ManipulationTargetBody> targets = host.SourceBaddyOrder;
            ActorSphereContact? sphere = ActorLaserCollision.CheckBodies(previous, endpoint, targets.Select(target => target.Sphere).ToArray(), radiusScale: 6144, ignoreRadius: host.IgnoreSphereRadius);
            ManipulationTargetBody? target = null;
            if (sphere is { } contact) target = targets.First(candidate => ReferenceEquals(candidate.Sphere.Identity, contact.Body.Identity));
            if (target is not null && target.Type is 304 or 307 or 310 or 312 or 313 or 317 or 324)
            {
                bool large = (state.Flags & 8) != 0;
                FixedVector3 horizontal = FixedVectorMath.Normalize(direction with { Y = 0 });
                host.HitBaddy(target.Sphere.Identity, new(30, 0, 23, (ushort)(large ? 100 : 50), horizontal, (short)(large ? 500 : 350), (ushort)(large ? 15 : 8)));
                FixedVector3 normal = new(unchecked((short)-direction.X), unchecked((short)-direction.Y), unchecked((short)-direction.Z));
                host.Chunk(state, normal); state.BodyFlags |= 0x40; return;
            }
        }
        if (host.LineToItem(previous, endpoint, 1) is not { } hit) return;
        body.Position = hit.Position - direction * 128;
        if ((state.Flags & 8) != 0) state.ImpactCount = unchecked((short)(state.ImpactCount + 1));
        short oldYawVelocity = body.AngularVelocity.Y;
        body.AngularVelocity = body.AngularVelocity with { X = unchecked((short)(random(128) - 64)), Y = unchecked((short)(random(2 * oldYawVelocity) - oldYawVelocity)) };
        if (state.ImpactCount == 1) host.ShakeCamera(body.Position, 2);
        host.Chunk(state, new(unchecked((short)hit.Normal.X), unchecked((short)hit.Normal.Y), unchecked((short)hit.Normal.Z)));
        state.BodyFlags |= 0x40;
    }
    public static void Smash(ManipulationState state, IManipulationHost host)
    {
        state.Body.Velocity = new(0, 0x20000, 0x20000);
        host.Chunk(state, new(0, -4096, 0)); TurnOffShadow(state, host); SendPulse(state, host); state.BodyFlags |= 0x40;
    }
    public static void TurnOffShadow(ManipulationState state, IManipulationHost host) { if (state.ShadowItem is { } item) host.HideEnvironmentModel(item); }
    public static void SendPulse(ManipulationState state, IManipulationHost host)
    {
        if (state.PulseSent != 0) return;
        state.PulseSent = 1; host.SendAttachedPulse(state.Node);
    }
    private static int Damp(int value, byte count) => unchecked(value - (value >> count));
    // CManipOb inherits the original CBody::Hit accept-without-state-change behavior.
    public static bool Hit() => true;
}

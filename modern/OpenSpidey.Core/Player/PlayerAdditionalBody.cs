using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerAdditionalBodyCheats(bool PulsatingHead, bool Toon, bool Stickman);

/// <summary>Actual render-only CBody504/505 fields, created as plain bodies rather than CSuper/baddy actors.</summary>
public sealed class PlayerAdditionalBody(ushort type, ushort model)
{
    public ushort Type { get; } = type;
    public ushort ModelIndex { get; } = model;
    public ushort ItemFlags { get; set; }
    public ushort BodyFlags { get; set; } = 0x16;
    public FixedVector3 Position { get; set; }
    public ActorAngles Angles { get; set; }
    public FixedVector3 Scale { get; set; } = new(4096, 4096, 4096);
    public uint Rgb { get; set; }
}

/// <summary>Source extra-model construction, cheat scaling, sense pulse and actual posed-part world angle extraction.</summary>
public static class PlayerAdditionalBodyRules
{
    public static PlayerAdditionalBody CreateSense(PlayerAdditionalBodyCheats cheats)
    {
        var body = new PlayerAdditionalBody(504, 4) { ItemFlags = 0x200 };
        if (cheats.PulsatingHead) body.Scale = ScaleShorts(body.Scale, value => value << 1);
        ApplyCheatScale(body, cheats, false);
        return body;
    }
    public static PlayerAdditionalBody CreateFist(PlayerAdditionalBodyCheats cheats)
    {
        var body = new PlayerAdditionalBody(505, 8);
        ApplyCheatScale(body, cheats, true);
        body.ItemFlags |= 0x200;
        return body;
    }
    public static void ApplyCheatScale(PlayerAdditionalBody body, PlayerAdditionalBodyCheats cheats, bool fist)
    {
        if (cheats.Toon) body.Scale = ScaleShorts(body.Scale, value => value * 3 >> 1);
        else if (cheats.Stickman) body.Scale = ScaleShorts(body.Scale, value => fist ? value >> 1 : value * 3 >> 2);
    }
    public static void SetFistScale(PlayerAdditionalBody body, int halfScale, PlayerAdditionalBodyCheats cheats, bool applyCheats)
    {
        body.ItemFlags |= 0x200;
        short scale = unchecked((short)halfScale);
        body.Scale = new(scale, scale, scale);
        if (applyCheats) ApplyCheatScale(body, cheats, true);
    }
    public static void SetSensePulse(PlayerAdditionalBody body, uint sourceClock, PlayerAdditionalBodyCheats cheats)
    {
        int pulse = GameAngle.Heading(unchecked((int)sourceClock << 6)).Sine;
        if (pulse < 0) pulse = -pulse;
        pulse = pulse / 2 + 2048;
        body.Scale = new(pulse, pulse, pulse);
        if (cheats.PulsatingHead) body.Scale = ScaleShorts(body.Scale, value => value << 1);
        ApplyCheatScale(body, cheats, false);
    }
    public static void SetFistPresentation(PlayerAdditionalBody body, FixedVector3 camera, bool fireWebbing, Func<int, int> random)
    {
        int distance = PlayerTargetSelection.Distance(body.Position, camera);
        if (distance < 256)
        {
            body.ItemFlags |= 0xC00;
            uint level = unchecked((uint)(distance - 170));
            body.Rgb = distance < 170 ? 0 : ((level << 8) | level) << 8 | level;
        }
        else { body.ItemFlags &= 0xF3FF; body.Rgb = 0x808080; }
        if (fireWebbing)
        {
            uint blue = body.Rgb & 255;
            body.ItemFlags |= 0x400;
            body.Rgb = unchecked((uint)(random((int)blue) / 2) << 8) | blue;
        }
    }
    public static ActorAngles PartAngles(FixedJointTransform actor, FixedJointTransform part)
    {
        FixedJointTransform world = SourceVertexLighting.Multiply(actor, part);
        int horizontal = FixedVectorMath.OriginalSquareRoot(unchecked(world.M02 * world.M02 + world.M22 * world.M22));
        return new(unchecked((short)GameAngle.Atan2(-world.M12, horizontal)),
            unchecked((short)GameAngle.Atan2(world.M02, world.M22)), unchecked((short)GameAngle.Atan2(world.M10, world.M11)));
    }
    private static FixedVector3 ScaleShorts(FixedVector3 scale, Func<int, int> apply) =>
        new(unchecked((short)apply(scale.X)), unchecked((short)apply(scale.Y)), unchecked((short)apply(scale.Z)));
}

using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct ActorSpriteRingParticle(FixedVector3 Position, FixedVector3 Velocity,
    int AnimationIndex, short Scale, ushort ScaleDecay, short ColorDecay, ushort FrigDeltaZ);

public interface IActorSpriteRingHost
{
    int GroundHeight(FixedVector3 position, int above, int below);
    void EmitSprite(ActorSpriteRingParticle particle);
}

/// <summary>Recovered CBaddy::MakeSpriteRing placement and original Bit_MakeSpriteRing emissions.</summary>
public static class ActorSpriteRing
{
    public static int Create(ActorBodyMotion actor, FixedVector3? localOffset, IActorSpriteRingHost host)
    {
        FixedVector3 center = actor.Position;
        if (localOffset is { } offset)
        {
            FixedJointTransform rotation = FixedJointTransform.FromJoint(new(actor.Angles.X, actor.Angles.Y, actor.Angles.Z, 0, 0, 0));
            center += FixedRotation.Transform(rotation, offset >> 12) << 12;
        }
        // The source queries the actor center, then replaces transformed Y even when ground returns -1.
        center = center with { Y = unchecked(host.GroundHeight(actor.Position, 300, 600) - 0xA000) };
        for (int i = 0; i < 24; i++)
        {
            (short sine, short cosine) = GameAngle.Heading((i * 0x1000 / 24) & 0xFFF);
            host.EmitSprite(new(center, new(cosine * 8, 0, sine * 8), 1, 512, 32, 16, 0));
        }
        return 0;
    }
}

/// <summary>Original ring particles use CMotionBlur::Move (PC vtable0x53B3CC), once per bit update.</summary>
public sealed class SpriteRingParticleMotion
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; }
    public short Scale { get; private set; }
    public ushort ScaleDecay { get; }
    public short ColorDecay { get; }
    public ushort FrigDeltaZ { get; }
    public int AnimationIndex { get; }
    public byte AnimationFrames { get; }
    public short Age { get; private set; }
    public sbyte Frame { get; private set; }
    public uint CodeRgb { get; private set; } = 0x2E808080;
    public bool Alive { get; private set; } = true;
    public uint PostScale => 0x10001000;
    public byte SemiTransparencyRate => 32;
    public short Angle => 0;

    public SpriteRingParticleMotion(ActorSpriteRingParticle particle, byte animationFrames)
    {
        Position = particle.Position;
        Velocity = particle.Velocity;
        Scale = particle.Scale;
        ScaleDecay = particle.ScaleDecay;
        ColorDecay = particle.ColorDecay;
        FrigDeltaZ = particle.FrigDeltaZ;
        AnimationIndex = particle.AnimationIndex;
        AnimationFrames = animationFrames;
    }

    public void Advance()
    {
        if (!Alive) return;
        Position += Velocity;
        Scale = unchecked((short)(Scale - ScaleDecay));
        if (Scale < 0) { Scale = 0; Alive = false; return; }
        byte red = Decay((byte)CodeRgb), green = Decay((byte)(CodeRgb >> 8)), blue = Decay((byte)(CodeRgb >> 16));
        CodeRgb = (CodeRgb & 0xFF000000) | red | ((uint)green << 8) | ((uint)blue << 16);
        if ((CodeRgb & 0xFFFFFF) == 0) { Alive = false; return; }
        Age = unchecked((short)(Age + 1));
        if ((Age & 1) != 0)
        {
            Frame = unchecked((sbyte)(Frame + 1));
            if (Frame >= AnimationFrames) Frame = 0;
        }
    }

    private byte Decay(byte component) => ColorDecay > component ? (byte)0 : unchecked((byte)(component - (ColorDecay & 0xFF)));
}

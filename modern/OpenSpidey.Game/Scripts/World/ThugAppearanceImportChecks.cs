using Godot;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Checks actual owned native meshes/materials and original protected Shadow2 quad without changing gameplay actors.</summary>
public static class ThugAppearanceImportChecks
{
    public static void Verify(GodotThugActor actor, GodotThugAppearance appearance)
    {
        ushort flags = actor.ItemFlags;
        uint bodyFlags = actor.State.BodyFlags, tint = actor.State.TintColor;
        int scale = actor.State.ShadowScale;
        FixedVector3 position = actor.State.ShadowPosition;
        short distance = appearance.ShadowDistance, threshold = appearance.ShadowThreshold;
        try
        {
            actor.ItemFlags = (ushort)(flags & ~0xC80);
            actor.State.ShadowPosition = new FixedVector3(-10,20,30) <<12;
            actor.State.ShadowScale = 50;
            appearance.ShadowDistance = 0;
            appearance.ShadowThreshold = 200;
            appearance.ShadowOn(); appearance.Synchronize();
            appearance.VerifyGeometry(); appearance.VerifyTransparency(false);
            FixedVector3[] expected = [new(-60,20,80),new(40,20,80),new(-60,20,-20),new(40,20,-20)];
            if (!appearance.Shadow.State.Exists || !appearance.Shadow.State.Corners.SequenceEqual(expected.Select(point => point<<12)) ||
                appearance.Shadow.State.Transparency != 128 || appearance.Shadow.State.CodeBgr != 0x1C0341)
                throw new InvalidDataException("Native protected shadow differs from independent original floor-normal/corner/tint gold.");
            // Actual PC source pow(f32(64/255),f32(.7))*255→int96; face channels then multiply and shift8.
            actor.State.TintColor = 0xFF0040;
            actor.ItemFlags |= 0xC00;
            appearance.Synchronize(); appearance.VerifyGeometry(96,0,255); appearance.VerifyTransparency(true);
            actor.ItemFlags &= 0xF3FF;
            appearance.Synchronize(); appearance.VerifyGeometry(); appearance.VerifyTransparency(false);
            appearance.KillShadow();
            if (appearance.Shadow.State.Exists || (actor.State.BodyFlags & 8) != 0 || appearance.Shadow.Visible)
                throw new InvalidDataException("KillShadow must clear the source body flag and protected native quad.");
            GD.Print("Passed native thug appearance checks: unchanged exact skin, byte-quantized source tint, corpse blend and original Shadow2 quad/lifetime.");
        }
        finally
        {
            actor.ItemFlags = flags; actor.State.BodyFlags = bodyFlags; actor.State.TintColor = tint;
            actor.State.ShadowScale = scale; actor.State.ShadowPosition = position;
            appearance.ShadowDistance = distance; appearance.ShadowThreshold = threshold;
            appearance.Synchronize();
        }
    }
}

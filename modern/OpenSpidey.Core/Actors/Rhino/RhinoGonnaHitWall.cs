using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

public sealed partial class RhinoBrain
{
    /// <summary>CRhino::GonnaHitWall (0x47E8A0): physics step, then the 128-unit forward probe and its wall reactions.</summary>
    private int GonnaHitWall(int mode)
    {
        state.PreviousPosition = host.Body.Position;
        host.DoPhysics(0);
        FixedVector3 scaled = (host.Body.Position - state.PreviousPosition) >> 12;
        FixedVector3 direction = FixedVectorMath.Normalize(scaled with { Y = 0 });
        if (direction.Y != 0) return 0;
        if (!host.TryGetChargeWallContact(state.PreviousPosition, direction, out RhinoChargeWallContact contact))
            return 0;

        if ((contact.ItemFlags & 0x10) != 0 && contact.ActorType == 0x191 &&
            !ReferenceEquals(contact.Collider, host.PlayerHeldObject))
        {
            host.SmashEnvironmentalBody(contact.Collider);
            if (mode == 0) return 1;
            return HeavyWallDamage();
        }

        if (Contains(host.WallChunks.Chunk0, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            return 2;
        }

        host.Body.Position = state.PreviousPosition;
        if (Contains(host.WallChunks.Chunk1, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            if (mode == 0) return 1;
            return HeavyWallDamage();
        }

        if (Contains(host.WallChunks.Chunk2, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            if (mode != 0)
            {
                SetUpStuckHorn(contact, wallImpact: false);
                SetState(0xC);
            }
            return 4;
        }

        bool scrape = Contains(host.WallChunks.Chunk3, contact.ModelChecksum);
        if (mode != 0)
        {
            state.Health = unchecked((short)(state.Health - 10));
            if (state.Health <= 0) SetState(0x15);
        }
        if (state.Health <= 0) return 1;
        PlayXAPlease(3, 3, 1);
        SetUpStuckHorn(contact, wallImpact: !scrape);
        return 8;
    }

    private int HeavyWallDamage()
    {
        state.Health = unchecked((short)(state.Health - 100));
        if (state.Health <= 0)
        {
            SetState(0x15);
            return 1;
        }
        host.Neutralize();
        state.BodyFlags |= 0x10;
        SetState(9);
        return 1;
    }

    /// <summary>CRhino::SetUpStuckHorn (0x47E1F0). The original's normal*120 += on a discarded temporary has no effect.</summary>
    private void SetUpStuckHorn(RhinoChargeWallContact contact, bool wallImpact)
    {
        host.Neutralize();
        FixedVector3 position = host.Body.Position;
        FixedVector3 aimPoint = position - (contact.Normal << 14);
        int yaw = unchecked((short)FixedAim.Calculate(position, aimPoint).Yaw);
        host.AttachLookAtYaw(yaw, 0, 55, 200);
        host.AttachMoveTo(contact.HitPosition, 200, 0, 500, 1);
        state.Field388 = 0;
        host.RunAnimation(24);
        if (wallImpact)
        {
            host.CreateWallImpact(contact);
            host.ShakeCamera();
        }
        ShakePad();
        host.PlayPositionalSound(0x804B);
        SetState(0xA);
    }

    private static bool Contains(uint[] list, uint checksum)
    {
        foreach (uint entry in list)
            if (entry == checksum) return true;
        return false;
    }
}

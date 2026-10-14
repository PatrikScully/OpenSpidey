using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>CRhino::GonnaHitWall wall/chunk probe after non-zero horizontal motion (0x47E8A0).</summary>
public static class RhinoGonnaHitWall
{
    public static int Evaluate(RhinoState state, IRhinoHost host, RhinoWallChunkLists chunks, int mode, Action<int, int, int> playXAPlease)
    {
        state.PreviousPosition = host.Body.Position;
        host.DoPhysics(0);
        FixedVector3 delta = host.Body.Position - state.PreviousPosition;
        FixedVector3 scaled = delta >> 12;
        FixedVector3 horizontal = scaled with { Y = 0 };
        FixedVector3 direction = FixedVectorMath.Normalize(horizontal);
        if (direction.Y != 0) return 0;
        if (!host.TryGetChargeWallContact(state.PreviousPosition, direction, out RhinoChargeWallContact contact))
            return 0;

        if ((contact.ItemFlags & 0x10) != 0 && contact.ActorType == 0x191 &&
            !host.IsPlayerBody(contact.Collider))
        {
            host.NotifyChargeBarrierVisible(contact.Collider);
            if (mode == 0) return 1;
            host.ApplyChargeBarrierCollision(mode);
            return 1;
        }

        if (Contains(chunks.Chunk0, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            return 2;
        }

        host.RewindChargePosition(state.PreviousPosition);
        if (Contains(chunks.Chunk1, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            if (mode == 0) return 1;
            host.ApplyChargeBarrierCollision(mode);
            return 1;
        }

        if (Contains(chunks.Chunk2, contact.ModelChecksum))
        {
            host.ChunkItemByChecksum(contact.ModelChecksum);
            if (mode != 0)
            {
                host.BeginStuckHorn(contact, shake: 0);
                state.State = 0xC;
                state.Substate = 0;
            }
            return 4;
        }

        if (Contains(chunks.Chunk3, contact.ModelChecksum))
        {
            if (mode != 0) host.ApplyChargeScrapeDamage();
            if (state.Health <= 0) return 1;
            playXAPlease(3, 3, 1);
            host.BeginStuckHorn(contact, shake: 0);
            return 8;
        }

        if (mode != 0) host.ApplyChargeScrapeDamage();
        if (state.Health <= 0) return 1;
        playXAPlease(3, 3, 1);
        host.BeginStuckHorn(contact, shake: 1);
        return 8;
    }

    private static bool Contains(uint[] list, uint checksum)
    {
        foreach (uint entry in list)
            if (entry == checksum) return true;
        return false;
    }
}

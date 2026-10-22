using System.Buffers.Binary;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ControlActorSmokeJetTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] words = new byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(0), 120);
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2), 1500);
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(4), 80);
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(6), 90);
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(8), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(10), 0x002B);

        var operands = new ActorScriptCursor(words);
        ControlActorSmokeJetSpec spec = ControlActorSmokeJet.ReadSpec(operands, damageBaddies: true, ControlActorSmokeJet.SewerChecksum);
        assert(spec.Length == 120 && spec.RawDuration == 1500 && spec.Red == 80 && spec.Green == 90 &&
            spec.Blue == 100 && spec.ScrollSpeed == 0x2B && spec.TextureChecksum == ControlActorSmokeJet.SewerChecksum &&
            spec.DamageBaddies && operands.Position == 12,
            "SmokeJetOn operands follow the six-word CScriptOnlyBaddy layout (length, duration, RGB, scroll).");

        var position = new FixedVector3(1, 2, 3) << 12;
        var angles = new ActorAngles(10, 20, 30);
        var effects = new ControlActorEffects(new(), new Host());
        operands = new ActorScriptCursor(words);
        assert(effects.Execute(0x429B, operands, position, angles, default) && operands.Position == 12 &&
            effects.SmokeJet is { Spec.DamageBaddies: true, Spec.TextureChecksum: ControlActorSmokeJet.SewerChecksum,
                ExtendedDuration: true, Duration: 500, Alive: true },
            "0x429B creates the sewer checksum jet, applies the 0x3E8 duration bias and consumes twelve operand bytes.");

        FixedVector3 moved = position + new FixedVector3(4096, 0, 0);
        effects.Advance(0, moved);
        assert(effects.SmokeJet!.Anchor == moved, "The owned jet anchor follows the control actor during AI reposition ticks.");

        assert(effects.Execute(0x429F, new ActorScriptCursor(ReadOnlyMemory<byte>.Empty), moved, angles, default) &&
            effects.SmokeJet is null,
            "0x429F deletes the owned jet pointer.");

        operands = new ActorScriptCursor(words);
        assert(effects.Execute(0x42A1, operands, position, angles, default) &&
            effects.SmokeJet!.Spec is { DamageBaddies: false, TextureChecksum: ControlActorSmokeJet.GroundChecksum },
            "0x42A1 selects the ground checksum variant without setting damage-baddies.");
    }

    private sealed class Host : IControlActorEffectHost
    {
        public void CreateCompass(FixedVector3 position) { }
        public void DestroyCompass() { }
        public uint PlayGlobalSound(uint sound) => 0;
        public uint PlayPositionSound(uint sound, FixedVector3 position) => 0;
        public void StopSound(uint handle) { }
        public void UpdateSoundPosition(uint handle, FixedVector3 position) { }
    }
}

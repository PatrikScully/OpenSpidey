using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.UI;

internal static class ControlActorEffectsTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(ControlActorEffectsTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.control-actor-effects.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var state = new ActorScriptState { Flags = test.GetProperty("flags").GetUInt32(),
                SkipDuringDeath = test.GetProperty("skip").GetInt32() != 0 };
            int mode = test.GetProperty("mode").GetInt32();
            var host = new Host(test.GetProperty("soundReturn").GetUInt32(), mode == 2);
            var effects = new ControlActorEffects(state, host)
            {
                SoundHandle = test.GetProperty("handle").GetUInt32(),
                SoundPositionPeriod = test.GetProperty("oldPeriod").GetInt16()
            };
            JsonElement inputPosition = test.GetProperty("position");
            var position = new FixedVector3(inputPosition[0].GetInt32(), inputPosition[1].GetInt32(), inputPosition[2].GetInt32());
            byte[] words = new byte[4];
            BinaryPrimitives.WriteUInt16LittleEndian(words, test.GetProperty("operand").GetUInt16());
            BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(2), test.GetProperty("period").GetUInt16());
            var operands = new ActorScriptCursor(words);
            bool result = true;
            if (mode == 0) result = effects.Execute(test.GetProperty("opcode").GetUInt16(), operands, position, default,
                new(FastForwardDeath: test.GetProperty("fast").GetInt32() != 0));
            else if (mode == 1) effects.Advance(test.GetProperty("clock").GetUInt32(), position);
            else effects.Destroy();

            JsonElement expected = test.GetProperty("expected");
            long[] actual = [(result ? 1 : 0), effects.SoundHandle, effects.SoundPositionPeriod, state.Flags, operands.Position,
                (host.Compass.Active ? 1 : 0), host.Compass.TargetPosition.X, host.Compass.TargetPosition.Y,
                host.Compass.TargetPosition.Z, host.Compass.FlashTimer, ..host.Events];
            long[] gold = [expected.GetProperty("result").GetInt32(), expected.GetProperty("handle").GetUInt32(),
                expected.GetProperty("period").GetInt32(), expected.GetProperty("flags").GetUInt32(),
                expected.GetProperty("consumed").GetInt32(), ..expected.GetProperty("compass").EnumerateArray().Select(value => value.GetInt64()),
                ..expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt64())];
            assert(actual.SequenceEqual(gold),
                $"Original CScriptOnlyBaddy effect case{index}, mode{mode}: actual[{string.Join(",", actual)}] expected[{string.Join(",", gold)}].");
            if (mode == 2)
            {
                int nativeCalls = host.Events.Count;
                effects.Destroy();
                assert(host.Events.Count == nativeCalls, "Logical source removal and actual Predelete cannot stop the same owned sound or destroy the compass twice.");
            }
            index++;
        }
        var zeroPeriod = new ControlActorEffects(new(), new Host(0, false)) { SoundPositionPeriod = 0 };
        bool divideByZero = false;
        try { zeroPeriod.Advance(3, default); }
        catch (DivideByZeroException) { divideByZero = true; }
        assert(divideByZero, "A malformed zero reposition period retains the original signed IDIV failure rather than silently inventing a timing policy.");
    }

    private sealed class Host : IControlActorEffectHost
    {
        private readonly uint soundReturn;
        public CompassState Compass { get; } = new() { FlashTimer = 17 };
        public List<long> Events { get; } = [];
        public Host(uint soundReturn, bool active)
        {
            this.soundReturn = soundReturn;
            Compass.Create(new FixedVector3(91, -3, 5) << 12);
            if (!active) Compass.Destroy();
        }
        public void CreateCompass(FixedVector3 position) => Compass.Create(position);
        public void DestroyCompass() => Compass.Destroy();
        public uint PlayGlobalSound(uint sound) { Events.AddRange([1, sound, 8192, 0]); return soundReturn; }
        public uint PlayPositionSound(uint sound, FixedVector3 position)
        { Events.AddRange([2, sound, position.X, position.Y, position.Z, 0]); return soundReturn; }
        public void StopSound(uint handle) => Events.AddRange([0, handle]);
        public void UpdateSoundPosition(uint handle, FixedVector3 position) => Events.AddRange([3, handle, position.X, position.Y, position.Z, 0]);
    }
}

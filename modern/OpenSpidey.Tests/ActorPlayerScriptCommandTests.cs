using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;

internal static class ActorPlayerScriptCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ActorPlayerScriptCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-player-scripts.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            long[] input = Numbers(fixture.GetProperty("input"));
            ushort opcode = (ushort)input[0]; int start = (int)input[2];
            short[] program = input[12] != 0 ? [0, 16, 124, 850, 1, 134, 1, 3, 7, 100, 100, 9, 1, 0, 255] : [0, 9, 1, 0, 255];
            var bytes = new byte[128];
            if (opcode == 0x470E)
                for (int word = 0; word < program.Length; word++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(start + word * 2), program[word]);
            else BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start), (ushort)input[1]);
            var cursor = new ActorScriptCursor(bytes, 102, start);
            var host = new Host(input, cursor);
            bool result = opcode == 0x42B9 ? ActorCinematicScreenCommand.TryExecute(opcode, cursor, host) :
                ActorPlayerScriptCommands.TryExecute(opcode, cursor, host);
            JsonElement expected = fixture.GetProperty("expected");
            assert(result && expected.GetProperty("result").GetInt32() == 1 &&
                cursor.Position == expected.GetProperty("cursor").GetInt32() && host.EntryCursor == expected.GetProperty("entry").GetInt32(),
                $"Whole original actor script case{index} preserves call-entry and post-call cursor commits.");
            long[] wanted = Numbers(expected.GetProperty("state"));
            // The typed VM uses booleans for source1AC/1B4; unchanged poisoned source bytes retain their truth value.
            wanted[3] = wanted[3] != 0 ? 1 : 0; wanted[4] = wanted[4] != 0 ? 1 : 0;
            assert(host.Snapshot().SequenceEqual(wanted), $"Whole original actor script case{index}: state differs. Actual[{string.Join(',', host.Snapshot())}], expected[{string.Join(',', wanted)}].");
            string[] wantedCalls = expected.GetProperty("calls").EnumerateArray().Select(value => value.GetString()!).ToArray();
            assert(host.Calls.SequenceEqual(wantedCalls), $"Whole original4BC1A0 case{index} preserves recover/destructor/handle/animation ordering.");
            assert(host.SoundWords.SequenceEqual(Numbers(expected.GetProperty("sounds"))), $"Original script switch case{index} resets only selected clip0/21 sound words.");
            PlayerButtonState[] pad = expected.GetProperty("pad").EnumerateArray().Select(record =>
            { long[] values = Numbers(record); return new PlayerButtonState((byte)values[0], (byte)values[1], (byte)values[2]); }).ToArray();
            assert(host.Pad.SequenceEqual(pad) && expected.GetProperty("preserved").GetBoolean(),
                $"Original script switch case{index} clears exactly the20 three-byte pad records and preserves all other0xF20 bytes.");
            index++;
        }
        var unrelated = new ActorScriptCursor(ReadOnlyMemory<byte>.Empty);
        assert(!ActorPlayerScriptCommands.TryExecute(0x470C, unrelated, null!) &&
            !ActorCinematicScreenCommand.TryExecute(0x42BA, unrelated, null!) && unrelated.Position == 0,
            "Unrelated commands do not resolve player/cinematic globals or consume data.");
        var timer = new ActorScriptCursor(new byte[2]);
        try { ActorPlayerScriptCommands.TryExecute(0x470D, timer, new FailedHost()); throw new Exception("Required timer failure was swallowed."); }
        catch (InvalidOperationException) { assert(timer.Position == 2, "Original470D advances its cursor before the required player call."); }
        var embedded = new ActorScriptCursor(new byte[4]);
        try { ActorPlayerScriptCommands.TryExecute(0x470E, embedded, new FailedHost()); throw new Exception("Required player VM failure was swallowed."); }
        catch (InvalidOperationException) { assert(embedded.Position == 0, "Original470E keeps its actor cursor until the whole script switch returns."); }
        foreach (byte[] malformed in new[] { Array.Empty<byte>(), new byte[] { 1, 0 }, new byte[] { 0, 0, 1, 0 } })
        {
            bool rejected = false;
            try { PlayerSynthesizedInput.StreamLength(malformed, 128); } catch (InvalidDataException) { rejected = true; }
            assert(rejected, "An embedded source player stream requires its genuine zero-followed-byFF terminator.");
        }
        byte[] embeddedWithTail = [0, 0, 9, 0, 1, 0, 0, 0, 255, 0, 0xB4, 0x42, 0x7A, 0];
        assert(PlayerSynthesizedInput.StreamLength(embeddedWithTail) == 10 &&
            PlayerScriptProgram.Read(embeddedWithTail.AsSpan(0, 10)).Instructions.Count == 2,
            "The raw stream scanner leaves later actor42B4 data outside the decoded player program.");
        var shared = new PlayerCombatState(73, 100) { FieldE18 = 7, SavedInputAnimationSpeed = -222, LookaroundActive = true };
        int exits = 0;
        var countdown = new PlayerInputSuppression();
        countdown.BindSourceState(shared, () => { exits++; shared.LookaroundActive = false; });
        assert(countdown.RemainingTicks == 7 && countdown.SavedAnimationSpeed == -222,
            "Binding input suppression adopts the actual source E18/E12 values rather than a second countdown.");
        countdown.Set(10, 0x12345, shared.LookaroundActive);
        assert(shared.FieldE18 == 10 && shared.SavedInputAnimationSpeed == 0x2345 && exits == 1 && !shared.LookaroundActive,
            "SetIgnoreInputTimer publishes its true source timer/signed16 speed before exiting the real lookaround owner.");
        shared.FieldE18 = 2;
        PlayerInputSuppressionStep exact = countdown.Advance(2, false);
        assert(shared.FieldE18 == 0 && exact.ClearButtons && !exact.BlockMovement && exact.RestoredAnimationSpeed is null,
            "An external E18 write is read directly by source decay; exact zero does not restore animation speed.");
        countdown.Set(1, -12345);
        PlayerInputSuppressionStep expired = countdown.Advance(2, false);
        assert(shared.FieldE18 == 0 && expired.RestoredAnimationSpeed == -12345,
            "Negative expiry restores the canonical signed E12 speed and clamps the same E18 field to zero.");
        countdown.Set(8000, 123);
        countdown.Advance(2, true);
        assert(shared.FieldE18 == 8000, "Actual synthesized input keeps the original E18 countdown suspended.");
    }

    private static long[] Numbers(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class FailedHost : IActorPlayerScriptHost
    {
        public void SetIgnoreInputTimer(ushort frames) => throw new InvalidOperationException("The genuine player timer service failed.");
        public int SwitchToSynthesizedInput(ReadOnlyMemory<byte> source, int sourceOffset) => throw new InvalidOperationException("The genuine player VM service failed.");
    }
    private sealed class Host : IActorPlayerScriptHost, IActorCinematicScreenHost, IPlayerSynthesizedInputHost
    {
        private readonly long[] input;
        private readonly ActorScriptCursor cursor;
        private readonly PlayerCombatState combat;
        private readonly PlayerCutsceneSkipState skip;
        private readonly PlayerInputSuppression suppression;
        private bool decoding;
        private int checkpoint, elapsed;
        private readonly bool[] trails = new bool[2];
        private readonly int[] fade = new int[2];
        private object? dome, held;
        private uint validation = 0x12345678;
        private int animation = -1, firstFrame = -2, lastFrame = -2;
        public List<string> Calls { get; } = [];
        public long[] SoundWords { get; } = [0xABCD0012u, 0x12345678u, 0xABCD0034u, 0x98765432u];
        public PlayerButtonState[] Pad { get; } = new PlayerButtonState[20];
        public int EntryCursor { get; private set; } = -1;
        public int WideScreenValue { get; set; } = -444;
        public int WideScreenShadow { get; set; } = -555;
        public object? HeldObject => held;

        public Host(long[] input, ActorScriptCursor cursor)
        {
            this.input = input; this.cursor = cursor;
            int seed = (int)input[9];
            combat = new(73, 100) { Velocity = new(seed + 11, seed - 27, -seed), SynthesizedInputActive = seed != 0,
                FieldAE5 = (byte)seed, ActionState = (uint)input[7], LookaroundActive = input[11] != 0, FieldE18 = -123 };
            skip = new((byte)(seed + 1)); decoding = (byte)(seed + 3) != 0;
            checkpoint = seed * 31; elapsed = seed * 17;
            suppression = new(() => { Calls.Add("lookaround"); combat.LookaroundActive = false; }); suppression.Set(-123, -234);
            trails[0] = (input[4] & 1) != 0; trails[1] = (input[4] & 2) != 0;
            dome = input[5] != 0 ? new object() : null; held = input[6] != 0 ? new object() : null;
            for (int slot = 0; slot < Pad.Length; slot++)
            {
                int offset = 0x1C0 + slot * 16;
                Pad[slot] = new((byte)(offset * 37 + seed), (byte)((offset + 1) * 37 + seed), (byte)((offset + 2) * 37 + seed));
            }
        }
        public void SetIgnoreInputTimer(ushort frames)
        {
            EntryCursor = cursor.Position;
            combat.FieldE18 = frames;
            suppression.Set(frames, (int)input[10], combat.LookaroundActive);
            cursor.Seek(cursor.Position + (int)input[3]);
        }
        public int SwitchToSynthesizedInput(ReadOnlyMemory<byte> source, int sourceOffset)
        {
            EntryCursor = cursor.Position;
            PlayerSynthesizedInput.Begin(combat, skip, Pad, this, () => { decoding = true; checkpoint = 0; });
            int consumed = PlayerSynthesizedInput.StreamLength(source.Span, sourceOffset);
            cursor.Seek(cursor.Position + (int)input[3]);
            return consumed;
        }
        public void SetVelocity(FixedVector3 velocity) => combat.Velocity = velocity;
        public bool SmokeTrailPresent(int slot) => trails[slot];
        public void FadeSmokeTrail(int slot) { trails[slot] = false; fade[slot] = 1; }
        public object? RecoverDome() { Calls.Add("recover"); return dome; }
        public void DestroyDome(object value) => Calls.Add("destroy-dome");
        public void ClearDomeHandle() { Calls.Add("clear-dome"); dome = null; validation = 0; }
        public void DestroyHeldObject(object item)
        { Calls.Add("destroy-held"); if (input[13] != uint.MaxValue) combat.ActionState = (uint)input[13]; }
        public void ClearHeldObject() => held = null;
        public void SelectAndResetAnimationSounds(ushort clip)
        {
            if (input[8] == 0) return;
            int offset = clip == 21 ? 2 : 0;
            SoundWords[offset] &= 65535; SoundWords[offset + 1] &= 65535;
        }
        public void RunAnimation(ushort clip, int from, int to)
        { Calls.Add("animation"); animation = clip; firstFrame = from; lastFrame = to; }
        public void ClearInputAliases() { }
        public long[] Snapshot() => [combat.Velocity.X, combat.Velocity.Y, combat.Velocity.Z,
            combat.SynthesizedInputActive ? 1 : 0, decoding ? 1 : 0, combat.FieldAE5, skip.Allowed, checkpoint, elapsed,
            trails[0] ? 1 : 0, trails[1] ? 1 : 0, fade[0], fade[1], dome is not null ? 1 : 0, validation,
            held is not null ? 1 : 0, combat.ActionState, animation, firstFrame, lastFrame,
            combat.FieldE18, suppression.SavedAnimationSpeed, combat.LookaroundActive ? 1 : 0, WideScreenValue, WideScreenShadow];
    }
}

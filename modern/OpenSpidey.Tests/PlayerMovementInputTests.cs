using System.Reflection;
using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerMovementInputTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.player-input.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement fixture in document.RootElement.EnumerateArray())
        {
            int[] state = Values(fixture, "state");
            int[] sample = Values(fixture, "sample");
            int[] options = Values(fixture, "options");
            var initial = new PlayerMovementInputState(new((sbyte)state[0], (sbyte)state[1]), default,
                state[2], (short)state[3], (short)state[4], state[5] != 0, state[6] != 0, state[7] != 0);
            var input = new PlayerInputSample(sample[0] != 0, new((sbyte)sample[1], (sbyte)sample[2]),
                sample[3] != 0, sample[4] != 0, sample[5] != 0, sample[6] != 0,
                sample[7] != 0, sample[8] != 0,
                sample[9] != 0 ? new PlayerAxes((sbyte)sample[10], (sbyte)sample[11]) : null);
            var settings = new PlayerInputOptions(options[0] != 0, (ushort)options[1], (ushort)options[2],
                options[3] != 0, options[4] != 0);
            PlayerMovementInputResult result = PlayerMovementInput.Read(initial, input, settings);
            PlayerMovementInputState next = result.State;
            int[] actual = [next.Axes.Forward, next.Axes.Sideways, next.PreviousAxes.Forward, next.PreviousAxes.Sideways,
                next.MovementRamp, next.Heading, next.PrimaryCenterPending ? 1 : 0,
                next.SecondaryCenterPending ? 1 : 0, next.CenteredLatch ? 1 : 0,
                result.LeftOnePressed ? 1 : 0, result.LeftTwoTriggered ? 1 : 0];
            assert(actual.SequenceEqual(Values(fixture, "result")),
                $"Player input {index++} matches the recovered input reader.");
            assert(next.ReferenceHeading == initial.ReferenceHeading, "Input preserves the reference heading.");
        }

        var baseline = new PlayerMovementInputState(new(12, -34), default, 224, 321, 0, false, false, false);
        var digital = new PlayerInputSample(false, default, true, true, true, true, false, false);
        PlayerMovementInputResult read = PlayerMovementInput.Read(baseline, digital, default);
        assert(read.State.Axes == new PlayerAxes(-127, 127) && read.State.MovementRamp == 256,
            "Conflicting digital directions prioritize up/right and cap the movement ramp.");
        read = PlayerMovementInput.Read(baseline, digital with { AnalogAvailable = true, Analog = new(0, 12) }, default);
        assert(read.State.Axes == new PlayerAxes(0, 12), "A nonzero analogue axis suppresses both digital axes.");

        var pending = baseline with { PrimaryCenterPending = true, SecondaryCenterPending = true, CenteredLatch = true };
        read = PlayerMovementInput.Read(pending, digital with { LeftOnePressed = true, LeftTwoTriggered = true },
            new(true, 1, 0, true, true));
        assert(read.State.Axes == default && read.State.PreviousAxes == pending.Axes && read.State.MovementRamp == 0 &&
            read.State.Heading == pending.Heading && read.State.CenteredLatch && !read.State.PrimaryCenterPending &&
            !read.State.SecondaryCenterPending && !read.LeftOnePressed && read.LeftTwoTriggered,
            "Ignored physical input clears movement and pending flags while preserving heading, latch and the other button.");
        read = PlayerMovementInput.Read(baseline, digital with { Scripted = new(-127, 0) }, new(true, 0, 0, false, false));
        assert(read.State.Axes == new PlayerAxes(-127, 0), "Story-script movement bypasses ignored physical input.");

        read = PlayerMovementInput.Read(pending, default, default);
        assert(read.State.CenteredLatch && !read.State.PrimaryCenterPending && read.State.SecondaryCenterPending && read.LeftTwoTriggered,
            "Centering consumes the primary pending event before the secondary event.");
        read = PlayerMovementInput.Read(read.State, default, default);
        assert(!read.State.SecondaryCenterPending && !read.LeftTwoTriggered,
            "The next centered sample consumes the secondary event without repeating the primary trigger.");
        read = PlayerMovementInput.Read(pending, digital with { Scripted = new(-128, 1) }, new(false, 0, 0, true, true));
        assert(read.State.Axes.Forward == -128, "Inverting the minimum signed byte retains its wrapped value.");

        bool rejected = false;
        try { PlayerMovementInput.Read(baseline, digital, new(false, 1, 0, false, false)); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "A nonzero correction with a zero divisor reports malformed input configuration.");
        read = PlayerMovementInput.Read(baseline, digital, new(false, 0, 0, false, false));
        assert(read.State.Axes == new PlayerAxes(-127, 127), "Disabled correction does not require a divisor.");
    }

    private static int[] Values(JsonElement fixture, string property) =>
        fixture.GetProperty(property).EnumerateArray().Select(value => value.GetInt32()).ToArray();
}

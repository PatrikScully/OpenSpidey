using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.UI;

internal static class PlayerLookaroundSetupTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundSetupTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-lookaround-setup.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var combat = new PlayerCombatState(100, 100) { HeadingTurnTicksRemaining = 77 };
            var angles = new PlayerLookaroundState(combat) { Active = input[23] != 0, EntranceTicks = (int)input[24], ExitTicks = (int)input[25],
                PreviousHeading = (short)input[27], Pitch = (int)input[30], Yaw = (int)input[31], SmoothedPitch = (int)input[32], SmoothedYaw = (int)input[33],
                NeckPitch = (short)input[34], NeckYaw = (short)input[35], HeadPitch = (short)input[36], HeadYaw = (short)input[37], TransitionFade = 13 };
            FixedVector3 Vec(int first) => new((int)input[first], (int)input[first + 1], (int)input[first + 2]);
            FixedQuaternion Quat(int first) => new((int)input[first], (int)input[first + 1], (int)input[first + 2], (int)input[first + 3]);
            var state = new PlayerLookaroundTransitionState(angles) { Offset = Vec(11), EntryCameraPosition = Vec(14),
                LastCameraPosition = Vec(17), ExitCameraPosition = Vec(20), Path = Enumerable.Repeat(Quat(4), 24).ToArray() };
            var host = new Host(input, angles);
            PlayerLookaroundCameraSetupResult result = PlayerLookaroundCameraSetup.Step(state, Vec(8), Quat(0).ToMatrix(), input[29] != 0, (int)input[26], host);
            long[] actual = [angles.Active ? 1 : 0, angles.EntranceTicks, angles.ExitTicks, angles.PreviousHeading, angles.Pitch, angles.Yaw,
                angles.SmoothedPitch, angles.SmoothedYaw, angles.NeckPitch, angles.NeckYaw, angles.HeadPitch, angles.HeadYaw, angles.HeadingTurnTicksRemaining, angles.TransitionFade,
                state.Anchor.X, state.Anchor.Y, state.Anchor.Z, state.LastCameraPosition.X, state.LastCameraPosition.Y, state.LastCameraPosition.Z,
                host.CameraPosition.X, host.CameraPosition.Y, host.CameraPosition.Z,
                state.CurrentLookRotation.X, state.CurrentLookRotation.Y, state.CurrentLookRotation.Z, state.CurrentLookRotation.W,
                host.CameraRotation.X, host.CameraRotation.Y, host.CameraRotation.Z, host.CameraRotation.W, state.Path is null ? 0 : 1];
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Original PC lookaround setup[{string.Join(',', input)}]: actual matrix/quat pipeline, stored/adjusted anchor, entry/exit position blends and camera clearance grid; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] expectedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(expectedEvents),
                $"Original PC camera setup[{string.Join(',', input)}] retains screen-target disable, heading read, typed path disposal and every actual line query/length/hit in source order; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', expectedEvents)}].");
            int stores = input[23] != 0 && (input[24] > input[26] || input[25] != 0) ? 1 : 0;
            assert(host.Screen.WideScreenValue == expected[13].GetInt32() && host.Screen.WideScreenShadow == expected[13].GetInt32() &&
                host.ScreenStores == stores,
                "The source setup writes both canonical screen globals only in actual entrance/exit branches, before its heading callback; inactive and steady states preserve authored screen values.");
            assert(result.SelectTarget == (angles.Active && angles.EntranceTicks == 0 && angles.ExitTicks == 0),
                "Only genuine steady-state setup reaches the separately owned target/action selection slice.");
        }
        VerifyExplicitExit(assert);
    }

    private static void VerifyExplicitExit(Action<bool, string> assert)
    {
        var angles = new PlayerLookaroundState(new(100, 100)) { Active = true, EntranceTicks = 24, TransitionFade = 19 };
        var state = new PlayerLookaroundTransitionState(angles) { Path = new FixedQuaternion[24] };
        var host = new ExitHost(state);
        PlayerLookaroundCameraSetup.Exit(state, host);
        assert(host.Events.SequenceEqual(new[] { "screen", "path", "pop", "behind", "target" }) &&
            host.Screen.WideScreenValue == 0 && host.Screen.WideScreenShadow == 0 && state.Path is null,
            "The active explicit source exit clears its shared screen before path disposal, camera pop/behind and target hiding.");
        host.Screen.Set(37); host.Events.Clear();
        PlayerLookaroundCameraSetup.Exit(state, host);
        assert(host.Events.Count == 0 && host.Screen.WideScreenValue == 37 && host.Screen.WideScreenShadow == 37,
            "An inactive explicit lookaround exit must preserve an authored42B9/195 screen value and emit no camera or screen side effect.");
    }

    private sealed class Host(long[] input, PlayerLookaroundState angles) : IPlayerLookaroundCameraSetupHost
    {
        private int queries, headingReads;
        public CinematicScreenState Screen { get; } = new() { WideScreenValue = 13, WideScreenShadow = 13 };
        public int ScreenStores { get; private set; }
        public List<long> Events { get; } = [];
        public FixedVector3 CameraPosition { get; private set; }
        public FixedQuaternion CameraRotation { get; private set; }
        public short EffectiveHeading { get { headingReads++; Events.Add(2); return (short)input[28]; } }
        public void SetCinematicScreen(int value)
        {
            if (headingReads != 0 || value != angles.TransitionFade)
                throw new InvalidDataException("Source lookaround screen stores must observe the updated transition fade before any heading read.");
            Screen.WideScreenValue = Screen.WideScreenShadow = value; ScreenStores++;
        }
        public void SetTargetVisible(bool visible) => Events.AddRange([1, visible ? 1 : 0]);
        public void DeleteTransitionPath(FixedQuaternion[] path) => Events.Add(5);
        public void SetDesiredCameraRotation(FixedQuaternion rotation) => CameraRotation = rotation;
        public void SetCameraPosition(FixedVector3 position) => CameraPosition = position;
        public PlayerLookaroundCameraTrace TraceWorld(FixedVector3 start, FixedVector3 end)
        {
            int length = PlayerLookaroundCameraSetup.SourceLineLength(start, end);
            bool hit = queries < 64 && ((unchecked((ulong)input[38]) >> queries) & 1) != 0; queries++;
            Events.AddRange([4, start.X, start.Y, start.Z, end.X, end.Y, end.Z, length, hit ? 1 : 0, 1, 0]);
            return new(hit, (int)input[39], length, start + (end - start) / 2);
        }
    }

    private sealed class ExitHost(PlayerLookaroundTransitionState state) : IPlayerLookaroundExitHost
    {
        public List<string> Events { get; } = [];
        public CinematicScreenState Screen { get; } = new() { WideScreenValue = 19, WideScreenShadow = 19 };
        public void SetCinematicScreen(int value)
        {
            if (state.Angles.Active || value != 0) throw new InvalidDataException("The source exit clears its player gate before the screen globals.");
            Screen.WideScreenValue = Screen.WideScreenShadow = value; Events.Add("screen");
        }
        public void DeleteTransitionPath(FixedQuaternion[] path)
        {
            if (Screen.WideScreenValue != 0 || Screen.WideScreenShadow != 0) throw new InvalidDataException("Source exit must clear the screen before path disposal.");
            Events.Add("path");
        }
        public void PopCameraMode() => Events.Add("pop");
        public void PutCameraBehind(int argument)
        {
            if (argument != 0) throw new InvalidDataException("Source lookaround exit must use PutCameraBehind0.");
            Events.Add("behind");
        }
        public void SetTargetVisible(bool visible)
        {
            if (visible) throw new InvalidDataException("Source exit hides the target.");
            Events.Add("target");
        }
    }
}

using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class ActorCameraShakeCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorCameraShakeCommandTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-camera-shake.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            byte[] bytes = new byte[4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)input[0]));
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 77);
            var cursor = new ActorScriptCursor(bytes); int reads = 0;
            var host = new Host(input[2] != 0, cursor)
            {
                State = new() { Amplitude = new(unchecked((short)input[6]), unchecked((short)input[7]), unchecked((short)input[8])),
                    Decay = new(unchecked((byte)input[6]), unchecked((byte)input[7]), unchecked((byte)input[8])),
                    Speed = new(unchecked((short)input[6]), unchecked((short)input[7]), unchecked((short)input[8])) }
            };
            FixedVector3 position = new(unchecked((int)input[3]), unchecked((int)input[4]), unchecked((int)input[5]));
            bool result = ActorCameraShakeCommand.TryExecute(0x4298, cursor, (variable, _) =>
            {
                reads++; assert(variable is 0x212A or 0x2139, "4298 oracle only uses genuine source node/difficulty getters.");
                return unchecked((short)input[1]);
            }, position, host);
            assert(result && expected.GetProperty("result").GetInt32() == 1 && cursor.Position == expected.GetProperty("consumed").GetInt32() &&
                reads == expected.GetProperty("reads").GetInt32() && host.Calls == expected.GetProperty("calls").GetInt32() &&
                host.Mode == expected.GetProperty("mode").GetInt32() && host.Entry == expected.GetProperty("entry").GetInt32() &&
                new[] { host.Position.X, host.Position.Y, host.Position.Z }.SequenceEqual(expected.GetProperty("position").EnumerateArray().Select(value => value.GetInt32())),
                $"Actual PC4298 case{index} resolves variable operands before the absent-camera guard, consumes the same cursor before native callback, maps0/1/2/other and passes the original actor position unchanged.");
            CameraShakeState state = host.State;
            int[] profile = [state.Amplitude.X, state.Amplitude.Y, state.Amplitude.Z,
                state.Decay.X, state.Decay.Y, state.Decay.Z, state.Speed.X, state.Speed.Y, state.Speed.Z];
            assert(profile.SequenceEqual(expected.GetProperty("profile").EnumerateArray().Select(value => value.GetInt32())),
                $"Actual source CCamera::Shake case{index} keeps missing-camera state or writes all three native amplitude/decay/speed fields exactly; actual=[{string.Join(',', profile)}].");
            index++;
        }
    }

    private sealed class Host(bool cameraPresent, ActorScriptCursor cursor) : IActorCameraShakeHost
    {
        public bool CameraPresent => cameraPresent;
        public CameraShakeState State { get; init; } = null!;
        public int Calls { get; private set; }
        public int Mode { get; private set; } = -1;
        public int Entry { get; private set; } = -1;
        public FixedVector3 Position { get; private set; }
        public void ShakeCamera(FixedVector3 position, int sourceMode)
        { Calls++; Position = position; Mode = sourceMode; Entry = cursor.Position; State.Shake(position, sourceMode); }
    }
}

using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

internal static class CinematicActorTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CinematicActorTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.cinematic-actors.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            JsonElement expected = test.GetProperty("expected");
            if (mode == "M")
            {
                int[] input = Words(test.GetProperty("input"));
                ActorBodyMotion body = new() { Position = Vector(input, 2), Velocity = Vector(input, 5),
                    Acceleration = Vector(input, 8), Angles = Angles(input, 11), AngularVelocity = Angles(input, 14),
                    AngularAcceleration = Angles(input, 17), AngularDamping = new((byte)input[20], (byte)input[21], (byte)input[22]) };
                CinematicActorMotion.Advance(body, input[0], input[1]);
                int[] actual = Snapshot(body), reference = Words(expected);
                assert(actual.SequenceEqual(reference), $"Actual PC Black Cat two-tick physics case {index}: actual=[{string.Join(',', actual)}], expected=[{string.Join(',', reference)}].");
            }
            else if (mode == "T")
            {
                int[] input = Words(test.GetProperty("input"));
                ActorBodyMotion body = new() { Angles = Angles(input, 0), AngularVelocity = Angles(input, 6), AngularAcceleration = Angles(input, 9) };
                CinematicActorMotion.TurnTowards(body, new((short)input[3], (short)input[4]), input[12]);
                int[] output = [body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
                    body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];
                assert(output.SequenceEqual(Words(expected)), $"Actual PC Utils_TurnTowards wrapping and retained aligned acceleration case {index}.");
            }
            else
            {
                int[] words = Words(test.GetProperty("program"));
                byte[] program = new byte[words.Length * 2];
                for (int word = 0; word < words.Length; word++)
                    BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(word * 2), unchecked((short)words[word]));
                Host host = new();
                CinematicActorScriptRuntime runtime = new(new ActorScriptCursor(program), host, CinematicActorScriptProfile.BlackCat);
                runtime.Begin();
                int frameIndex = 0;
                foreach (JsonElement frame in test.GetProperty("frames").EnumerateArray())
                {
                    int[] input = Words(frame);
                    host.Body.Position = Vector(input, 4); host.AnimationFinished = input[2] != 0;
                    host.SignalFlags = (ushort)input[3]; host.Events.Clear();
                    runtime.Update(input[0], input[1] != 0);
                    JsonElement row = expected[frameIndex++];
                    int[] scalar = [runtime.Running ? 1 : 0, runtime.Decoding ? 1 : 0, runtime.ElapsedTicks,
                        runtime.Cursor, runtime.LookTargetNode, runtime.CheckpointNode, host.ItemFlags,
                        host.AnimationId, host.AnimationFinished ? 1 : 0, host.SignalFlags];
                    int[] expectedScalar = row.EnumerateArray().Take(10).Select(value => value.GetInt32()).ToArray();
                    assert(scalar.SequenceEqual(expectedScalar) && Snapshot(host.Body).SequenceEqual(Words(row[10])) &&
                        runtime.CommandTypes.SequenceEqual(Words(row[11])) && host.Events.SequenceEqual(Words(row[12])),
                        $"Actual PC cinematic case {index}, frame {frameIndex}: scalar=[{string.Join(',', scalar)}] expected=[{string.Join(',', expectedScalar)}]; " +
                        $"motion=[{string.Join(',', Snapshot(host.Body))}] expected=[{string.Join(',', Words(row[10]))}]; " +
                        $"commands=[{string.Join(',', runtime.CommandTypes)}] expected=[{string.Join(',', Words(row[11]))}]; " +
                        $"events=[{string.Join(',', host.Events)}] expected=[{string.Join(',', Words(row[12]))}].");
                }
            }
            index++;
        }
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
    private static FixedVector3 Vector(int[] words, int offset) => new(words[offset], words[offset + 1], words[offset + 2]);
    private static ActorAngles Angles(int[] words, int offset) => new(unchecked((short)words[offset]), unchecked((short)words[offset + 1]), unchecked((short)words[offset + 2]));
    private static int[] Snapshot(ActorBodyMotion body) => [body.Position.X, body.Position.Y, body.Position.Z,
        body.Velocity.X, body.Velocity.Y, body.Velocity.Z, body.Acceleration.X, body.Acceleration.Y, body.Acceleration.Z,
        body.Angles.X, body.Angles.Y, body.Angles.Z, body.AngularVelocity.X, body.AngularVelocity.Y, body.AngularVelocity.Z,
        body.AngularAcceleration.X, body.AngularAcceleration.Y, body.AngularAcceleration.Z];

    private sealed class Host : ICinematicActorScriptHost
    {
        public ActorBodyMotion Body { get; } = new();
        public int SurfaceClearance => 100;
        public int AnimationId { get; private set; } = 12;
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public ushort SignalFlags { get; set; }
        public List<int> Events { get; } = [];
        public FixedVector3 TriggerPosition(int node) { Events.AddRange([1, node]); return new(node * 400 * 4096, node * -80 * 4096, node * 130 * 4096); }
        public IReadOnlyList<ushort> TriggerLinks(int node) { Events.AddRange([2, node]); return new ushort[] { 7 }; }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags)
        { Events.AddRange([3, position.X, position.Y, position.Z, above, below, flags]); return 160 * 4096; }
        public void RunAnimation(int animation) { Events.AddRange([4, animation, 0, -1]); AnimationId = animation; AnimationFinished = false; }
        public void CycleAnimation(int animation) { Events.AddRange([5, animation, 1]); AnimationId = animation; AnimationFinished = false; }
        public void PlayVoice(short bank, short track, short priority) => Events.AddRange([6, bank, track, priority]);
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source)
        {
            if (source != CinematicActorSignalSource.InputFlags) throw new InvalidDataException("Actual PC Black Cat wait15 consumes input44 rather than body46 flags.");
            if ((SignalFlags & 1) == 0) return false;
            SignalFlags &= 0xFFFE; return true;
        }
    }
}

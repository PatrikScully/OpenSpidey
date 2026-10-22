using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Mathematics;

internal static class VenomTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VenomTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.venom.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, count = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "C") Construct(assert, index, input, expected);
            else if (mode == "W") Wait(assert, index, input, expected);
            else ResolveSwitch(assert, index, input, expected);
            index++; count++;
        }
        assert(count == 30, "Venom fixture keeps 24 constructor, 5 wait and 1 ResolveSwitchNodes case.");
        assert(VenomState.ModuleName == "venom" && VenomState.Type == 313, "Source module name stays tied to type313.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        VenomState state = new(input[1], input[0], input[2], 0);
        Host host = new();
        VenomBrain.Construct(state, host, () => { });
        string model = VenomState.ModelNameFor(input[0]);
        int[] actual = [VenomState.Type, state.MinorRadius, state.LevelId, state.AiState, state.Substate, state.Health,
            state.Field39C, unchecked((int)state.Field218), model == "venom2" ? 1 : 0, state.SourceNode];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CVenom constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void ResolveSwitch(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        ResolveHost host = new();
        VenomState state = new(1, 0x604, 0, 0) { ResolveSwitchNodesPending = true };
        VenomResolveSwitchNodes.Resolve(state, host);
        int[] actual = [state.Field3B8, state.SwitchNodeCount, state.SwitchNodeIndices[0]];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CVenom ResolveSwitchNodes case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Wait(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        VenomState state = new(1, 0x501, 2, 0) { AiState = input[1], Substate = input[2], Field3B0 = 11, Field3B4 = 22 };
        if (input[0] != 0) VenomWaitState.Enter(state);
        else VenomWaitState.Exit(state, unchecked((ushort)input[3]), unchecked((ushort)input[4]));
        int[] actual = [state.AiState, state.Substate, state.Field3B0, state.Field3B4];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CVenom wait case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class ResolveHost : IVenomHost
    {
        private readonly TriggerNode[] nodes =
        [
            new(0, 0, 0, ReadOnlyMemory<byte>.Empty),
            new(1, 1002, 0, Pack(1002, 4, 1, 2)),
            new(2, 5, 0, Pack(5, 0, 0, 0, 10, 20, 30)),
            new(3, 1002, 0, Pack(1002, 9, 0, 0))
        ];
        public int TriggerNodeCount => nodes.Length;
        public TriggerNode TriggerNode(int node) => nodes[node];
        public ushort TriggerNodeTypeCode(int node) => nodes[node].TypeCode;
        public short TriggerNodeSubtype(int node) => BinaryPrimitives.ReadInt16LittleEndian(nodes[node].Data.Span[2..]);
        public IEnumerable<VenomControlSwitch> EnumerateControlSwitches()
        {
            yield return new(new object(), new FixedVector3(10 << 12, 20 << 12, 30 << 12));
            yield return new(new object(), new FixedVector3(100 << 12, 20 << 12, 30 << 12));
        }
        public ActorBodyMotion Body { get; } = new();
        public int LevelId => 0x604;
        public int ElapsedTicks => 0;
        public int AnimationId => 0;
        public bool AnimationFinished => false;
        public ushort ItemFlags { get; set; }
        public int SurfaceClearance => 100;
        public FixedVector3 PlayerPosition { get; set; }
        public FixedVector3 PrimaryAimTarget { get; set; }
        public FixedVector3 SecondaryAimTarget { get; set; }
        public FixedVector3? MechListPosition => null;
        public bool FastForwardRequested => false;
        public int DifficultyLevel => 0;
        public int Random(int bound) => 0;
        public void PlayPositionalSound(int sound) { }
        public void RunAnimation(int animation) { }
        public void CycleAnimation(int animation) { }
        public FixedVector3 TriggerPosition(int node)
        {
            if (node == 2) return new FixedVector3(10 << 12, 20 << 12, 30 << 12);
            return default;
        }
        public IReadOnlyList<ushort> TriggerLinks(int node) => [];
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => 0;
        public void PlayVoice(short bank, short track, short priority) { }
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source) => false;
        public int AnimationFrame => 0;
        public int TimerRelated => 0;
        public int LastFootstepSound { get; set; }
        public ThugMessageQueue Messages { get; } = new();
        public void PlayPositionSpeech(int bank, int track, FixedVector3 position) { }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to) => false;
        public int SwitchState(object? control) => 0;
        public void FlickSwitch(object? control) { }
        public void SetPlayerFocusLockTarget() { }
        public void CreateCompass(FixedVector3 position) { }
        public void SendPulseToNode(int node) { }
        public void AdjustWaterModel(int waterLevel) { }
        public void Die() { }
        public void BuildTransform() { }
        private static byte[] Pack(params short[] words)
        {
            byte[] bytes = new byte[words.Length * 2];
            for (int index = 0; index < words.Length; index++)
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), words[index]);
            return bytes;
        }
    }

    private sealed class Host : IVenomHost
    {
        public ActorBodyMotion Body { get; set; } = new();
        public int TriggerNodeCount => 1;
        public TriggerNode TriggerNode(int node) => new((ushort)node, 1, 0, ReadOnlyMemory<byte>.Empty);
        public ushort TriggerNodeTypeCode(int node) => 1;
        public short TriggerNodeSubtype(int node) => 0;
        public IEnumerable<VenomControlSwitch> EnumerateControlSwitches() => [];
        public int LevelId => 0x501;
        public int ElapsedTicks { get; set; } = 1;
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public int SurfaceClearance => 100;
        public FixedVector3 PlayerPosition { get; set; } = new(4096, 0, 4096);
        public FixedVector3 PrimaryAimTarget { get; set; } = new(8192, 0, 8192);
        public FixedVector3 SecondaryAimTarget { get; set; } = new(8192, 0, 8192);
        public FixedVector3? MechListPosition => null;
        public bool FastForwardRequested => false;
        public int DifficultyLevel => 0;
        public int Random(int bound) => bound <= 0 ? 0 : 1;
        public void PlayPositionalSound(int sound) { }
        public void RunAnimation(int animation) => AnimationId = animation;
        public void CycleAnimation(int animation) => AnimationId = animation;
        public FixedVector3 TriggerPosition(int node) => default;
        public IReadOnlyList<ushort> TriggerLinks(int node) => [];
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => 0;
        public void PlayVoice(short bank, short track, short priority) { }
        public bool ConsumeAnimationSignal(CinematicActorSignalSource source) => false;
        public int AnimationFrame => 0;
        public int TimerRelated => 0;
        public int LastFootstepSound { get; set; }
        public ThugMessageQueue Messages { get; } = new();
        public void PlayPositionSpeech(int bank, int track, FixedVector3 position) { }
        public bool LineOfSight(FixedVector3 from, FixedVector3 to) => false;
        public int SwitchState(object? control) => 0;
        public void FlickSwitch(object? control) { }
        public void SetPlayerFocusLockTarget() { }
        public void CreateCompass(FixedVector3 position) { }
        public void SendPulseToNode(int node) { }
        public void AdjustWaterModel(int waterLevel) { }
        public void Die() { }
        public void BuildTransform() { }
    }
}

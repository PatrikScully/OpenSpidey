using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Mathematics;

internal static class RhinoTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(RhinoTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.rhino.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0, counts = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            string mode = test.GetProperty("mode").GetString()!;
            int[] input = Words(test.GetProperty("input"));
            int[] expected = Words(test.GetProperty("expected"));
            if (mode == "C") Construct(assert, index, input, expected);
            else if (mode == "I") Init(assert, index, input, expected);
            else Flags(assert, index, input, expected);
            index++; counts++;
        }
        assert(counts == 48, "Rhino fixture keeps all recovered constructor, RhinoInit and original CheckStateFlags cases.");
        assert(RhinoState.ModuleName == "rhino" && RhinoState.Type == 307 && RhinoState.ModelName == "rhino",
            "Source module name and type307 stay tied to the relocatable Rhino factory.");
        assert(RhinoState.SetupWord0 == 0x201 && RhinoState.SetupWord1 == 0,
            "gRhinoStrangeInitData at 0x5520B8 seeds movement/idle animation words 0x201/0.");
    }

    private static void Construct(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        RhinoState state = new(input[0], input[1], 0);
        Host host = new() { AttackClock = input[1] };
        int randomCalls = 0;
        RhinoBrain.Construct(state, host, () => { }, bound => { randomCalls++; return bound <= 0 ? 0 : 1; });
        int[] actual =
        [
            RhinoState.Type, state.SurfaceClearance, state.AttackRelatedSnapshot, state.MinorRadius, state.Field216, state.PushValue,
            state.SourceNode, state.State, state.Substate, state.Field344, (int)state.BehaviorFlags, state.Health,
            unchecked((int)state.MovementAnimations), unchecked((int)state.IdleAnimations)
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CRhino constructor case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
        assert(randomCalls == RhinoState.DazedStarCount && state.DazedStarSpeeds.All(speed => speed == 1),
            $"Constructor case {index} fills five dazed-star speeds via Rnd(4096).");
        assert(host.ItemFlags == 0x480, $"Constructor case {index} sets item flags 0x480 before script parse.");
    }

    private static void Init(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        RhinoState state = new(1, input[1], 0);
        Host host = new() { AttackClock = input[1], Ground = input[0] };
        RhinoBrain.Construct(state, host, () => { }, _ => 0);
        var brain = new RhinoBrain(state, new RhinoStateFlags(), host);
        brain.Advance();
        // Case1 only flips bothFlags to ChargePlayer; the next AI frame enters ChargePlayer case0.
        if (input[0] != -1)
        {
            assert(state.State == 0 && state.Substate == 1, $"RhinoInit case {index} must settle on the ground before charge.");
            state.State = 8;
            state.Substate = 0;
        }
        int[] actual =
        [
            host.Body.Position.Y, state.State, state.Substate, state.ShadowPosition.Y, state.ShadowGroundY, state.Field358
        ];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CRhino::RhinoInit case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static void Flags(Action<bool, string> assert, int index, int[] input, int[] expected)
    {
        var flags = new RhinoStateFlags();
        ushort value = flags.Check(input[0]);
        int[] actual = [value, flags.CachedIndex];
        assert(actual.SequenceEqual(expected),
            $"Actual PC CBaddy::CheckStateFlags(rhino 0x5521D0) case {index}: actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
    }

    private static int[] Words(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private sealed class Host : IRhinoHost
    {
        public ActorBodyMotion Body { get; }
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks { get; set; } = 1;
        public int AttackClock { get; set; }
        public FixedVector3 PlayerPosition { get; set; }
        public int PlayerInputSuppression { get; set; } = 1; // Keep boss-camera leave quiet in RhinoInit fixtures.
        public bool HasMessages { get; set; }
        public bool PlayerIsCrawling => false;
        public int Difficulty => 1;
        public int WebDomeCount => 0;
        public int Ground { get; set; } = -1;
        public Host()
        {
            Body = new();
            Actor = new(Body);
        }
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => Ground;
        public void SetHeight(bool initial, int above, int below) { }
        public void RunAnimation(int animation) => AnimationId = animation;
        public void Neutralize() { }
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void PlaySounds() { }
        public void DoPhysics(int mode) { }
        public void SetGlobalBoss(bool enabled) { }
        public void SetBossCameraMode() { }
        public int Random(int bound) => 0;
        public ActorBodyMotion? RecoverPlayerBody() => null;
        public void AttachPlayerLookAt(uint completionMask, int rate, int tolerance) { }
        public void PlayPositionalSound(int id) { }
        public bool LineOfSightToPlayer(int argument) => true;
        public RhinoWallChunkLists WallChunks { get; } = new();
        public bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact)
        {
            contact = default;
            return false;
        }
        public void ChunkItemByChecksum(uint checksum) { }
        public void RewindChargePosition(FixedVector3 position) { }
        public void NotifyChargeBarrierVisible(object? collider) { }
        public void ApplyChargeBarrierCollision(int mode) { }
        public void ApplyChargeScrapeDamage() { }
        public void BeginStuckHorn(RhinoChargeWallContact contact, int shake) { }
        public bool IsPlayerBody(object? collider) => false;
        public bool TryDetermineFightState(RhinoState state, int argument) => false;
        public bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius) => false;
        public void HitPlayerFromCharge(FixedVector3 delta) { }
        public ReadOnlySpan<RhinoSpeechEntry> SpeechEntries => ReadOnlySpan<RhinoSpeechEntry>.Empty;
        public bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority) => false;
        public bool PlayerCanTalkNow() => true;
        public void AttachRhinoSpeech(int bank, int slot) { }
        public void AttachPlayerSpeech(int bank, int slot) { }
    }
}

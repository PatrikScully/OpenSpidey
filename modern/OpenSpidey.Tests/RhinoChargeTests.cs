using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Mathematics;

internal static class RhinoChargeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(RhinoChargeTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.rhino-charge.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var host = new Host(test.GetProperty("input"));
            new RhinoBrain(host.State, new RhinoStateFlags(), host).ChargePlayer();
            long[] actual = host.Snapshot();
            long[] expected = Numbers(test.GetProperty("expected").GetProperty("snapshot"));
            assert(actual.SequenceEqual(expected),
                $"ChargePlayer case{index}: snapshot actual=[{string.Join(',', actual)}] expected=[{string.Join(',', expected)}].");
            JsonElement events = test.GetProperty("expected").GetProperty("events");
            assert(host.Events.Count == events.GetArrayLength(),
                $"ChargePlayer case{index}: event count {host.Events.Count} vs {events.GetArrayLength()}.");
            for (int eventIndex = 0; eventIndex < host.Events.Count; eventIndex++)
            {
                long[] want = Numbers(events[eventIndex]);
                assert(host.Events[eventIndex].SequenceEqual(want),
                    $"ChargePlayer case{index} event{eventIndex}: actual=[{string.Join(',', host.Events[eventIndex])}] expected=[{string.Join(',', want)}].");
            }
            index++;
        }
        assert(index >= 18, "Rhino ChargePlayer fixture keeps the original-exe case0/case2 corpus.");
    }

    private static long[] Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64()).ToArray();

    private sealed class Host : IRhinoHost
    {
        private readonly ActorBodyMotion playerBody = new();
        private int randomSeed;
        public RhinoState State { get; }
        public ActorBodyMotion Body { get; }
        public ActorProcedureState Actor { get; }
        public ActorProcedureList Procedures { get; } = new();
        public List<long[]> Events { get; } = [];
        public int AnimationId { get; set; }
        public bool AnimationFinished { get; set; }
        public ushort ItemFlags { get; set; }
        public int ElapsedTicks { get; }
        public int AttackClock { get; set; }
        public FixedVector3 PlayerPosition { get; }
        public int PlayerInputSuppression => 1;
        public bool HasMessages => false;
        public bool PlayerIsCrawling { get; }
        public int Difficulty => 1;
        public int WebDomeCount => 0;

        public Host(JsonElement input)
        {
            long[] values = Numbers(input);
            Body = new() { Position = new(unchecked((int)values[10]), unchecked((int)values[11]), unchecked((int)values[12])) };
            Actor = new(Body) { CompletionFlags = unchecked((uint)values[6]) };
            State = new(1, 0, 0)
            {
                State = unchecked((int)values[0]),
                Substate = unchecked((int)values[1]),
                CombatFlags = unchecked((uint)values[7]),
                Field330 = unchecked((int)values[8]),
                Field334 = unchecked((int)values[9])
            };
            ElapsedTicks = unchecked((int)values[2]);
            AnimationFinished = values[3] != 0;
            AnimationId = unchecked((int)values[4]);
            PlayerIsCrawling = values[5] != 0;
            PlayerPosition = new(unchecked((int)values[13]), unchecked((int)values[14]), unchecked((int)values[15]));
            playerBody.Position = PlayerPosition;
            randomSeed = unchecked((int)values[16]);
        }

        public long[] Snapshot() =>
        [
            State.State, State.Substate, State.Field310, State.Field330, State.Field334,
            Actor.CompletionFlags, State.CombatFlags, AnimationId, AnimationFinished ? 1 : 0
        ];

        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => -1;
        public void SetHeight(bool initial, int above, int below) { }
        public void RunAnimation(int animation)
        {
            Events.Add([5, animation, 0, -1]);
            AnimationId = animation;
            AnimationFinished = false;
        }
        public void Neutralize() => Events.Add([3]);
        public void BuildTransform() { }
        public void ProcessMessages() { }
        public void PlaySounds() { }
        public void DoPhysics(int mode) => Events.Add([7, mode]);
        public void SetGlobalBoss(bool enabled) { }
        public void SetBossCameraMode() { }
        public int Random(int bound)
        {
            Events.Add([1, bound, randomSeed]);
            int value = bound <= 0 ? 0 : randomSeed % bound;
            randomSeed++;
            return value;
        }
        public ActorBodyMotion? RecoverPlayerBody() => playerBody;
        public void AttachPlayerLookAt(uint completionMask, int rate, int tolerance)
        {
            Events.Add([2, 64]);
            Events.Add([4, unchecked((int)completionMask), rate, tolerance, 1, 0]);
            Procedures.Attach(ActorLookAtProcedure.ForBody(Actor, RecoverPlayerBody, completionMask, rate, tolerance));
        }
        public void PlayPositionalSound(int id) => Events.Add([6, id, 0]);
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

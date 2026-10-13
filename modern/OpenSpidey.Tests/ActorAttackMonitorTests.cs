using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Combat;

internal static class ActorAttackMonitorTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorAttackMonitorTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-attack-monitor.json")
            ?? throw new InvalidDataException("Attack monitor source fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var actor = new ActorProcedureState(new ActorBodyMotion { Position = Vector(test.GetProperty("position")) })
            {
                CompletionFlags = test.GetProperty("actorCompletionFlags").GetUInt32()
            };
            var host = new Host { Position = actor.Body.Position, PlayerPosition = Vector(test.GetProperty("player")) };
            var procedure = new ActorAttackMonitorProcedure(actor, host, test.GetProperty("start").GetInt32(),
                test.GetProperty("mask").GetUInt32(), test.GetProperty("length").GetInt32(),
                test.GetProperty("completionMask").GetUInt32())
            {
                ExecuteNext = () => host.Events.Add(7)
            };
            JsonElement expected = test.GetProperty("expected");
            long[] initial = [procedure.AnimationId, procedure.StartFrame, procedure.EndFrame,
                procedure.HookMask, actor.CompletionFlags];
            assert(initial.SequenceEqual(Values(expected.GetProperty("initial"))) && procedure.SourceType == 1281,
                "Monitor captures the clip, inclusive source frame window, hook mask and cleared completion signal.");
            int stepIndex = 0;
            foreach (JsonElement step in test.GetProperty("steps").EnumerateArray())
            {
                int[] input = step.EnumerateArray().Select(value => value.GetInt32()).ToArray();
                host.AnimationId = input[0]; host.AnimationFrame = input[1]; host.DomeCount = input[2];
                host.Delta = new(input[3], input[4], input[5]); host.CollisionOrdinal = input[6];
                host.ReturnedPart = (ushort)input[7]; host.PlayerHealth = (short)input[8]; host.AttackDamage = (ushort)input[9];
                host.Events.Clear(); host.CollisionCalls = 0;
                procedure.Execute(input[10]);
                JsonElement wanted = expected.GetProperty("steps")[stepIndex++];
                long[] state = [procedure.Flags, procedure.CompletionMask, procedure.PreviousSampleValid ? 1 : 0,
                    actor.CompletionFlags, host.PlayerHealth];
                assert(state.SequenceEqual(Values(wanted.GetProperty("state"))),
                    "Monitor frame eligibility, clip cancellation, hit completion and death result match source updates independently of elapsed ticks.");
                assert(host.Events.SequenceEqual(Values(wanted.GetProperty("events"))),
                    "SmackSpidey samples hook bits in order, extends previous sweeps, forwards source scale and emits exact damage/effect/victory events.");
                FixedVector3[] points = wanted.GetProperty("points").EnumerateArray().Select(Vector).ToArray();
                assert(procedure.PreviousHookPositions.SequenceEqual(points),
                    "Attack monitor retains exact previous hook samples, including early-hit and dome-suppressed updates.");
            }
        }
    }

    private static long[] Values(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private static FixedVector3 Vector(JsonElement element)
    {
        int[] values = element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new(values[0], values[1], values[2]);
    }

    private sealed class Host : IActorAttackMonitorHost
    {
        public int AnimationId { get; set; } = 13;
        public int AnimationFrame { get; set; }
        public int DomeCount { get; set; }
        public ushort AttackDamage { get; set; }
        public FixedVector3 Position { get; init; }
        public FixedVector3 PlayerPosition { get; init; }
        public short PlayerHealth { get; set; }
        public FixedVector3 Delta { get; set; }
        public int CollisionOrdinal { get; set; }
        public int CollisionCalls { get; set; }
        public ushort ReturnedPart { get; set; }
        public List<long> Events { get; } = [];
        public FixedVector3 GetActorHookPosition(ActorHook hook)
        {
            FixedVector3 point = Position + Delta + new FixedVector3(hook.PartIndex * 4096,
                -hook.PartIndex * 2048, hook.PartIndex * 1024);
            Events.AddRange([1, hook.X, hook.Y, hook.Z, hook.PartIndex, point.X, point.Y, point.Z]);
            return point;
        }
        public bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int collisionScale, out ActorHook hitHook)
        {
            bool hit = CollisionCalls++ == CollisionOrdinal;
            hitHook = new(0, 0, 0, ReturnedPart);
            Events.AddRange([2, from.X, from.Y, from.Z, to.X, to.Y, to.Z, collisionScale, hit ? 1 : 0, ReturnedPart]);
            return hit;
        }
        public void CreateCombatImpactEffect(FixedVector3 position, int flags) => Events.AddRange([3, position.X, position.Y, position.Z, flags]);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => Events.AddRange([4, sound, position.X, position.Y, position.Z, volume]);
        public void HitPlayer(ActorHit hit)
        {
            Events.AddRange([5, hit.Flags, hit.PartIndex, hit.AttackKind, hit.Damage, hit.Direction.X, hit.Direction.Y, hit.Direction.Z]);
            PlayerHealth = unchecked((short)(PlayerHealth - hit.Damage));
        }
        public void Victorious() => Events.Add(6);
    }
}

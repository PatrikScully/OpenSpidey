using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

internal static class ScorpionConstructionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionConstructionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.scorpion-construction.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("constructors").EnumerateArray())
        {
            JsonElement expected = test.GetProperty("expected");
            short type = test.GetProperty("type").GetInt16();
            int ordinal = test.GetProperty("ordinal").GetInt32();
            ushort node = test.GetProperty("node").GetUInt16();
            assert(expected.GetProperty("allocated").GetInt32() == (type == 310 ? 0xC28 : 0x380),
                "Actual source factories request their original Scorpion/Jonah family allocation sizes.");
            if (test.GetProperty("accepted").GetInt32() == 0)
            {
                assert(expected.GetProperty("count").GetInt32() == ordinal && expected.GetProperty("state").GetArrayLength() == 0 &&
                    expected.GetProperty("events").GetArrayLength() == 0,
                    "The original rejected heap transport enters no CBaddy constructor or model/list/animation/script/panel callback.");
                continue;
            }
            var count = new BaddyConstructionCount(ordinal);
            using BaddyConstructionLease lease = count.BeginConstruction();
            ScorpionEncounterBodyState state = type == 310 ? new ScorpionState(lease.Ordinal) : new JonahState(lease.Ordinal);
            var actor = new ActorProcedureState(new());
            var host = new Host(state, actor, test.GetProperty("difficulty").GetInt32());
            bool cheat = test.GetProperty("cheat").GetInt32() != 0;
            ushort[] words = test.GetProperty("words").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            byte[] program = new byte[words.Length * 2];
            for (int word = 0; word < words.Length; word++) BinaryPrimitives.WriteUInt16LittleEndian(program.AsSpan(word * 2), words[word]);
            int[] position = test.GetProperty("position").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            short[] angles = test.GetProperty("angles").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            var definition = new TriggerActorDefinition(node, 1, type, Array.Empty<ushort>(), Array.Empty<byte>(),
                new(position[0], position[1], position[2]), new(angles[0], angles[1], angles[2]), 16, program);
            if (node == 0)
            {
                if (state is ScorpionState scorpion) ScorpionEncounterInitialization.ConstructDefaultScorpion(scorpion, actor, host);
                else ScorpionEncounterInitialization.ConstructDefaultJonah((JonahState)state, actor, host, cheat);
            }
            else
            {
                if (state is ScorpionState scorpion) ScorpionEncounterInitialization.ConstructScorpion(scorpion, actor, definition, host);
                else ScorpionEncounterInitialization.ConstructJonah((JonahState)state, actor, definition, host, cheat);
            }
            assert(host.Snapshot().SequenceEqual(Numbers(expected.GetProperty("state"))) &&
                count.Count == expected.GetProperty("count").GetInt32() && (host.Attached ? 1 : 0) == expected.GetProperty("head").GetInt32(),
                $"Whole PC encounter constructor {index} matches placement, body/type/node/health/damage/flags/setup, canonical animation, ordinal and tail-region initialization.");
            assert(host.Events.Count == expected.GetProperty("events").GetArrayLength() &&
                host.Events.Zip(expected.GetProperty("events").EnumerateArray()).All(pair => pair.First.SequenceEqual(Numbers(pair.Second))),
                $"Whole PC encounter constructor {index++} preserves every model/list/CycleAnim/ParseScript/health-bar callback and all45 source fields visible at each boundary.");
            if (state is ScorpionState tail)
                assert(tail.TailControlPoints.Length == 4 && tail.TailNodes.Length == 23 && tail.TailHistory.Length == 128 &&
                    tail.TailControlPoints.Concat(tail.TailNodes).Concat(tail.TailHistory).All(value => value == default),
                    "Scorpion owns the original four control points,23 tail nodes and128 history vectors initialized before its first source AI update.");
        }
        foreach (JsonElement test in document.RootElement.GetProperty("jonahModule").EnumerateArray())
        {
            var jonah = new JonahState(0) { State = test.GetProperty("state").GetInt32(), SignalFlags = test.GetProperty("signals").GetUInt32() };
            uint decision = JonahState.WhatShouldScorpionDo(jonah.State);
            jonah.ShouldPlead();
            assert(decision == test.GetProperty("expected")[0].GetUInt32() && jonah.SignalFlags == test.GetProperty("expected")[1].GetUInt32(),
                "Whole original Jonah module functions retain the state5/state8 decision and preserve existing signal flags when adding the pleaC mask.");
        }
    }

    private static IEnumerable<long> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
    private sealed class Host(ScorpionEncounterBodyState state, ActorProcedureState actor, int difficulty) :
        IScorpionEncounterInitializationHost, IActorScriptHost, IActorScriptVariableWorld
    {
        private ActorScriptVariables? variables;
        private short lightType;
        public List<long[]> Events { get; } = [];
        public bool Attached { get; private set; }
        public ActorScriptState ScriptState { get; private set; } = null!;
        public int Difficulty => difficulty;
        public int MovieInputPolls => 0;
        public FixedVector3? PlayerPosition => null;
        public void InitializeModel(string name) => Event(1, name switch { "scorpion" => 310, "jameson" => 316, "jjjj" => 999, _ => throw new InvalidDataException(name) });
        public void AssignSourceLight(short type) => lightType = type;
        public void AttachToSourceBaddyList() { Attached = true; Event(2); }
        public ushort ReadAnimationFrameCount(ushort clip) => clip == 2 ? (ushort)32 : throw new InvalidDataException("The genuine Scorpion constructor selects only clip2.");
        public void CycleAnimation(ushort clip, sbyte direction) => Event(3, clip, direction);
        public void ParseScript(ActorScriptCursor script)
        {
            Event(4);
            ScriptState = new() { ActorType = state.Type, NodeId = state.SourceNode };
            variables = new(ScriptState, this) { Damage = state.Damage };
            new ActorScriptRuntime(script, this).Start();
        }
        public void CreateHealthBar(short actorType) => Event(5, actorType);
        public short ReadVariable(ushort variable, ActorScriptCursor operands) => variables!.Read(variable, operands);
        public void WriteVariable(ushort variable, ActorScriptCursor operands)
        { variables!.Write(variable, operands); state.Damage = variables.Damage; }
        public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) => throw new InvalidDataException($"Unexpected constructor opcode{opcode:X4}.");
        public ActorScriptCursor ScriptAtNode(ushort node) => throw new InvalidOperationException();
        public bool PlayerCeilingCheck(short x, short z) => throw new InvalidOperationException();
        public void FlashCompass() => throw new InvalidOperationException();
        public void SendDeathPulse() => throw new InvalidOperationException();
        public void Retire() => throw new InvalidOperationException();
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => throw new InvalidOperationException();
        public int Random(int range) => throw new InvalidOperationException();
        public ushort FirstLink(ushort node) => throw new InvalidOperationException();
        public void SelectModel(uint checksum) => throw new InvalidOperationException();
        private void Event(int kind, params long[] arguments) => Events.Add(new long[] { kind }.Concat(arguments).Concat(Snapshot()).ToArray());
        public long[] Snapshot()
        {
            var motion = actor.Body;
            var animation = state.Animation;
            var scorpion = state as ScorpionState;
            return [motion.Position.X, motion.Position.Y, motion.Position.Z, motion.Angles.X, motion.Angles.Y, motion.Angles.Z,
                state.ItemFlags, state.BodyFlags, state.Type, state.SourceNode, state.CurrentWaypoint, state.MinorRadius, state.FieldF4,
                state.SurfaceClearance, state.GeneralParameter, state.PushValue, state.Health, state.Damage, state.WaitUpdates, state.State,
                state.Substate, state.BehaviorFlags, state.MovementAnimations, state.IdleAnimations,
                animation.ClipId, animation.Frame, animation.Fraction, animation.Direction, (byte)animation.Mode, animation.FrameCount,
                animation.TargetFrame, animation.Finished, animation.Speed, state.ActorPhase, lightType, scorpion?.CollisionPartMask ?? 0,
                scorpion?.TailGeometryRegion ?? 0, scorpion?.TailGlowRegion ?? 0, scorpion?.BossCameraEnabled ?? 0,
                motion.LinearDamping.X, motion.LinearDamping.Y, motion.LinearDamping.Z,
                motion.AngularDamping.X, motion.AngularDamping.Y, motion.AngularDamping.Z];
        }
    }
}

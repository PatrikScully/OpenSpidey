using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Private constructor/model proof; it does not register boss actors or substitute encounter AI.</summary>
internal static class ScorpionConstructionImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive, ReadOnlyMemory<byte> originalImage)
    {
        var scope = new Node3D { Name = "SourceScorpionConstructorProof" }; owner.AddChild(scope);
        var destination = new Node3D { Name = "SourceEncounterReparentProof" }; owner.AddChild(destination);
        var resources = new GodotScorpionEncounterResources(archive, new(archive), originalImage);
        var lighting = new SourceVertexLightingState();
        var nativeOwners = new List<GodotScorpionEncounterModel>();
        var nativeResources = new List<Resource>();
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/L2A2_T.trg"));
        try
        {
            TriggerActorDefinition scorpionDefinition = TriggerActorDefinition.Read(triggers.Nodes[1]);
            var scorpion = new ScorpionState(0);
            var scorpionMotion = new ActorProcedureState(new());
            var scorpionHost = new Host(scope, resources, lighting, scorpion, Capture);
            ScorpionEncounterInitialization.ConstructScorpion(scorpion, scorpionMotion, scorpionDefinition, scorpionHost, triggers.Nodes[1].SourceOffset);
            Assert(scorpion.Type == 310 && scorpion.SourceNode == 1 && scorpion.Health == 900 && scorpion.Damage == 15 &&
                scorpion.Animation.ClipId == 2 && scorpion.Animation.FrameCount == 32 && scorpion.Animation.Finished == 0 &&
                scorpionHost.Model.Model.Playback == scorpion.Animation && scorpionHost.PanelRequests.SequenceEqual(new short[] { 310 }) &&
                scorpionHost.Events.SequenceEqual(new[] { "model:scorpion", "light:310", "attach", "cycle:2:1", "script", "panel:310" }),
                "Unchanged L2A2 node1 must establish source900 health, authored2103 damage15, real clip2/CycleAnim and the original constructor callback order.");
            VerifyBank(scorpionHost.Model, scorpionMotion, scorpion.ItemFlags, 24, 30);

            TriggerActorDefinition jonahDefinition = TriggerActorDefinition.Read(triggers.Nodes[2]);
            var jonah = new JonahState(1);
            var jonahMotion = new ActorProcedureState(new());
            var jonahHost = new Host(scope, resources, lighting, jonah, Capture);
            ScorpionEncounterInitialization.ConstructJonah(jonah, jonahMotion, jonahDefinition, jonahHost, sourceNodeOffset: triggers.Nodes[2].SourceOffset);
            Assert(jonah.Type == 316 && jonah.SourceNode == 2 && jonah.Health == 120 && jonah.BodyFlags == 0x206 &&
                jonah.Animation.FrameCount == 1 && jonah.Animation.Finished == 1 && jonahHost.Model.Model.Playback == jonah.Animation &&
                jonahHost.PanelRequests.SequenceEqual(new short[] { 316 }) &&
                jonahHost.Events.SequenceEqual(new[] { "model:jameson", "light:316", "attach", "script", "panel:316" }),
                "Unchanged L2A2 node2 must preserve source120 health, body flag10 clearing and untouched CSuper constructor playback rather than issuing a synthetic animation command.");
            VerifyBank(jonahHost.Model, jonahMotion, jonah.ItemFlags, 16, 26);

            var joel = new JonahState(2);
            var joelMotion = new ActorProcedureState(new());
            var joelHost = new Host(scope, resources, lighting, joel, Capture);
            ScorpionEncounterInitialization.ConstructJonah(joel, joelMotion, jonahDefinition, joelHost, joelJewettCheat: true, sourceNodeOffset: triggers.Nodes[2].SourceOffset);
            Assert(joel.ModelName == "jjjj" && joelHost.Events[0] == "model:jjjj" && joelHost.Model.Model.Playback == joel.Animation,
                "The actual Joel constructor branch must select the genuine JJJJ bank while preserving Jonah's source body configuration.");
            VerifyBank(joelHost.Model, joelMotion, joel.ItemFlags, 16, 26);

            Resource[] beforeReparent = scorpionHost.Model.CaptureOwnedResources();
            scorpionHost.Model.Reparent(destination);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            scorpionHost.Model.VerifyNativePoseAndLighting();
            Assert(beforeReparent.All(GodotObject.IsInstanceValid) && beforeReparent.SequenceEqual(scorpionHost.Model.CaptureOwnedResources()),
                "A live source encounter model reparent must preserve its same private mesh/material/texture identities and rebuild its genuine pose render storage.");

            var failedState = new JonahState(3);
            var failedMotion = new ActorProcedureState(new());
            var failed = new Host(scope, resources, lighting, failedState, Capture) { ThrowOnHealthBar = true };
            bool rejected = false;
            try { ScorpionEncounterInitialization.ConstructJonah(failedState, failedMotion, jonahDefinition, failed, sourceNodeOffset: triggers.Nodes[2].SourceOffset); }
            catch (ConstructorTransportFailure) { rejected = true; }
            finally { if (failed.Model is { } model && GodotObject.IsInstanceValid(model)) model.Free(); }
            Assert(rejected && failed.CapturedResources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "Failure after a real model/light/authored-script constructor must release all its private resources, including the unused material, through native Predelete.");
            GD.Print("Verified native Scorpion/Jonah constructor slice: unchanged L2A2 node1/2 bytecode, original body/animation/panel callbacks, all Scorpion/Jameson/JJJJ clips and part boxes, source light uploads, reparenting and constructor-failure resource cleanup. Encounter AI/Hit/tail/health-bar rendering remain unregistered.");
        }
        finally
        {
            foreach (GodotScorpionEncounterModel model in nativeOwners)
                if (GodotObject.IsInstanceValid(model)) model.Free();
            scope.Free(); destination.Free();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(nativeResources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "Actual source encounter model destruction must release every private mesh/material/texture resource after reparenting and failure.");
        }

        void Capture(GodotScorpionEncounterModel model)
        { nativeOwners.Add(model); nativeResources.AddRange(model.CaptureOwnedResources()); }
    }

    private static void VerifyBank(GodotScorpionEncounterModel owner, ActorProcedureState actor, ushort flags, int parts, int clips)
    {
        Assert(owner.Model.SourcePartCount == parts && owner.CollisionBoxes.Count == parts && owner.Model.ClipCount == clips,
            "The native encounter model must retain its complete original skeleton, collision boxes and animation bank.");
        var constructorPlayback = owner.Model.Playback;
        for (int clip = 0; clip < clips; clip++)
        {
            owner.Model.SelectClip(clip);
            foreach (int frame in new[] { 0, owner.Model.FrameCount - 1 })
            {
                owner.Model.SetSourceAnimationFrame(frame);
                owner.SynchronizeSourcePose(actor.Body.Position, actor.Body.Angles, flags);
                owner.SynchronizeSourceLighting(flags);
                owner.VerifyNativePoseAndLighting();
            }
        }
        owner.Model.RestoreSourcePlayback(constructorPlayback);
        owner.SynchronizeSourcePose(actor.Body.Position, actor.Body.Angles, flags);
        owner.SynchronizeSourceLighting(flags);
    }

    private sealed class ConstructorTransportFailure : Exception { }

    private sealed class Host(Node scope, GodotScorpionEncounterResources resources, SourceVertexLightingState lighting,
        ScorpionEncounterBodyState state, Action<GodotScorpionEncounterModel> capture) :
        IScorpionEncounterInitializationHost, IActorScriptHost, IActorScriptVariableWorld
    {
        private ActorScriptVariables? variables;
        public GodotScorpionEncounterModel Model { get; private set; } = null!;
        public Resource[] CapturedResources { get; private set; } = [];
        public List<string> Events { get; } = [];
        public List<short> PanelRequests { get; } = [];
        public bool ThrowOnHealthBar { get; init; }
        public ActorScriptState ScriptState { get; private set; } = null!;
        public int Difficulty => 2;
        public int MovieInputPolls => 0;
        public FixedVector3? PlayerPosition => null;
        public void InitializeModel(string name)
        {
            Events.Add("model:" + name);
            Model = GodotScorpionEncounterModel.Create(resources, name, lighting);
            scope.AddChild(Model);
            CapturedResources = Model.CaptureOwnedResources(); capture(Model);
        }
        public void AssignSourceLight(short type) { Events.Add("light:" + type); Model.AssignSourceLight(type); }
        public void AttachToSourceBaddyList() => Events.Add("attach");
        public ushort ReadAnimationFrameCount(ushort clip) => checked((ushort)Model.Model.ReadFrameCount(clip));
        public void CycleAnimation(ushort clip, sbyte direction)
        { Events.Add($"cycle:{clip}:{direction}"); Model.Model.CycleClip(clip, direction); }
        public void ParseScript(ActorScriptCursor script)
        {
            Events.Add("script");
            ScriptState = new() { ActorType = state.Type, NodeId = state.SourceNode };
            variables = new(ScriptState, this) { Damage = state.Damage };
            new ActorScriptRuntime(script, this).Start();
        }
        public void CreateHealthBar(short type)
        {
            Events.Add("panel:" + type); PanelRequests.Add(type);
            if (ThrowOnHealthBar) throw new ConstructorTransportFailure();
            Model.Model.RestoreSourcePlayback(state.Animation);
        }
        public short ReadVariable(ushort variable, ActorScriptCursor operands) => variables!.Read(variable, operands);
        public void WriteVariable(ushort variable, ActorScriptCursor operands)
        { variables!.Write(variable, operands); state.Damage = variables.Damage; }
        public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) => throw new NotSupportedException($"The real boss constructor requires an additional source opcode{opcode:X4}.");
        public ActorScriptCursor ScriptAtNode(ushort node) => throw new NotSupportedException("The constructor has no source script-switch dependency.");
        public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("The constructor has no player-ceiling dependency.");
        public void FlashCompass() => throw new NotSupportedException("The constructor has no compass dependency.");
        public void SendDeathPulse() => throw new NotSupportedException("The constructor has no death-pulse dependency.");
        public void Retire() => throw new NotSupportedException("The constructor must retain its actual native body resources.");
        public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => throw new NotSupportedException();
        public int Random(int range) => throw new NotSupportedException();
        public ushort FirstLink(ushort node) => throw new NotSupportedException();
        public void SelectModel(uint checksum) => throw new NotSupportedException();
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}

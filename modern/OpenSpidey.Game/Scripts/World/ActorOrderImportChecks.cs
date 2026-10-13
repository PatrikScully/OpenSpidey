using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

internal static class ActorOrderImportChecks
{
    public static async Task VerifyAsync(Node owner, GodotActorRegistry sessionActors)
    {
        var requests = new List<SpeechRequest>();
        var playback = new Playback();
        var speech = new SpeechController(playback);
        TriggerNode controlTemplate = sessionActors.Triggers.Nodes[36];
        GodotActorRegistry actors = null!;
        actors = new GodotActorRegistry { Name = "ActorOrderVerification", Triggers = sessionActors.Triggers,
            ObjectLibrary = sessionActors.ObjectLibrary, ObjectTextures = sessionActors.ObjectTextures,
            ClaimObjectModel = sessionActors.ClaimObjectModel, Environment = sessionActors.Environment,
            VoicePlayback = (bank, slot, priority) =>
            {
                requests.Add(new(bank, slot, priority));
                speech.Play(bank, slot, priority);
                if (bank == 1 && slot == 10)
                    actors.Spawn(Script(controlTemplate, retainModel: false, 0x4107, 0x42A3, 1, 0, 51, 0x4100));
            } };
        owner.AddChild(actors);
        actors.SetPhysicsProcess(false);
        var spawned = new List<GodotScriptActor>();
        actors.Spawned += spawned.Add;
        try
        {
            TriggerNode platform = sessionActors.Triggers.Nodes[170];
            TriggerNode control = sessionActors.Triggers.Nodes[36];
            // Interleave construction so a single head-inserted list would incorrectly run control B first.
            actors.Spawn(Script(platform, retainModel: true, 0x42B6, 1, 0x42A3, 1, 9, 50, 0x4100));
            actors.Spawn(Script(control, retainModel: false, 0x42B6, 4, 0x42A3, 67, 15, 50, 0x4100));
            actors.Spawn(Script(platform, retainModel: true, 0x42B6, 1, 0x42A3, 1, 10, 50, 0x4100));
            actors.Spawn(Script(control, retainModel: false, 0x42B6, 4, 0x42A3, 67, 14, 50, 0x4100));
            if (requests.Count != 0 || spawned.Count != 4 || spawned[0].UpdateGroup != ActorUpdateGroup.Environment ||
                spawned[1].UpdateGroup != ActorUpdateGroup.Control)
                throw new InvalidDataException("Actor constructors lost their deferred waits or source update families.");
            actors.SetPhysicsProcess(true);
            while (actors.SourceUpdates < 3)
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            actors.SetPhysicsProcess(false);
            SpeechRequest[] expected = [new(1, 10, 50), new(1, 9, 50), new(1, 0, 51), new(67, 14, 50), new(67, 15, 50)];
            if (actors.SourceUpdates != 3 || spawned.Count != 5 || spawned.Take(4).Any(actor => actor.SourceUpdates != 3) ||
                spawned[4].SourceUpdates != 1 || !requests.SequenceEqual(expected) || playback.Played.Count != 2 ||
                playback.Played[0].Bank != 1 || playback.Played[0].Slot != 10 || speech.Current != expected[2])
                throw new InvalidDataException("Mixed actor families changed head order, same-frame later-family spawning or speech priority results.");
            GD.Print("Verified native actor ordering: environment before control, head order within families, same-frame control spawning and original speech priority results.");
        }
        finally { actors.QueueFree(); }
    }

    private static TriggerNode Script(TriggerNode node, bool retainModel, params ushort[] words)
    {
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        int prefix = definition.ObjectDataOffset + 18 + (retainModel ? 6 : 0);
        using var stream = new MemoryStream();
        stream.Write(node.Data.Span[..prefix]);
        using var writer = new BinaryWriter(stream);
        foreach (ushort word in words) writer.Write(word);
        return node with { Data = stream.ToArray() };
    }

    private sealed class Playback : ISpeechPlayback
    {
        public bool Finished => false;
        public List<SpeechClip> Played { get; } = [];
        public void Play(SpeechClip clip) => Played.Add(clip);
        public void Stop() { }
        public void Pause(bool paused) { }
    }
}

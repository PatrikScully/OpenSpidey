using System.Diagnostics;
using Godot;

namespace OpenSpidey.Game.Audio;

/// <summary>Retires playback while the audio mixer and main loop can release their references.</summary>
internal static class GodotAudioShutdown
{
    internal readonly record struct Resource(ulong Id, string Source, RefCounted Owner);

    public static async Task<Resource[]> RetirePlaybackAsync(Node root, SceneTree tree)
    {
        Node[] nodes = Descendants(root).ToArray();
        GodotSoundChannels[] sounds = nodes.OfType<GodotSoundChannels>().ToArray();
        GodotSpeechPlayer[] speech = nodes.OfType<GodotSpeechPlayer>().ToArray();
        Resource[] resources = sounds.SelectMany(backend => backend.ReadOwnedResources())
            .Concat(speech.SelectMany(backend => backend.ReadOwnedResources()))
            .Select(resource => new Resource(resource.Id, resource.Source, resource.Owner)).ToArray();
        foreach (GodotSoundChannels backend in sounds)
            for (int channel = 0; channel < 32; channel++) backend.Controller.Stop(1u << channel);
        foreach (GodotSpeechPlayer backend in speech)
        {
            backend.Controller.Stop();
            backend.Player.Stream = null;
        }
        Resource[] pcm = resources.Where(resource => resource.Owner is AudioStreamWav).ToArray();
        var deadline = Stopwatch.StartNew();
        while (pcm.Any(resource => resource.Owner.GetReferenceCount() != 1))
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("Stopped audio retains PCM: " + string.Join(", ",
                    pcm.Where(resource => resource.Owner.GetReferenceCount() != 1)
                        .Select(resource => $"{resource.Source} (references={resource.Owner.GetReferenceCount()})")));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        return resources;
    }

    public static async Task VerifyReleasedAsync(IReadOnlyList<Resource> resources, SceneTree tree)
    {
        var deadline = Stopwatch.StartNew();
        while (resources.Any(resource => GodotObject.IsInstanceIdValid(resource.Id)))
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("Native audio deletion retains resources: " + string.Join(", ",
                    resources.Where(resource => GodotObject.IsInstanceIdValid(resource.Id))
                        .Select(resource => $"{resource.Source} ({resource.Id})")));
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
            foreach (Node descendant in Descendants(child)) yield return descendant;
    }
}

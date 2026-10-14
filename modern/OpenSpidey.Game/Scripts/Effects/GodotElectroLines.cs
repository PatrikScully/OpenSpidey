using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Effects;

/// <summary>Owns CElectroLine SpecialDisplay bits created by Effects_CreateElectroLines (0x439CC0).</summary>
public partial class GodotElectroLines : Node
{
    private readonly List<ElectroLineState> lines = [];
    private bool disposed;
    public event Action<SourceParticleRegistration>? SourceBitCreated;
    public Func<IReadOnlyList<ActorLaserBody>?>? PlayerBodies { get; set; }
    public Func<int, int> Random { get; set; } = _ => 0;
    public Func<ReadOnlyMemory<byte>> TextureManifest { get; set; } =
        () => throw new InvalidOperationException("Electroline Bolt textures require the loaded level .tex manifest.");
    public int Count => lines.Count;
    public IReadOnlyList<ElectroLineState> Lines => lines;

    public void Create(TriggerFile triggers, ushort nodeId)
    {
        CheckAlive();
        uint[] checksums =
        [
            NamedTextureTable.Find(TextureManifest().Span, "Bolt"),
            NamedTextureTable.Find(TextureManifest().Span, "Bolt2"),
            NamedTextureTable.Find(TextureManifest().Span, "Bolt3")
        ];
        foreach (ElectroLineState line in EffectsCreateElectroLines.Create(triggers, nodeId, checksums))
            Attach(line);
    }

    public void KillOwner(ushort node)
    {
        CheckAlive();
        // SendKillFromNode type409: SpecialDisplayList walk, mType==9 and field_6A==node, then CBit::Die.
        foreach (ElectroLineState line in lines.ToArray())
        {
            if (line.Alive && line.OwnerNode == node) line.Die();
        }
    }

    private void Attach(ElectroLineState line)
    {
        lines.Insert(0, line);
        SourceBitCreated?.Invoke(new(line,
            _ => line.Advance(Random, PlayerBodies?.Invoke()),
            () => !disposed && lines.Contains(line) && line.Alive,
            () => lines.Remove(line)));
    }

    private void CheckAlive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(GodotElectroLines));
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        lines.Clear();
    }
}

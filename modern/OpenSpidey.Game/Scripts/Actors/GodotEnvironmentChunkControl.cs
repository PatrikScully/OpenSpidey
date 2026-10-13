using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Actual unscripted CChunkControl body in ControlBaddyList; entries retain the live source ENV item identities.</summary>
public partial class GodotEnvironmentChunkControl : GodotScriptActor, IActorScriptVariableWorld
{
    private GodotActorRegistry registry = null!;
    private GodotEnvironmentVisibility visibility = null!;
    private Func<uint> clock = null!;
    public EnvironmentChunkControl State { get; private set; } = null!;
    public ushort BodyFlags { get => State.BodyFlags; set => State.BodyFlags = value; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Control;
    public override FixedVector3 SourcePosition => State.Position;
    internal static GodotEnvironmentChunkControl Create(GodotActorRegistry registry, GodotEnvironmentVisibility visibility,
        Func<uint> clock, FixedVector3 position, ushort capacity)
    {
        var actor = new GodotEnvironmentChunkControl { Name = "OriginalEnvironmentChunkControl", registry = registry,
            visibility = visibility, clock = clock, State = new(position, capacity),
            ScriptState = new() { ActorType = 0, NodeId = ushort.MaxValue }, Lifecycle = new(true, false) };
        try
        {
            actor.Variables = new(actor.ScriptState, actor) { Position = position };
            // Source constructor attaches before AddChunk or any static/moving piece edits. There is no authored trigger definition.
            registry.Adopt(actor); return actor;
        }
        catch { if (registry.Owns(actor)) registry.DestroyImmediately(actor); else if (GodotObject.IsInstanceValid(actor)) actor.Free(); throw; }
    }
    internal override void Begin() => Position = PsxCoordinates.FixedWorldPosition(State.Position);
    internal override void Advance() => AdvanceSourceClock(clock());
    public void AdvanceSourceClock(uint sourceClock)
    {
        State.BodyFlags = State.Timing.PrepareClock(sourceClock, State.BodyFlags);
        GodotEnvironmentVisibilityItem[] affected = State.Entries.Select(entry => entry.Item).OfType<GodotEnvironmentVisibilityItem>().ToArray();
        State.Update(unchecked((uint)State.Timing.ElapsedTicks)); SourceUpdates++;
        foreach (GodotEnvironmentVisibilityItem item in affected) visibility.SynchronizeSourceItem(item);
        if ((State.BodyFlags & 0x40) != 0) Lifecycle.Kill();
    }
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public FixedVector3? PlayerPosition => registry.PlayerPosition;
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public int Random(int range) => registry.RandomSource(range);
    public ushort FirstLink(ushort nodeId) => registry.FirstLink(nodeId);
    public void SelectModel(uint checksum) => throw new InvalidOperationException("Original CChunkControl has no script or body model.");
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) State?.Dispose();
    }
}

using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Original finalizer's hook1/0 snapshots; preceding attachment mutations are retained for later attachments.</summary>
public record struct PlayerHandHooks(FixedVector3 Left, FixedVector3 Right);
public delegate void PlayerHandHookUpdate(ref PlayerHandHooks hooks);

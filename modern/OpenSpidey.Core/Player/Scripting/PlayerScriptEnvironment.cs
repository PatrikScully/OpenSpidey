using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player.Scripting;

public readonly record struct PlayerScriptEnvironment(bool Abort, bool ForceMenuExit, int LevelId, FixedVector3? BossPosition = null);

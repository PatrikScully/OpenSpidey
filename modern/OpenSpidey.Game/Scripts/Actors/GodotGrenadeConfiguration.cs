using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Scene-owned original assets, clock, collision, audio and actual blast target handlers.</summary>
public sealed class GodotGrenadeConfiguration
{
    public required GodotActorWorldQuery World { get; init; }
    public required Func<byte, PsxAssetLibrary> ResolveRegion { get; init; }
    public required Func<int> Timer { get; init; }
    public required Func<int> TotalTime { get; init; }
    public required Func<int> LevelId { get; init; }
    public required Func<byte> ObjectFileRegion { get; init; }
    public required Func<byte> DefaultGrenadeRegion { get; init; }
    public required Func<int, int> Random { get; init; }
    public required GodotGlowFlash Glows { get; init; }
    public required GodotGrenadeExplosions Explosions { get; init; }
    public required Func<GodotActorShadow> ShadowFactory { get; init; }
    public required Action<uint, FixedVector3, int> PlayPositionSound { get; init; }
    public required Action<GodotGrenadeProjectile, GrenadeDamageRequest> ApplyObjectBlast { get; init; }
    public required Action<GodotGrenadeProjectile, FixedVector3, ushort, ushort> ApplyEnvironmentBlast { get; init; }
    public required Action<GodotGrenadeProjectile> AttachProjectile { get; init; }
    public required Action<GodotGrenadeProjectile> DetachProjectile { get; init; }
}

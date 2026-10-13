using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Combat;

/// <summary>Named SHitInfo fields shared by enemy attacks and player/actor damage reactions.</summary>
public readonly record struct ActorHit(byte Flags, byte PartIndex, int AttackKind, ushort Damage,
    FixedVector3 Direction, short SlideDistance = 0, ushort SlideFrames = 0);

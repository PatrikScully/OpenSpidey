using Godot;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Thug appearance ownership backed by the shared source actor material/shadow implementation.</summary>
public sealed class GodotThugAppearance : IDisposable
{
    private readonly GodotActorAppearance appearance;
    private GodotThugAppearance(GodotActorAppearance appearance) => this.appearance = appearance;
    public GodotActorShadow Shadow => appearance.Shadow;
    public short ShadowDistance { get => appearance.ShadowDistance; set => appearance.ShadowDistance = value; }
    public short ShadowThreshold { get => appearance.ShadowThreshold; set => appearance.ShadowThreshold = value; }
    public static GodotThugAppearance Create(GodotThugActor actor,DecodedTexture shadowTexture,float sourceTintGamma=.7f) =>
        new(GodotActorAppearance.Create(new Host(actor),shadowTexture,sourceTintGamma));
    public void ShadowOn() => appearance.ShadowOn();
    public void KillShadow() => appearance.KillShadow();
    public void Synchronize() => appearance.Synchronize();
    public void Dispose() => appearance.Dispose();
    internal void VerifyGeometry(int? redTintByte=null,int? greenTintByte=null,int? blueTintByte=null) => appearance.VerifyGeometry(redTintByte,greenTintByte,blueTintByte);
    internal void VerifyTransparency(bool enabled) => appearance.VerifyTransparency(enabled);
    private sealed class Host(GodotThugActor actor) : IGodotActorAppearanceHost
    {
        public Node3D AppearanceNode => actor;
        public ImportedAnimatedModel Model => actor.Model;
        public uint BodyFlags { get => actor.State.BodyFlags; set => actor.State.BodyFlags=value; }
        public ushort ItemFlags => actor.ItemFlags;
        public uint TintColor => actor.State.TintColor;
        public FixedVector3 ShadowPosition => actor.State.ShadowPosition;
        public int ShadowScale => actor.State.ShadowScale;
    }
}

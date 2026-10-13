using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

/// <summary>Real surface-contact brightness and original SLight vertex uploads over the player's retained render pose.</summary>
public partial class GodotPlayerBrightness : Node, IPlayerSurfaceBrightnessHost
{
    private GodotPlayer player = null!;
    private SourceVertexLightingState sharedColours = null!;
    private GodotSourceVertexLighting? lighting;
    private SourceModelLight? uploadedLight;
    private bool destroyed;
    public PlayerBrightnessState State { get; private set; } = null!;
    public int SurfaceContacts { get; private set; }
    public int RenderVisits { get; private set; }
    public int LitPartCount => lighting?.LitPartCount ?? 0;
    public int VisibleLitPartCount => lighting?.VisibleLitPartCount ?? 0;
    internal SourceVertexLightingState SourceColours => sharedColours;

    public static GodotPlayerBrightness Create(GodotPlayer player, OriginalPlayerLightingData data, SourceVertexLightingState sharedColours)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(data); ArgumentNullException.ThrowIfNull(data.Light);
        ArgumentNullException.ThrowIfNull(sharedColours);
        if (player.Controller.SurfaceBrightness is not null) throw new InvalidOperationException("The player already owns its source surface-brightness callback.");
        var owner = new GodotPlayerBrightness { Name = "OriginalPlayerBrightness", player = player,
            sharedColours = sharedColours, State = new(data.Light) };
        // CPlayer's actual constructor enables dynamic lighting and source render parts with its480 item mask.
        player.ActorState.ItemFlags |= 0x480;
        player.Controller.SurfaceBrightness = owner;
        return owner;
    }
    public void AdjustBrightness(bool darkSurface, int elapsedTicks)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        SurfaceContacts++;
        State.Adjust(State.SurfaceTarget(darkSurface), elapsedTicks);
    }
    public void SynchronizeSourceLighting()
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        if (!ReferenceEquals(uploadedLight, State.Light))
        {
            lighting = new(player.Model, State.Light, sharedColours, player.Model.GetPartPose);
            uploadedLight = State.Light;
        }
        // Rendering uses the genuine geometry order, including the player's final render-only part swaps.
        lighting!.Synchronize(player.Controller.Orientation.Basis, player.ActorState.ItemFlags);
        RenderVisits++;
    }
    internal void VerifyUploads() => lighting?.VerifyUploads();
    public override void _Process(double delta) => SynchronizeSourceLighting();
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || destroyed) return;
        destroyed = true;
        if (player is not null && ReferenceEquals(player.Controller.SurfaceBrightness, this)) player.Controller.SurfaceBrightness = null;
        lighting = null; uploadedLight = null;
    }
}

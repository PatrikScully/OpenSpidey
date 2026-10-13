using Godot;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Effects;

namespace OpenSpidey.Game.Effects;

/// <summary>Viewport overlay for the recovered PC flash quad; updates follow source ticks.</summary>
public partial class GodotScreenFlash : CanvasLayer, IActorScreenFlashHost
{
    private readonly ColorRect overlay = new()
    {
        Name = "ScreenFlashQuad",
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false
    };

    public ScreenFlashState State { get; } = new();
    public event Action? SourceUpdated;

    public GodotScreenFlash() => Layer = 100;

    public override void _Ready()
    {
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Synchronize();
    }

    public void Reset()
    {
        State.Reset();
        Synchronize();
    }

    public void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort)
    {
        State.Screen(red, green, blue, frames, importance, sort);
        Synchronize();
    }

    public void Update()
    {
        State.Update();
        Synchronize();
        SourceUpdated?.Invoke();
    }

    public void Synchronize()
    {
        // PCGfx_DrawQPoly2D retains packed alpha 0x20; DXPOLY mode 1 uses
        // SRCALPHA / INVSRCALPHA, exactly ColorRect's default mix blending.
        overlay.Color = new(State.Red / 255f, State.Green / 255f, State.Blue / 255f,
            ScreenFlashState.DisplayAlpha / 255f);
        overlay.Visible = State.Visible;
    }
}

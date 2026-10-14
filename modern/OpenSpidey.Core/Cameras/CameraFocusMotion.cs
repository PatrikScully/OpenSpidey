using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>A stable source body identity and its canonical live position, without a copied camera target vector.</summary>
public sealed class CameraFocusTarget(object identity, Func<FixedVector3> readPosition)
{
    public object Identity { get; } = identity ?? throw new ArgumentNullException(nameof(identity));
    private readonly Func<FixedVector3> readPosition = readPosition ?? throw new ArgumentNullException(nameof(readPosition));
    public FixedVector3 Position => readPosition();
}

/// <summary>CCamera AI's13C/140/144/150/15C/168 focus phase. Position remains in the shared camera mode owner.</summary>
public sealed class CameraFocusMotion(CameraModeState camera)
{
    private readonly CameraModeState camera = camera ?? throw new ArgumentNullException(nameof(camera));
    public CameraFocusTarget? Target { get; set; }
    public bool TrackingEnabled { get; set; } = true;
    public FixedVector3 MotionTarget { get; set; }
    public FixedVector3 Rate { get; set; }
    public int RemainingTicks { get; set; }
    public FixedVector3 Position { get => camera.FocusPosition; set => camera.FocusPosition = value; }

    /// <summary>Run before the camera mode selects this frame's focus owner; Boss3 therefore takes effect in the following AI update.</summary>
    public void Advance(int elapsedTicks)
    {
        if (unchecked((uint)RemainingTicks) <= unchecked((uint)elapsedTicks))
        {
            if (RemainingTicks != 0) { RemainingTicks = 0; Position = MotionTarget; }
            else if (TrackingEnabled && Target is { } target) Position = target.Position;
        }
        else
        {
            RemainingTicks = unchecked(RemainingTicks - elapsedTicks);
            Position += Rate * elapsedTicks;
        }
    }
}

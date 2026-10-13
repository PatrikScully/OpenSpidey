namespace OpenSpidey.Core.Cameras;

public enum CameraSurface : ushort { Floor, Wall, Ceiling, Reserved, Swing, Falling }
public enum CameraParameter : ushort { OffsetX, OffsetY, OffsetZ, HorizontalDistance, VerticalDistance }
public readonly record struct CameraPlayerState(bool OnWall, bool OnCeiling, bool Swinging, uint Action);
public readonly record struct CameraParameterTransitionSnapshot(int Value, int Target, int RemainingTicks, int Rate);

/// <summary>Recovered camera scalar interpolation, including per-update offset truncation.</summary>
public sealed class CameraParameterTransition(int initial, bool fractionalRate)
{
    public int Value { get; private set; } = initial;
    public int Target { get; private set; } = initial;
    public int RemainingTicks { get; private set; }
    public int Rate { get; private set; }

    public CameraParameterTransitionSnapshot Capture() => new(Value, Target, RemainingTicks, Rate);
    public void Restore(CameraParameterTransitionSnapshot snapshot)
    { Value = snapshot.Value; Target = snapshot.Target; RemainingTicks = snapshot.RemainingTicks; Rate = snapshot.Rate; }

    public void Set(int target, ushort duration, bool distanceInterpolationEnabled = true)
    {
        if (duration == 0) { Value = target; Target = target; RemainingTicks = 0; return; }
        Target = target;
        int difference = unchecked(target - Value);
        Rate = fractionalRate ? unchecked(difference << 12) / duration : difference / duration;
        if (fractionalRate || distanceInterpolationEnabled) RemainingTicks = duration;
    }

    public void Advance(int ticks)
    {
        if (RemainingTicks == 0) return;
        if (RemainingTicks <= ticks) { Value = Target; RemainingTicks = 0; return; }
        RemainingTicks -= ticks;
        int movement = unchecked(Rate * ticks);
        Value = unchecked(Value + (fractionalRate ? movement >> 12 : movement));
    }
}

/// <summary>Surface-specific defaults from player initialization and SetSpideyCamValue's application gates.</summary>
public sealed class CameraParameterProfiles
{
    private readonly short[,] values = new short[6, 5];
    public CameraParameterProfiles()
    {
        SetDefaults(CameraSurface.Floor, 512, -128);
        SetDefaults(CameraSurface.Wall, 700, 0);
        SetDefaults(CameraSurface.Ceiling, 800, 100);
        SetDefaults(CameraSurface.Swing, 512, -128);
        SetDefaults(CameraSurface.Falling, 512, -128);
    }
    private void SetDefaults(CameraSurface surface, short horizontal, short vertical)
    {
        values[(int)surface, 3] = horizontal;
        values[(int)surface, 4] = vertical;
    }
    public short Get(CameraSurface surface, CameraParameter parameter) => values[(int)surface, (int)parameter];
    public bool Set(CameraSurface surface, CameraParameter parameter, short value, bool apply, CameraPlayerState player)
    {
        if (surface == CameraSurface.Reserved) return false;
        if ((int)surface >= 6 || (int)parameter >= 5) throw new InvalidDataException("Unknown player camera profile or parameter.");
        values[(int)surface, (int)parameter] = value;
        if (!apply) return false;
        return surface switch
        {
            CameraSurface.Floor => !player.OnWall && !player.OnCeiling,
            CameraSurface.Wall => player.OnWall,
            CameraSurface.Ceiling => player.OnCeiling,
            CameraSurface.Swing => player.Swinging,
            CameraSurface.Falling => player.Action == 4,
            _ => false
        };
    }
}

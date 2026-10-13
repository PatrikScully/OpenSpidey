using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed class PlayerHandTrailState
{
    public object?[] Current { get; } = new object?[2];
    public FixedVector3[] PreviousOffsets { get; } = new FixedVector3[2];
    public uint Colour { get; set; }
}

public interface IPlayerHandTrailHost
{
    FixedVector3 HookPosition(int hook);
    object CreateTrail(FixedVector3 position, int count, int red, int green, int blue);
    void DeleteTrail(object trail);
    void SetTrailPosition(object trail, FixedVector3 position);
}

/// <summary>Actual hand58C/590 smoke owner selection and original4BB1E0 midpoint projection, distinct from the smash foot trails.</summary>
public static class PlayerHandTrails
{
    public static void Update(PlayerHandTrailState state, IPlayerHandTrailHost host, FixedVector3 playerPosition,
        ushort animation, short frame)
    {
        int action;
        bool deleteSecond = false, createSecond = false;
        if (animation == 104)
            action = frame > 6 ? 3 : frame <= 0 ? state.Current[0] is null ? 0 : 2 : state.Current[0] is null ? 1 : 2;
        else if (animation == 106)
        {
            if (frame > 7) { deleteSecond = true; action = 3; }
            else if (frame > 1) { createSecond = state.Current[1] is null; action = state.Current[0] is null ? 1 : 2; }
            else action = state.Current[0] is null ? 0 : 2;
        }
        else { deleteSecond = true; action = 3; }
        if (action == 3)
        {
            Delete(0);
            if (deleteSecond) Delete(1);
        }
        else if (action == 1) Create(0);
        else if (action == 2) Move(0);
        if (createSecond) Create(1);
        else if (state.Current[1] is not null) Move(1);

        void Delete(int slot)
        {
            if (state.Current[slot] is { } trail) host.DeleteTrail(trail);
            state.Current[slot] = null;
        }
        void Create(int slot)
        {
            FixedVector3 position = host.HookPosition(slot == 0 ? 1 : 0);
            state.Current[slot] = host.CreateTrail(position, 6, (byte)state.Colour, (byte)(state.Colour >> 8), (byte)(state.Colour >> 16));
            state.PreviousOffsets[slot] = position - playerPosition;
        }
        void Move(int slot)
        {
            object trail = state.Current[slot] ?? throw new InvalidOperationException("Source hand trail update requires its live owner.");
            FixedVector3 position = host.HookPosition(slot == 0 ? 1 : 0);
            FixedVector3 offset = position - playerPosition;
            if (IntermediateStep(playerPosition, offset, state.PreviousOffsets[slot]) is { } intermediate)
                host.SetTrailPosition(trail, intermediate);
            host.SetTrailPosition(trail, position);
            state.PreviousOffsets[slot] = offset;
        }
    }
    public static FixedVector3? IntermediateStep(FixedVector3 playerPosition, FixedVector3 current, FixedVector3 previous)
    {
        uint count = unchecked((uint)FixedVectorMath.OriginalDistance(previous, current)) >> 5;
        if (count == 0) return null;
        int previousLength = Length(previous), currentLength = Length(current);
        FixedVector3 middle = (previous + (current - previous) / 2) >> 8;
        // Original4BB278 shifts the vector itself. Recovered C++ accidentally shifts its count variable.
        middle = FixedVectorMath.Normalize(middle);
        return middle * (unchecked(previousLength + currentLength) / 2) + playerPosition;
    }
    private static int Length(FixedVector3 position)
    {
        FixedVector3 whole = position >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z));
    }
}

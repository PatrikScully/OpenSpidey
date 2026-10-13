namespace OpenSpidey.Core.Mathematics;

public readonly record struct GameRandomState(int Value, int Multiplier, int Increment);

/// <summary>Original game random stream, including state consumption for zero bounds and signed arithmetic.</summary>
public sealed class GameRandom(int seed)
{
    public GameRandomState State { get; private set; } = new(seed, 0x12B9B0A1, 0x0AA2FB3F);

    public void Restore(GameRandomState state) => State = state;

    public int Next(int bound)
    {
        unchecked
        {
            int value = State.Increment + State.Value * State.Multiplier;
            int multiplier = (value >> 4) + (value ^ State.Multiplier);
            int result = (bound * (ushort)value) >> 16;
            int increment = State.Increment + (value >> 3) - 0x10101010;
            State = new(value, multiplier, increment);
            return result;
        }
    }
}

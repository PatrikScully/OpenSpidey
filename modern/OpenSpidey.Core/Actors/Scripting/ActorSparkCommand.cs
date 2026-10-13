using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorSparkEmitter
{
    SparkSettings Settings { get; }
    int LiveBitCount { get; }
    void TrySpawnSpark(SparkSpawnRequest request);
}

/// <summary>Original4292 checks actual BitCount once, then emits its raw-count batch under the shared spark globals.</summary>
public static class ActorSparkCommand
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, FixedVector3 position,
        ActorAngles angles, IActorSparkEmitter emitter)
    {
        if (opcode != 0x4292) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(emitter);
        ushort count = operands.ReadWord();
        if (emitter.LiveBitCount >= 200) return true;
        SparkSettings settings = emitter.Settings;
        settings.Trajectory = angles; settings.Cone = new(512, 4096, 0);
        settings.Size = 0x00010001;
        settings.Red = settings.Green = settings.Blue = 128;
        settings.FadeRed = settings.FadeGreen = settings.FadeBlue = 4;
        settings.AllocationUsage = 0;
        for (int index = 0; index < count; index++) emitter.TrySpawnSpark(new(position, 32, 8192, 32));
        settings.AllocationUsage = 1;
        return true;
    }
}

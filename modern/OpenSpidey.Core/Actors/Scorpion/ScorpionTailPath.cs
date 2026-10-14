using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Original Scorpion cubic tail samples and ordered history updates, independent of native model and rendering ownership.</summary>
public static class ScorpionTailPath
{
    public const int ControlPointCount = 4;
    public const int NodeCount = 23;
    public const int HistoryCount = 128;
    private const int SegmentSampleCount = 12;
    private const int HistoryBlockStride = 32;
    private const int HistoryBlockSamples = 24;

    /// <summary>Sample original489660 from four whole-unit control points into Q12 points, retaining integer step/truncation and32-bit product wrapping.</summary>
    public static void SampleCubic(ReadOnlySpan<FixedVector3> controlPoints, Span<FixedVector3> samples)
    {
        if (controlPoints.Length != ControlPointCount)
            throw new ArgumentException("The original Scorpion cubic requires four control points.", nameof(controlPoints));
        if (samples.Length < 2)
            throw new ArgumentException("A source tail segment requires both endpoints.", nameof(samples));
        samples[0] = controlPoints[0] << 12;
        samples[^1] = controlPoints[3] << 12;
        int step = 4096 / (samples.Length - 1);
        int parameter = step;
        int inverse = 4096 - step;
        for (int sample = 1; sample < samples.Length - 1; sample++)
        {
            int inverseSquared = MultiplyQ12(inverse, inverse);
            int parameterSquared = MultiplyQ12(parameter, parameter);
            int firstWeight = MultiplyQ12(inverse, inverseSquared);
            int secondWeight = MultiplyQ12(unchecked(3 * parameter), inverseSquared);
            int thirdWeight = MultiplyQ12(parameter, MultiplyQ12(unchecked(3 * parameter), inverse));
            int finalWeight = MultiplyQ12(parameter, parameterSquared);
            // Original GPF multiplies whole-unit coordinates by Q12 coefficients without a final right shift.
            samples[sample] = controlPoints[0] * firstWeight + controlPoints[1] * secondWeight +
                controlPoints[2] * thirdWeight + controlPoints[3] * finalWeight;
            parameter = unchecked(parameter + step);
            inverse = unchecked(inverse - step);
        }
    }

    /// <summary>Original488370 reads posed hooks17..23, shares the middle endpoint, then shifts four sparse history blocks.</summary>
    public static void Update(Span<FixedVector3> controlPoints, Span<FixedVector3> nodes, Span<FixedVector3> history,
        Func<ushort, FixedVector3> readDynamicHook)
    {
        if (controlPoints.Length != ControlPointCount || nodes.Length != NodeCount || history.Length != HistoryCount)
            throw new ArgumentException("The source tail owns exactly four controls,23 nodes and128 history vectors.");
        ArgumentNullException.ThrowIfNull(readDynamicHook);
        for (ushort part = 17; part <= 20; part++)
            controlPoints[part - 17] = readDynamicHook(part) >> 12;
        SampleCubic(controlPoints, nodes[..SegmentSampleCount]);
        controlPoints[0] = controlPoints[3];
        for (ushort part = 21; part <= 23; part++)
            controlPoints[part - 20] = readDynamicHook(part) >> 12;
        SampleCubic(controlPoints, nodes[(SegmentSampleCount - 1)..]);
        for (int sample = 0; sample < HistoryBlockSamples; sample++)
        {
            history[3 * HistoryBlockStride + sample] = history[2 * HistoryBlockStride + sample];
            history[2 * HistoryBlockStride + sample] = history[HistoryBlockStride + sample];
            history[HistoryBlockStride + sample] = history[sample];
            // The original24th source vector lies beyond its23-node array at the first history vector.
            // That vector was already overwritten on iteration0, so the final history sample closes at the new first node.
            history[sample] = sample < NodeCount ? nodes[sample] : history[0];
        }
    }

    private static int MultiplyQ12(int left, int right) => unchecked(left * right) >> 12;
}

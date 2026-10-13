using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ThugBulletTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ThugBulletTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.thug-bullet.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            if (input[0] == 2)
            {
                var normal = ThugRibbonProjection.Normal(new((int)input[1], 1000, 0, input[3] != 0), new((int)input[2], 1000, 0, input[4] != 0));
                assert(normal.X == expected[0].GetInt32() && normal.Y == expected[1].GetInt32(), $"Ribbon normal{index} preserves original PC Y subtraction, clipping, aspect and signed truncation.");
            }
            else if (input[0] == 1)
            {
                var line = new ThugBarrelLineState();
                line.SetEndpoints(Vector(1), Vector(4));
                long[] actual = [line.Start.X, line.Start.Y, line.Start.Z, line.End.X, line.End.Y, line.End.Z, line.StartCodeRgb, line.EndCodeRgb];
                assert(line.Exists && actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())), $"Barrel{index} preserves exact source persistent blue endpoint fade and wrapping delta.");
                line.Clear();
                assert(!line.Exists, "DeleteBarrelLine releases persistent actor-owned line.");
                line.SetEndpoints(Vector(4), Vector(1));
                assert(line.Exists && line.Start == Vector(4) && line.End == Vector(1), "A subsequent shot recreates a cleared actor-owned barrel line.");
            }
            else
            {
                uint randomState = (uint)input[10];
                var calls = new List<int>();
                int Random(int range)
                {
                    calls.Add(range);
                    randomState = unchecked(randomState * 1664525 + 1013904223);
                    return range == 0 ? 0 : (int)(randomState % (uint)range);
                }
                var tracer = new ThugBulletTracerState(Vector(1), Vector(4), (byte)input[7], (byte)input[8], (byte)input[9], Random);
                int update = 0;
                foreach (JsonElement frame in expected.EnumerateArray())
                {
                    if (update != 0) { calls.Clear(); tracer.Advance(); }
                    assert(tracer.MaximumWidth == frame[0].GetUInt32() && tracer.Age == frame[1].GetInt32() && tracer.Alive == (frame[2].GetInt32() == 0),
                        $"Tracer{index} update{update} retains source width-before-decrement and fifth-update death.");
                    for (int bank = 0; bank < 2; bank++)
                    {
                        IReadOnlyList<ThugRibbonPoint> points = bank == 0 ? tracer.ColoredPoints : tracer.WhitePoints;
                        for (int point = 0; point < 5; point++)
                        {
                            var p = points[point];
                            long[] actual = [p.Position.X, p.Position.Y, p.Position.Z, p.Red, p.Green, p.Blue, p.Width];
                            assert(actual.SequenceEqual(frame[3][bank][point].EnumerateArray().Select(value => value.GetInt64())),
                                $"Tracer{index} update{update} bank{bank} point{point} preserves 1000-unit clipping, integer interpolation, sequential color extinction and random widths.");
                        }
                    }
                    assert(calls.SequenceEqual(frame[4].EnumerateArray().Select(value => value.GetInt32())), "Tracer preserves all eleven random calls including zero-range draws and overwritten endpoint widths.");
                    update++;
                }
            }
            index++;
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
        }
        var horizontal = ThugRibbonProjection.Normal(new(ThugRibbonProjection.Pack(10, 200), 1000, 3, false), new(ThugRibbonProjection.Pack(110, 200), 1000, 3, false));
        assert(horizontal == new ThugRibbonScreenNormal(0, -64), "Horizontal ribbons use actual equal Y; recovered source X/Y typo cannot skew the normal.");
    }
}

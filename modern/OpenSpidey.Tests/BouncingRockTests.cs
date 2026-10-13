using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class BouncingRockTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(BouncingRockTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.bouncing-rock.json")!);
        assert(document.RootElement.GetProperty("originalPcConstructorAndMoveVerified").GetBoolean(), "Rock gold requires original PC constructor and Move verification.");
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            uint seed = (uint)input[5]; List<int> bounds = [];
            int Random(int bound) { bounds.Add(bound); seed = unchecked(seed * 1664525 + 1013904223); return (int)(seed % (uint)bound); }
            var rock = new BouncingRockState(new((int)input[0], (int)input[1], (int)input[2]), (int)input[3], (uint)input[4], Random);
            assert(bounds.SequenceEqual(new[] { 200, 4096, 10, 20, 2 }), "Rock constructor uses five ordered original RNG bounds.");
            int update = 0;
            foreach (JsonElement state in test.GetProperty("states").EnumerateArray())
            {
                if (update != 0) rock.Advance();
                long[] actual = [rock.Position.X, rock.Position.Y, rock.Position.Z, rock.Velocity.X, rock.Velocity.Y, rock.Velocity.Z,
                    rock.Lifetime, rock.Scale, rock.Angle, rock.AngularVelocity, rock.PostScale, rock.SemiTransparencyRate, rock.GroundY,
                    rock.BounceCount, rock.Alive ? 1 : 0, rock.TextureFrame];
                long[] expected = state.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(expected), $"Rock PC case{caseIndex}, update{update}, input[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
                assert(rock.CodeBgr == 0x2C808080 && rock.TextureAnimation == 14, "Original rock retains neutral opaque colour and source texture animation14 without fade.");
                update++;
            }
            caseIndex++;
        }
        foreach (bool hit in new[] { false, true })
        {
            var host = new Host(hit); var position = new FixedVector3(1, int.MaxValue - 2, 3);
            BouncingRockEffects.MakeRocks(position, BouncingRockState.FireSelector, host, _ => 0);
            assert(host.Flags == 1 && host.From == position && host.To == (position with { Y = unchecked(position.Y + 0x1388000) }), "Web_GetGroundY preserves 5000-unit ray, wrapping endY and original flags1.");
            assert(host.Queries == 1 && host.Rocks.Count == 10 && host.Rocks.All(rock => rock.GroundY == (hit ? 77 : host.To.Y)), "MakeRocks queries once and emits ten real states; no-hit returns endY.");
        }
        bool rejected = false;
        try { _ = new BouncingRockState(default, 0, 0, _ => throw new Exception("RNG must not run")); } catch (ArgumentOutOfRangeException) { rejected = true; }
        assert(rejected, "Undefined rock texture selectors are rejected before RNG or resources.");
    }
    private sealed class Host(bool hit) : IBouncingRockHost
    {
        public int Queries, Flags;
        public FixedVector3 From, To;
        public List<BouncingRockState> Rocks { get; } = [];
        public FixedVector3? TraceGround(FixedVector3 from, FixedVector3 to, int flags) { Queries++; From = from; To = to; Flags = flags; return hit ? new(9, 77, 8) : null; }
        public void EmitRock(BouncingRockState rock) { if (Queries != 1) throw new InvalidOperationException("Ground must precede construction."); Rocks.Add(rock); }
    }
}

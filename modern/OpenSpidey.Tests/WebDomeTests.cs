using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class WebDomeTests
{
    private sealed class Host(int[] input) : IWebDomeHost
    {
        private int cursor = input[8];
        public List<long> Events { get; } = [];
        public FixedVector3 PlayerPosition { get; set; } = new(input[0], input[1], input[2]);
        public int Frame { get; set; }
        public bool Expired { get; set; }
        public int FireModelVertexCount => 5;
        public int Random(int bound) { int value = (cursor++ * 7 + 1) % bound; Events.AddRange([1, bound, value]); return value; }
        public WebDomePlayer? RecoverPlayer()
        { Events.AddRange([3, Expired ? 0 : 1]); return Expired ? null : new(PlayerPosition, (ushort)input[3], (short)(input[5] + Frame)); }
        public void AttachDome(WebDome dome) => Events.AddRange([2, 4, dome.FireVariant != 0 ? 1 : 0, 5]);
        public void DetachDome(WebDome dome) => throw new InvalidOperationException("Ctor/AI fixture does not execute the separately owned native cleanup phase.");
        public void AttachPiece(WebDomePiece piece) => Events.AddRange([6, piece.ModelIndex, piece.Delay, piece.FireVariant]);
        public void DetachPiece(WebDomePiece piece) => throw new InvalidOperationException("Ctor/AI fixture does not execute the separately owned native cleanup phase.");
        public FixedVector3 FireModelVertexWorldPosition(WebDome dome, int index) =>
            ((dome.Position >> 12) + new FixedVector3(index * 23 - 31, index * 17 - 13, index * 11 + 7)) << 12;
        public void EmitFirePulse(FixedVector3 position, int scale, Func<int, int> random) => Events.AddRange([10, position.X, position.Y, position.Z, scale]);
        public void ShatterTrappedPiece(WebDomePiece piece, int first, int second) => Events.AddRange([7, first, second, piece.ItemFlags]);
        public void EmitShockWave(int fireVariant, Func<int, int> random) => Events.AddRange([8, fireVariant]);
        public void EmitRing(FixedVector3 position, int fireVariant) => Events.AddRange([9, position.X, position.Y, position.Z, fireVariant]);
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebDomeTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-domes.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var host = new Host(input);
            var dome = new WebDome(new(host.PlayerPosition, (ushort)input[3], (short)input[5]), input[4], host);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0)
                {
                    host.Events.Clear();
                    if ((dome.BodyFlags & 0x40) == 0)
                    {
                        host.Frame = frame; host.PlayerPosition = new FixedVector3(input[0], input[1], input[2]) + new FixedVector3(frame * 1234, frame * -567, frame * 890);
                        host.Expired = frame == input[6];
                        foreach (WebDomePiece? piece in dome.Pieces) piece?.AdvanceActor();
                        dome.AdvanceActor(host.Random);
                        if (frame == input[7] && (dome.BodyFlags & 0x40) == 0) dome.Burst(host.Random);
                    }
                }
                long[] actual = [dome.Position.X, dome.Position.Y, dome.Position.Z, dome.Scale.X, dome.Scale.Y,
                    dome.Scale.Z, dome.ModelIndex, dome.FireVariant, dome.Mode, dome.ItemFlags, dome.BodyFlags];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(11).Select(value => value.GetInt64())),
                    $"CDome actualPC case {index} frame {frame}: source unsigned clearance, follow/expired player, fireY600 growth and exact mode/flag transitions.");
                for (int slot = 0; slot < dome.Pieces.Count; slot++)
                {
                    WebDomePiece? piece = dome.Pieces[slot]; JsonElement wanted = expected[11][slot];
                    if (piece is null) assert(wanted.ValueKind == JsonValueKind.Null, "CDome creates only source-defined ordinary/fire pieces.");
                    else
                    {
                        long[] fields = [piece.ModelIndex, piece.Delay, piece.TintStep, piece.Position.X, piece.Position.Y,
                            piece.Position.Z, piece.Tint, piece.ItemFlags, piece.BodyFlags];
                        assert(fields.SequenceEqual(wanted.EnumerateArray().Select(value => value.GetInt64())),
                            $"CDome actualPC case {index} frame {frame} piece {slot}: exact birth model/delay/flag32, follow and RGB-controlled hide/reveal.");
                    }
                }
                assert(host.Events.SequenceEqual(expected[12].EnumerateArray().Select(value => value.GetInt64())),
                    $"CDome actualPC case {index} frame {frame}: real piece order, source vertex pulse and trapped-item/shock/ring burst call order.");
                frame++;
            }
            index++;
        }
    }
}

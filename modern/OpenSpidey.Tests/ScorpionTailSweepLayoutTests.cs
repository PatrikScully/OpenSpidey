using System.Text.Json;
using OpenSpidey.Assets.Models;

internal static class ScorpionTailSweepLayoutTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionTailSweepLayoutTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-tail-sweep.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        IReadOnlyList<uint> palette = ScorpionTailSweepLayout.CreatePalette();
        IReadOnlyList<PsxFace> faces = ScorpionTailSweepLayout.CreateFaces();
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            JsonElement expected = row.GetProperty("expected");
            using var regions = new PsxAssetRegions(new Dictionary<string, PsxAssetLibrary>());
            PsxDynamicRegionLease[] occupied = Enumerable.Range(0, 40).Select(_ => regions.AllocateDynamic("BUSY", () => { })).ToArray();
            uint low = row.GetProperty("occupancy")[0].GetUInt32(), high = row.GetProperty("occupancy")[1].GetUInt32();
            for (int slot = 0; slot < occupied.Length; slot++)
            {
                bool busy = slot < 32 ? ((low >> slot) & 1) != 0 : ((high >> (slot - 32)) & 1) != 0;
                if (!busy) occupied[slot].Dispose();
            }
            bool released = false;
            PsxDynamicRegionLease sweep = regions.AllocateDynamic("SW", () => released = true);
            assert(sweep.Slot == expected.GetProperty("region").GetInt32() && regions.IsProtected(sweep) && sweep.IsActive && !released,
                "The shared typed region table must reproduce the whole original489370 first-free slot over every0..39 prefix and authored hole pattern, with source protected ownership.");
            long[][] descriptors = faces.Select(face => new long[] { face.Flags, 16,
                face.VertexIndices[0], face.VertexIndices[1], face.VertexIndices[2], face.VertexIndices[3], face.Color }).ToArray();
            assert(palette.Select(value => (long)value).SequenceEqual(Numbers(expected.GetProperty("palette"))) &&
                descriptors.SelectMany(value => value).SequenceEqual(expected.GetProperty("faces").EnumerateArray().SelectMany(Numbers)),
                $"Whole original489370 sweep layout{index++} preserves256 indexed green colours and all132 ordered double-sided additive quad descriptors, including each band's distinct palette gradient.");
            assert(Numbers(expected.GetProperty("header")).SequenceEqual(new long[] { 8, 92, 0, 132, 32767, 65535, 0x200000 }) &&
                expected.GetProperty("verticesUntouched").GetBoolean() && faces.All(face => face.TextureChecksum is null && face.TextureCoordinates.Length == 0),
                "The actual PC sweep initialization owns92 untouched vertex slots, no normals and untextured additive faces; it establishes no geometry update or draw phase.");
            assert(Numbers(expected.GetProperty("regionStores")).SequenceEqual(new long[] { 'S', 'W', 0, 0, 1, 1, 0, 1, 1, 1 }),
                "The source sweep's private SW slot stores its actual one-model/palette identities independently of the primary S tail.");
        }
    }

    private static IEnumerable<long> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
}

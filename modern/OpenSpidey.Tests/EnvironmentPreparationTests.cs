using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.World;

internal static class EnvironmentPreparationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(EnvironmentPreparationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.environment-preparation.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.GetProperty("boundaries").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(test.GetProperty("source").GetString()!);
            PsxScene scene = PsxSceneReader.Read(bytes);
            DecodedTexture[] textures = Enumerable.Range(0, 2).Select(index => new DecodedTexture(index == 0 ? 0x10Au : 0x20Au,
                64, 64, test.GetProperty("textureFlags")[index].GetUInt32(), [])
                { SourceFormat = test.GetProperty("textureFormats")[index].GetUInt32() }).ToArray();
            IReadOnlyList<PreparedEnvironmentModel> prepared = PsxEnvironmentPreparation.Prepare(scene, bytes, textures);
            for (int index = 0; index < prepared.Count; index++)
            {
                PreparedEnvironmentModel actual = prepared[index];
                long[] expected = test.GetProperty("expected")[index].EnumerateArray().Select(value => value.GetInt64()).ToArray();
                ushort itemFlags = scene.Instances[index].Flags;
                if ((actual.Model.Flags & 0x10) != 0) itemFlags |= 0x20;
                if ((actual.Model.Flags & 0x20) != 0) itemFlags |= 0x1000;
                long[] values = [actual.Model.Flags, itemFlags, actual.Model.Faces[0].Flags, actual.Model.Faces[0].CollisionWord!.Value,
                    actual.RendererFlags, actual.RendererFaceFlags[0]];
                string context = $"Whole original M3dInit4534A0/DCModel431430 version{scene.Version:X} model{index} rawface{scene.Models[index].Faces[0].Flags:X} packed{scene.Models[index].Faces[0].CollisionWord:X}";
                assert(values.SequenceEqual(expected), context + " matches prepared source model/item/face flags, source normal byte offset, actual DCFace and full DCModel draw flags. Actual[" + string.Join(',', values) + "] expected[" + string.Join(',', expected) + "].");
                var live = new EnvironmentChunkModelState(actual);
                assert(live.Flags == actual.Model.Flags && live.RendererFlags == actual.RendererFlags && live.Faces[0].Flags == actual.Model.Faces[0].Flags &&
                    live.Faces[0].RendererFlags == actual.RendererFaceFlags[0] && live.Revision == 0,
                    context + " constructs a typed paired source/DC face owner without changing immutable archive records.");
                live.Faces[0].Flags |= 0x80;
                live.Faces[0].CollisionWord &= ~0x10000u;
                assert(live.RenderSnapshot().Faces[0].Flags == (actual.Model.Faces[0].Flags | 0x80) &&
                    live.CollisionSnapshot().Faces[0].CollisionWord == (scene.Models[index].Faces[0].CollisionWord!.Value & ~0x10000u) &&
                    scene.Models[index].Faces[0].Flags == PsxSceneReader.Read(bytes).Models[index].Faces[0].Flags,
                    context + " publishes genuine chunk source edits while native collision snapshots retain index-based normals and exact modified high metadata.");
            }
        }
    }
}

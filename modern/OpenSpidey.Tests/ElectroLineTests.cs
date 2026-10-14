using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ElectroLineTests
{
    public static void Run(Action<bool, string> assert)
    {
        ElectroLineParameters parameters = new(12, 80, 80, 255, 25, 40, 15);
        uint[] textures = [0x7fe69d8du, 0xdf5869f0u, 0x959215adu];
        var start = new FixedVector3(-508, -259, -502) << 12;
        var end = new FixedVector3(506, -259, -503) << 12;
        var line = new ElectroLineState(4, 4, 5, start, end, parameters, textures);
        assert(line.OwnerNode == 4 && line.DamageParam == 15 && line.Spine.Length == 13,
            "CElectroLine seeds owner node, field_68 damage and NumFaces+1 spine points.");
        assert(line.Spine[0] == start && line.Spine[^1] == end && line.RibbonPositions[0] == start && line.RibbonPositions[^1] == end,
            "Constructor copies Trig_GetPosition start/end into both spine arrays.");
        FixedVector3 step = (end - start) / 12;
        for (int i = 1; i < 12; i++)
            assert(line.Spine[i] == start + step * i && line.RibbonPositions[i] == line.Spine[i],
                $"Face{i} is linearly interpolated (end-start)/NumFaces.");
        assert(line.Colours.All(colour => colour == 0x00FF5050u) && line.RibbonWidths.All(width => width == 25),
            "Setup packs RGB and SetWidth across every ribbon point.");

        var bounds = new List<int>();
        int Random(int bound) { bounds.Add(bound); return bound <= 1 ? 0 : 1; }
        line.AdvanceRibbon(Random);
        var expectedBounds = new List<int>();
        for (int i = 0; i < 11; i++) expectedBounds.AddRange([81, 192, 56]);
        expectedBounds.Add(3);
        assert(bounds.SequenceEqual(expectedBounds),
            "CElectro::Move consumes Rnd(2*field_64+1), Rnd(192), Rnd(56) per intermediate, then Rnd(NumTextures).");
        assert(line.FaceTextureIndex == 1 && line.RibbonPositions[0] == start && line.RibbonPositions[^1] == end,
            "Move reseeds endpoints and selects a texture index.");

        line.Advance(_ => 0, []);
        assert(line.Alive, "DamageParam15 with no colliding body does not invent a Hit.");
        bool missingBodies = false;
        try { line.Advance(_ => 0, null); }
        catch (NotSupportedException error) { missingBodies = error.Message.Contains("0x439B50", StringComparison.Ordinal); }
        assert(missingBodies, "DamageParam15 without a player body list keeps an explicit NotSupportedException.");

        using Stream stream = typeof(ElectroLineTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.l4a1-electro-triggers.trg")!;
        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        TriggerFile triggers = TriggerFile.Read(bytes);
        ElectroLineParameters parsed = EffectsCreateElectroLines.ReadParameters(triggers.Nodes[4]);
        assert(parsed == parameters, "L4A1 node4 after-position layout matches Effects_CreateElectroLines pData+8/+params.");
        IReadOnlyList<ElectroLineState> created = EffectsCreateElectroLines.Create(triggers, 4, textures);
        assert(created.Count == 1 && created[0].StartNode == 4 && created[0].EndNode == 5 && created[0].OwnerNode == 4,
            "L4A1 Effects_CreateElectroLines builds one CElectroLine for the 4→5 hop and stops on zero links.");
        assert(created[0].Spine[0] == start && created[0].Spine[^1] == end,
            "Created line uses Trig_GetPosition whole units << 12 for both endpoints.");
        created[0].Die();
        assert(!created[0].Alive, "SendKill SpecialDisplay Die clears the CElectroLine.");
    }
}

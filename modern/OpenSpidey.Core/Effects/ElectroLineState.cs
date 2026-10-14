using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Core.Effects;

/// <summary>Authored type409 parameter block after Trig_GetPosition's returned pointer (effects.cpp 0x439CC0).</summary>
public readonly record struct ElectroLineParameters(
    int FaceCount, byte Red, byte Green, byte Blue, ushort Width, ushort WidthVariance, ushort DamageParam);

/// <summary>One CElectroLine (0x4398B0): SpecialDisplay mType9, owner node at field_6A, optional damage at field_68.</summary>
public sealed class ElectroLineState
{
    public const byte SpecialDisplayType = 9;
    public ushort OwnerNode { get; }
    public ushort StartNode { get; }
    public ushort EndNode { get; }
    public ushort DamageParam { get; }
    public ushort Width { get; }
    public ushort WidthVariance { get; }
    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }
    public IReadOnlyList<uint> TextureChecksums { get; }
    public FixedVector3[] Spine { get; }
    public FixedVector3[] RibbonPositions { get; }
    public int[] RibbonOffsetsX { get; }
    public int[] RibbonOffsetsY { get; }
    public int[] RibbonOffsetsZ { get; }
    public uint[] RibbonWidths { get; }
    public uint[] Colours { get; }
    public int FaceTextureIndex { get; private set; }
    public bool Alive { get; private set; } = true;

    public ElectroLineState(ushort ownerNode, ushort startNode, ushort endNode, FixedVector3 start, FixedVector3 end,
        ElectroLineParameters parameters, IReadOnlyList<uint> textureChecksums)
    {
        ArgumentNullException.ThrowIfNull(textureChecksums);
        if (parameters.FaceCount <= 0) throw new ArgumentOutOfRangeException(nameof(parameters), "Zero NumFaces");
        if (textureChecksums.Count == 0 || textureChecksums.Count >= 20)
            throw new ArgumentOutOfRangeException(nameof(textureChecksums), "Suspicious NumTextures");
        OwnerNode = ownerNode; StartNode = startNode; EndNode = endNode; DamageParam = parameters.DamageParam;
        Width = parameters.Width; WidthVariance = parameters.WidthVariance;
        Red = parameters.Red; Green = parameters.Green; Blue = parameters.Blue;
        TextureChecksums = textureChecksums.ToArray();
        int faces = parameters.FaceCount, points = faces + 1;
        Spine = new FixedVector3[points];
        RibbonPositions = new FixedVector3[points];
        RibbonOffsetsX = new int[points]; RibbonOffsetsY = new int[points]; RibbonOffsetsZ = new int[points];
        RibbonWidths = new uint[points];
        Colours = new uint[points];
        uint packed = unchecked((uint)(parameters.Red | (parameters.Green << 8) | (parameters.Blue << 16)));
        FixedVector3 step = (end - start) / faces;
        FixedVector3 pos = start;
        Spine[0] = start; RibbonPositions[0] = start; RibbonWidths[0] = parameters.Width; Colours[0] = packed;
        for (int i = 0; i < faces - 1; i++)
        {
            pos += step;
            Spine[i + 1] = pos; RibbonPositions[i + 1] = pos; RibbonWidths[i + 1] = parameters.Width; Colours[i + 1] = packed;
        }
        Spine[faces] = end; RibbonPositions[faces] = end; RibbonWidths[faces] = parameters.Width; Colours[faces] = packed;
        FaceTextureIndex = 0;
    }

    public void Die() => Alive = false;

    /// <summary>CElectro::Move (0x439670): reseeds endpoints, jitters intermediates, randomizes RGB/widths, picks a face texture.</summary>
    public void AdvanceRibbon(Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!Alive) throw new InvalidOperationException("A deleted source SpecialDisplay cannot be updated.");
        int faces = Spine.Length - 1;
        uint baseColour = Colours[0];
        byte red = unchecked((byte)baseColour), blue = unchecked((byte)(baseColour >> 16)), green = unchecked((byte)(baseColour >> 8));
        uint baseWidth = RibbonWidths[0];
        RibbonPositions[0] = Spine[0];
        RibbonPositions[faces] = Spine[faces];
        for (int i = 1; i < faces; i++)
        {
            int jitter = unchecked(random(2 * WidthVariance + 1) - WidthVariance);
            RibbonPositions[i] = new(
                unchecked(Spine[i].X + jitter * RibbonOffsetsX[i]),
                unchecked(Spine[i].Y + jitter * RibbonOffsetsY[i]),
                unchecked(Spine[i].Z + jitter * RibbonOffsetsZ[i]));
            int scale = unchecked(random(192) + 64);
            Colours[i] = unchecked((uint)(((scale * green) & ~0xFF) | ((scale * red) >> 8) | (((scale * blue) & ~0xFF) << 8)));
            RibbonWidths[i] = unchecked((baseWidth * (uint)(random(56) + 200)) >> 8);
        }
        FaceTextureIndex = random(TextureChecksums.Count);
    }

    /// <summary>CElectroLine::Move (0x439B50): ribbon update, then optional player-segment Hit when field_68 != 0.</summary>
    public void Advance(Func<int, int> random, IReadOnlyList<ActorLaserBody>? playerBodies)
    {
        AdvanceRibbon(random);
        if (DamageParam == 0) return;
        if (playerBodies is null)
            throw new NotSupportedException("CElectroLine::Move (0x439B50) damage probe needs the live player body list.");
        FixedVector3 start = RibbonPositions[0], end = RibbonPositions[^1];
        if (ActorLaserCollision.CheckBodies(start, end, playerBodies, radiusScale: 4096) is null) return;
        throw new NotSupportedException("CElectroLine::Move player Hit (0x439B50) is not ported yet.");
    }
}

/// <summary>Effects_CreateElectroLines (0x439CC0): one CElectroLine per first-link hop until the chain ends or returns.</summary>
public static class EffectsCreateElectroLines
{
    public static IReadOnlyList<ElectroLineState> Create(TriggerFile triggers, ushort nodeId, IReadOnlyList<uint> boltChecksums)
    {
        ArgumentNullException.ThrowIfNull(triggers); ArgumentNullException.ThrowIfNull(boltChecksums);
        if (nodeId >= triggers.Nodes.Count) throw new ArgumentOutOfRangeException(nameof(nodeId));
        TriggerNode node = triggers.Nodes[nodeId];
        if (node.TypeCode != 1) throw new InvalidDataException("Node is not an electroline");
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != 409) throw new InvalidDataException("Node is not an electroline");
        ElectroLineParameters parameters = ReadParameters(node);
        if (boltChecksums.Count != 3) throw new InvalidDataException("Electrolines isn't composed of 3 Textures.");
        var lines = new List<ElectroLineState>();
        ushort current = nodeId;
        for (int guard = 0; guard < triggers.Nodes.Count + 1; guard++)
        {
            IReadOnlyList<ushort> links = MissionRuntime.ReadLinks(triggers.Nodes[current]);
            if (links.Count == 0) break;
            ushort next = links[0];
            lines.Add(new(nodeId, current, next, Position(triggers, current), Position(triggers, next), parameters, boltChecksums));
            current = next;
            if (next == nodeId) break;
        }
        return lines;
    }

    public static ElectroLineParameters ReadParameters(TriggerNode node)
    {
        TriggerPlacement placement = TriggerPlacement.Read(node);
        ReadOnlySpan<byte> after = node.Data.Span[placement.AfterPositionOffset..];
        if (after.Length < 12) throw new InvalidDataException("Electroline position block is truncated.");
        int faceCount = BinaryPrimitives.ReadInt32LittleEndian(after[8..]);
        int cursor = 12, textures = 0;
        if (after.Length >= 16 && BinaryPrimitives.ReadUInt32LittleEndian(after[12..]) != 0)
        {
            cursor = 16;
            while (cursor + 4 <= after.Length)
            {
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(after[cursor..]);
                textures++;
                cursor += 4;
                if (value == 0) break;
            }
        }
        if (textures != 3) throw new InvalidDataException("Electrolines isn't composed of 3 Textures.");
        if (cursor + 14 > after.Length) throw new InvalidDataException("Electroline parameter block is truncated.");
        return new(faceCount, after[cursor], after[cursor + 2], after[cursor + 4],
            BinaryPrimitives.ReadUInt16LittleEndian(after[(cursor + 6)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(after[(cursor + 8)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(after[(cursor + 12)..]));
    }

    private static FixedVector3 Position(TriggerFile triggers, ushort nodeId)
    {
        TriggerPosition position = TriggerPlacement.Read(triggers.Nodes[nodeId]).Position;
        return new FixedVector3(position.X, position.Y, position.Z) << 12;
    }
}

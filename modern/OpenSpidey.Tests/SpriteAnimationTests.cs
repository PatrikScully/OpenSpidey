using System.Buffers.Binary;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Player;

internal static class SpriteAnimationTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] bytes = Fixture();
        SpriteAnimation animation = SpriteAnimationReader.Read(bytes).Single();
        assert(animation.Name == "webknot" && animation.Frames[0] == new SpriteAnimationFrame(-12, -12, 24, 24, 0x12345678),
            "Sprite sequencers preserve signed frame offsets and resolve texture indices without pointer relocation.");
        assert(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(44)) == 0,
            "Sprite animation imports do not mutate the original texture index words.");
        byte[] noPacket = Fixture();
        Put(noPacket, 16, 7);
        assert(SpriteAnimationReader.Read(noPacket).Count == 0, "Assets without a sequencer record do not invent sprite animation data.");
        byte[] menuRectangle = Fixture(); menuRectangle[42] = 0; menuRectangle[43] = 0;
        assert(SpriteAnimationReader.Read(menuRectangle).Single().Frames[0].Width == 0,
            "Zero rectangle bytes in original menu sequencers survive import rather than being rejected or rewritten.");
        Reject(bytes.AsSpan(0, 43).ToArray(), assert);
        byte[] badTexture = Fixture(); Put(badTexture, 44, 1); Reject(badTexture, assert);
        byte[] badCount = Fixture(); Put(badCount, 36, 1000); Reject(badCount, assert);
        byte[] emptyName = Fixture(); Array.Clear(emptyName, 28, 8); Reject(emptyName, assert);
        byte[] oversizedPacket = Fixture(); Put(oversizedPacket, 20, 10000); Reject(oversizedPacket, assert);
        var frame = new SpriteAnimationFrame(-12, -12, 24, 24, 1);
        assert(FlatSpriteProjection.Project(frame, 176, 320) == new FlatSpriteRectangle(-7, -7, 13, 13),
            "Flat sprite perspective sizing preserves source integer shifts and signed offsets.");
        assert(FlatSpriteProjection.Project(frame, 176, 1000) == new FlatSpriteRectangle(-3, -3, 2, 1),
            "Distant flat sprites use the recovered two-pixel width and one-pixel height clamps.");
        assert(FlatSpriteProjection.Project(frame, 176, 199) is null && FlatSpriteProjection.Project(frame, 1000, 200) is null,
            "Flat sprite depth and projected-scale gates retain their original boundaries.");
    }

    private static byte[] Fixture()
    {
        byte[] bytes = new byte[60];
        Put(bytes, 0, 0x20006); Put(bytes, 4, 16); Put(bytes, 16, 69); Put(bytes, 20, 24);
        Put(bytes, 24, 1); "webknot"u8.CopyTo(bytes.AsSpan(28)); Put(bytes, 36, 1);
        bytes[40] = 244; bytes[41] = 244; bytes[42] = 24; bytes[43] = 24;
        Put(bytes, 48, uint.MaxValue); Put(bytes, 52, 1); Put(bytes, 56, 0x12345678);
        return bytes;
    }

    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Reject(byte[] bytes, Action<bool, string> assert)
    {
        try { SpriteAnimationReader.Read(bytes); }
        catch (InvalidDataException) { assert(true, "Malformed sprite sequencers are rejected with a source-data diagnostic."); return; }
        throw new Exception("Malformed sprite sequencer was accepted.");
    }
}

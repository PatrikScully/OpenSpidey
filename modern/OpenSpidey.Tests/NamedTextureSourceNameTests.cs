using System.Text;
using OpenSpidey.Assets.Textures;

internal static class NamedTextureSourceNameTests
{
    public static void Run(Action<bool, string> assert)
    {
        foreach (char whitespace in new[] { '\t', '\n', '\v', '\f', '\r', ' ' })
        {
            byte[] manifest = Encoding.Latin1.GetBytes($"{whitespace}0x1234{whitespace}D:\\Art\\Boss HealthBarEnd : 32x32 pixels\r\n0x5678 D:\\Other\\BOSS : 32x32 pixels");
            assert(NamedTextureTable.FindFirstSourceName(manifest, "boss") == 0x1234,
                "Original sscanf tokenization uses exactly its ASCII whitespace and the first lowercase Windows basename, including the Boss label's embedded space.");
        }
        byte[] paths = Encoding.Latin1.GetBytes("0x8 D:\\Parent\\Scorpion : 64x64 pixels\r\n0x9 D:\\Duplicate\\scorpion : 64x64 pixels\r0xA D:\\Parent\\scorpion_wounded : 64x64 pixels");
        assert(NamedTextureTable.FindFirstSourceName(paths, "SCORPION") == 8 && NamedTextureTable.FindFirstSourceName(paths, "scorpion_wounded") == 10,
            "Source lookup retains first-match checksum order, its final backslash basename and case-insensitive name comparison.");
        byte[] unicode = Encoding.Latin1.GetBytes("0xB D:\\Parent\\Boss\u00A0HealthBarEnd : 32x32 pixels");
        Reject<KeyNotFoundException>(() => NamedTextureTable.FindFirstSourceName(unicode, "boss"));
        assert(NamedTextureTable.FindFirstSourceName(unicode, "Boss\u00A0HealthBarEnd") == 11,
            "A Latin1 nonbreaking space belongs to the original filename token; it must not be treated as Unicode whitespace.");
        byte[] longName = Encoding.Latin1.GetBytes("0xC D:\\Parent\\abcdefghijklmnopqrstuvwxyz01234 : 32x32 pixels\r\n0xD D:\\Parent\\abcdefghijklmnopqrstuvwxyz0123456789 : 32x32 pixels");
        assert(NamedTextureTable.FindFirstSourceName(longName, "abcdefghijklmnopqrstuvwxyz01234") == 12,
            "A31-byte name fits the original32-byte table with its genuine terminating zero.");
        Reject<NotSupportedException>(() => NamedTextureTable.FindFirstSourceName(longName, "abcdefghijklmnopqrstuvwxyz012345"));
        byte[] loneLf = Encoding.Latin1.GetBytes("0xE D:\\Parent\\Other : 32x32 pixels\n0xF D:\\Parent\\Boss : 32x32 pixels");
        Reject<KeyNotFoundException>(() => NamedTextureTable.FindFirstSourceName(loneLf, "boss"));
        byte[] nullTerminated = Encoding.Latin1.GetBytes("0xE D:\\Parent\\Other : 32x32 pixels\0\r\n0xF D:\\Parent\\Boss : 32x32 pixels");
        Reject<KeyNotFoundException>(() => NamedTextureTable.FindFirstSourceName(nullTerminated, "boss"));
        byte[] forwardSlash = Encoding.Latin1.GetBytes("0x10 D:\\Parent\\Nested/Scorpion : 32x32 pixels");
        assert(NamedTextureTable.FindFirstSourceName(forwardSlash, "Nested/Scorpion") == 16,
            "Original basename extraction recognizes the last backslash and retains forward slashes within the name token.");
        Reject<KeyNotFoundException>(() => NamedTextureTable.FindFirstSourceName(forwardSlash, "scorpion"));
        byte[] inspection = Encoding.Latin1.GetBytes("0x488ECF41 D:\\Art\\Boss HealthBarEnd : 32x32 pixels");
        assert(NamedTextureTable.FindFirst(inspection, "Boss HealthBarEnd") == 0x488ECF41 &&
            NamedTextureTable.FindFirstSourceName(inspection, "boss") == 0x488ECF41,
            "The inspection API retains the complete authored filename while source lookup reproduces texLoadChecksums' sscanf token.");

        void Reject<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { assert(true, "Source texture names reject an absent token match."); return; }
            throw new Exception($"Expected {typeof(T).Name} for absent source texture name.");
        }
    }
}

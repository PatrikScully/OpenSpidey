using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;

internal static class ActorJointPoseTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorJointPoseTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.actor-joint-poses.json")
            ?? throw new InvalidDataException("Source mutable joint fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        var packets = new Dictionary<string, ActorPoseLinkPacket>();
        foreach (JsonElement entry in document.RootElement.GetProperty("packets").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(entry.GetProperty("bytes").GetString()!);
            JsonElement expected = entry.GetProperty("links");
            ActorPoseLinkPacket packet = ActorPoseLinkPacket.Read(bytes, expected.GetArrayLength());
            assert(packet.HeaderWord == 0 && packet.Links.Count == expected.GetArrayLength(), "Source pose packet count is its second halfword.");
            for (int index = 0; index < packet.Links.Count; index++)
            {
                short[] values = Values(expected[index]);
                assert(packet.Links[index] == new ActorPoseLink(unchecked((ushort)values[0]), unchecked((ushort)values[1]),
                    values[2], values[3], values[4], unchecked((ushort)values[5])), "Original joint-to-part order and first-match parent link resolution match original packets.");
            }
            for (int length = 0; length < bytes.Length; length++)
            {
                int truncated = length;
                Reject(() => ActorPoseLinkPacket.Read(bytes.AsSpan(0, truncated), expected.GetArrayLength()), assert);
            }
            byte[] invalid = bytes.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(4), checked((ushort)expected.GetArrayLength()));
            Reject(() => ActorPoseLinkPacket.Read(invalid, expected.GetArrayLength()), assert);
            packets.Add(entry.GetProperty("name").GetString()!, packet);
        }
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            FixedJointTransform[] key = test.GetProperty("key").EnumerateArray().Select(Pose).ToArray();
            RawJointPose[] joints = test.GetProperty("joints").EnumerateArray().Select(value =>
            {
                short[] fields = Values(value);
                return new RawJointPose(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5]);
            }).ToArray();
            var actual = new FixedJointTransform[key.Length];
            ActorJointPoseBuilder.Apply(key, packets[test.GetProperty("packet").GetString()!].Links, joints, actual,
                test.GetProperty("interpolated").GetBoolean());
            FixedJointTransform[] expected = test.GetProperty("expected").EnumerateArray().Select(Pose).ToArray();
            for (int part = 0; part < actual.Length; part++)
                assert(actual[part] == expected[part], $"Original PC BuildPose case {caseIndex}, part {part}: actual={actual[part]}, expected={expected[part]}.");
            caseIndex++;
        }
    }
    private static short[] Values(JsonElement element) => element.EnumerateArray().Select(v => unchecked((short)v.GetInt32())).ToArray();
    private static FixedJointTransform Pose(JsonElement element)
    {
        short[] v = Values(element);
        return new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11]);
    }
    private static void Reject(Action action, Action<bool, string> assert)
    {
        try { action(); assert(false, "Malformed source pose packet was accepted."); }
        catch (InvalidDataException) { assert(true, "Malformed source pose packet is bounded."); }
    }
}

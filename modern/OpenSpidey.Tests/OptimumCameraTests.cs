using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

internal static class OptimumCameraTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(OptimumCameraTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.optimum-camera.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement expected = test.GetProperty("expected");
            if (test.GetProperty("mode").GetString() == "E")
            {
                JsonElement angles = test.GetProperty("angles");
                FixedQuaternion actual = SourceCameraEulerRotation.FromAngles(new ActorAngles(
                    angles[0].GetInt16(), angles[1].GetInt16(), angles[2].GetInt16()));
                var target = new FixedQuaternion(expected[0].GetInt32(), expected[1].GetInt32(),
                    expected[2].GetInt32(), expected[3].GetInt32());
                assert(actual == target, $"Optimum camera case{index}: original4164F0 Euler up/back/cross/MToQ target actual{actual}, expected{target}.");
            }
            else
            {
                var host = new Host(test);
                JsonElement points = test.GetProperty("points");
                int selected = new OptimumViewingNodeSelector(host).Select(Vector(points[0]), Vector(points[1]));
                assert(selected == expected[0].GetInt32() && (host.CameraCollisionCheck ? 1 : 0) == expected[1].GetInt32(),
                    $"Optimum camera case{index}: source file order, signed yaw, distance512..4096, strict positive winner and camera-mask lifetime actual{selected}/{host.CameraCollisionCheck}, expected{expected[0]}/{expected[1]}.");
                int[] trace = expected[2].EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(host.Events.SequenceEqual(trace),
                    $"Optimum camera case{index}: original ray endpoints/order/policy, per-ray penalties and each successive fixed target actual[{string.Join(',', host.Events)}], expected[{string.Join(',', trace)}].");
                assert(host.Dispatch.SequenceEqual(new[] { SourceVisibilityList.StaticEnvironment, SourceVisibilityList.Baddy }),
                    $"Optimum camera case{index}: restores the two original active lists before evaluating a node, even with no candidates.");
                for (int list = 0; list < host.Items.Length; list++)
                    assert(host.Items[list].Select(item => (int)item.ItemFlags).SequenceEqual(expected[3][list].EnumerateArray().Select(value => value.GetInt32())),
                        $"Optimum camera case{index}, list{list}: untouched4E6BC0 restores flag0 with original direct Q12 bounds, int.MinValue exclusion and loaded-region policy.");
            }
            index++;
        }
        assert(SourceCameraEulerRotation.FromAngles(new AimAngles(0, 0)) == new FixedQuaternion(0, 4096, 0, 0),
            "Zero source Euler camera angles retain the original half-turn quaternion convention.");
    }

    private static FixedVector3 Vector(JsonElement value, int start = 0) => new(value[start].GetInt32(),
        value[start + 1].GetInt32(), value[start + 2].GetInt32());

    private sealed class Host : IOptimumViewingCameraHost
    {
        private readonly JsonElement nodes;
        private int currentNode, ray;
        public bool CameraCollisionCheck { get; set; }
        public List<int> Events { get; } = [];
        public List<SourceVisibilityList> Dispatch { get; } = [];
        public Item[][] Items { get; }
        public Host(JsonElement test)
        {
            nodes = test.GetProperty("nodes");
            CameraCollisionCheck = test.GetProperty("initialCollision").GetInt32() != 0;
            Items = Enumerable.Range(0, 2).Select(_ => test.GetProperty("items").EnumerateArray().Select(
                item => new Item(Vector(item), item[3].GetUInt16(), item[4].GetInt32() != 0)).ToArray()).ToArray();
        }
        public IEnumerable<SourceViewingNode> SourceNodes
        {
            get
            {
                for (int id = 0; id < nodes.GetArrayLength(); id++)
                {
                    currentNode = id; ray = 0;
                    yield return new(id, nodes[id][0].GetUInt16(), Vector(nodes[id], 1));
                }
            }
        }
        public IEnumerable<IVisibilityBoxItem> SourceItems(SourceVisibilityList list)
        { Dispatch.Add(list); return Items[list == SourceVisibilityList.StaticEnvironment ? 0 : 1]; }
        public bool CameraRayOccluded(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([2, currentNode, ray, start.X, start.Y, start.Z, end.X, end.Y, end.Z, 1, CameraCollisionCheck ? 1 : 0]);
            return (nodes[currentNode][4].GetInt32() & (1 << ray++)) != 0;
        }
        public void SetFixedCamera(FixedVector3 position, AimAngles angles) => Events.AddRange(
            [3, currentNode, position.X, position.Y, position.Z, angles.Pitch, angles.Yaw, 0, 0]);
    }

    private sealed class Item(FixedVector3 position, ushort flags, bool usable) : IVisibilityBoxItem
    {
        public FixedVector3 SourcePosition => position;
        public bool RegionUsable => usable;
        public ushort ItemFlags { get; private set; } = flags;
        public void SetVisible(bool visible) => ItemFlags = visible ? unchecked((ushort)(ItemFlags & ~1)) : (ushort)(ItemFlags | 1);
    }
}

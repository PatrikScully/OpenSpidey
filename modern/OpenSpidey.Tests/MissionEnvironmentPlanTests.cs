using System.Text;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class MissionEnvironmentPlanTests
{
    public static void Run(Action<bool, string> assert, string? assets)
    {
        var early = new TriggerFile([Autoexec("death", "L1A1_G"), Restart(1, "death", null)]);
        assert(MissionEnvironmentPlan.Read(early) == new MissionEnvironmentPlan("data/l1a1_g.psx", true),
            "Autoexec environment selection retains the actual command phase without reading a restart load.");
        var late = new TriggerFile([Autoexec("death", null), Restart(1, "death", "L2A1_G"), Restart(2, "saved", "L2A1_G")]);
        assert(MissionEnvironmentPlan.Read(late) == new MissionEnvironmentPlan("data/l2a1_g.psx", false) &&
            MissionEnvironmentPlan.Read(late, "SaVeD") == new MissionEnvironmentPlan("data/l2a1_g.psx", false),
            "Late geometry resolves the authored death or case-insensitive saved restart without moving its128 into autoexec.");
        var alternate = new TriggerFile([
            Autoexec("death", "L1A1_G"),
            Autoexec("alternate", null, 1, TriggerNodeKind.AlternativeAutoexec),
            Autoexec("ignored", "L3A1_G", 2, TriggerNodeKind.AlternativeAutoexec),
            Restart(3, "death", null), Restart(4, "alternate", "L2A1_G")]);
        assert(MissionEnvironmentPlan.Read(alternate) == new MissionEnvironmentPlan("data/l1a1_g.psx", true) &&
            MissionEnvironmentPlan.Read(alternate, alternativeAutoexec: true) == new MissionEnvironmentPlan("data/l2a1_g.psx", false),
            "Alternate planning selects only the first kind15 node and its own restart, matching the actual queue instead of combining unused environments.");
        assert(MissionEnvironmentPlan.Read(early, alternativeAutoexec: true) == MissionEnvironmentPlan.Read(early) &&
            MissionEnvironmentPlan.Read(late, "saved", alternativeAutoexec: true) == MissionEnvironmentPlan.Read(late, "saved"),
            "Without kind15, alternate startup falls back to every normal autoexec and retains saved-restart geometry.");
        var multiple = new TriggerFile([Autoexec("death", null), Autoexec("saved", null, 1),
            Restart(2, "death", "L1A1_G"), Restart(3, "saved", "L2A1_G")]);
        assert(MissionEnvironmentPlan.Read(multiple) == new MissionEnvironmentPlan("data/l2a1_g.psx", false),
            "The final normal autoexec restart selection determines a deferred environment in actual file order.");
        Reject(() => MissionEnvironmentPlan.Read(new([Autoexec("missing", null)])));
        Reject(() => MissionEnvironmentPlan.Read(new([Autoexec("death", null), Restart(1, "death", null)])));
        var host = new EndNodeHost();
        var command = new EndLevelNodeCommandHandler(host);
        command.Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0), new(302, 0, new byte[] { 6, 0 }));
        assert(host.Node == 6 && host.Calls == 1, "Source302 executes against its application global with no player or transition owner.");
        Reject(() => command.Execute(new(early.Nodes[0], 0), new(302, 0, ReadOnlyMemory<byte>.Empty)));

        if (assets is null) return;
        using var archive = new PkrArchive(Path.Combine(assets, "data.pkr"));
        foreach (string mission in new[] { "L1A1", "L1A2", "L1A3", "L1A4", "L2A1" })
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/" + mission + "_T.trg"));
            MissionEnvironmentPlan plan = MissionEnvironmentPlan.Read(triggers);
            assert(plan.AssetPath.Equals("data/" + mission + "_G.psx", StringComparison.OrdinalIgnoreCase) &&
                plan.LoadsInAutoexec == (mission != "L2A1") && archive.Contains(plan.AssetPath),
                $"Original {mission} resolves its real geometry and preserves its authored startup load phase.");
        }

        void Reject(Action operation)
        {
            try { operation(); }
            catch (Exception error) when (error is InvalidDataException or NotSupportedException)
            { assert(true, "Missing startup references or invalid operands fail explicitly."); return; }
            throw new Exception("Invalid startup metadata was accepted.");
        }
    }

    private static TriggerNode Autoexec(string restart, string? environment, ushort id = 0, TriggerNodeKind kind = TriggerNodeKind.Autoexec)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        writer.Write((ushort)kind); writer.Write((ushort)140); Name(writer, restart);
        if (environment is not null) { writer.Write((ushort)128); Name(writer, environment); }
        writer.Write(ushort.MaxValue);
        return new(id, (ushort)kind, 0, data.ToArray());
    }

    private static TriggerNode Restart(ushort node, string name, string? environment)
    {
        using var data = new MemoryStream(); using var writer = new BinaryWriter(data);
        writer.Write((ushort)8); writer.Write((ushort)0);
        writer.Write(0); writer.Write(0); writer.Write(0);
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0); Name(writer, name);
        if (environment is not null) { writer.Write((ushort)128); Name(writer, environment); }
        writer.Write(ushort.MaxValue);
        return new(node, 8, 0, data.ToArray());
    }

    private static void Name(BinaryWriter writer, string name)
    {
        writer.Write(Encoding.Latin1.GetBytes(name)); writer.Write((byte)0);
        if ((writer.BaseStream.Position & 1) != 0) writer.Write((byte)0);
    }

    private sealed class EndNodeHost : IMissionEndLevelNodeHost
    {
        public ushort Node { get; private set; }
        public int Calls { get; private set; }
        public void SetEndLevelNode(ushort node) { Node = node; Calls++; }
    }
}

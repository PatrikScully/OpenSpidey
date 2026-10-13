using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Actual player controller and original rig, compared with untouched PC matrix/cache sequences without a simulation step.</summary>
internal static partial class PlayerStartOrientationImportChecks
{
    private sealed record StartCase(short[] Matrix, FixedVector3 Normal, FixedVector3 Cache, short Yaw, bool Fresh, int[][] Expected);

    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, Func<GodotPlayer> playerFactory)
    {
        var viewport = new SubViewport { Name = "SourceRestartOrientationProof", OwnWorld3D = true,
            Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            GodotPlayer player = playerFactory(); root.AddChild(player); player.SetPhysicsProcess(false);
            var camera = new Camera3D { Current = true, Position = player.Position + new Vector3(0, 3, 8) }; root.AddChild(camera);
            camera.LookAt(player.GlobalPosition); camera.MakeCurrent();
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A1_T.trg"));
            RestartPoint restart = triggers.Nodes.Where(node => node.TypeCode == (ushort)TriggerNodeKind.Restart)
                .Select(RestartPoint.Read).Single(point => point.Name == "Re_Restart01");
            if (restart.Orientation.X != 3243 || restart.Orientation.Y != SourceCases[0].Yaw || restart.Orientation.Z != 0)
                throw new InvalidDataException("Native restart proof must retain the genuine authored pitch3243/yaw0 while SetStartOrientation reads only yaw.");
            FixedVector3 position = player.Controller.Position, velocity = player.Controller.Velocity;
            int checks = 0;
            for (int index = 0; index < SourceCases.Length; index++)
            {
                StartCase source = SourceCases[index];
                player.Controller.Orientation.Restore(new(Basis(source.Matrix), source.Cache, !source.Fresh));
                player.Combat.State.CrawlSurfaceNormal = source.Normal;
                player.Combat.State.IsCrawling = false;
                player.Controller.ApplyCombatState();
                for (int frame = 0; frame < source.Expected.Length; frame++)
                {
                    player.Controller.SetStartOrientation(index == 0 ? restart.Orientation.Y : source.Yaw);
                    player.RefreshCombatTransform();
                    int[] expected = source.Expected[frame];
                    FixedJointTransform expectedBasis = Basis(expected.Take(9).Select(value => (short)value).ToArray());
                    Transform3D expectedTransform = PsxCoordinates.CharacterTransform(expectedBasis);
                    expectedTransform.Origin = PsxCoordinates.FixedWorldPosition(position);
                    if (!Matrix(player.Controller.Orientation.Basis).SequenceEqual(expected.Take(9)) ||
                        player.Controller.Orientation.Capture().LastNormal != new FixedVector3(expected[9], expected[10], expected[11]) ||
                        player.Combat.State.ForwardDirection != new FixedVector3(expected[12], expected[13], expected[14]) ||
                        player.Combat.State.UpDirection != new FixedVector3(expected[18], expected[19], expected[20]) ||
                        player.Controller.Position != position || player.Controller.Velocity != velocity ||
                        !player.Transform.IsEqualApprox(expectedTransform) || !GodotObject.IsInstanceValid(player.Model))
                        throw new InvalidDataException($"Native source SetStartOrientation{index}/visit{frame} changed the original matrix/cache/axis writes, position/velocity or rendered body transform.");
                    if (expected[^1] != 0 && (player.Controller.Action != PlayerAction.Idle || player.Model.ClipId != 0))
                        throw new InvalidDataException("A degenerate source start matrix must invoke the actual stand-mode animation owner.");
                    checks++;
                }
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"Passed {checks} native source start-orientation visits: actual authored restart yaw, repeated current-matrix multiplication, nontrivial surface basis, cache16/17 threshold, genuine fallback/stand and body transform.");
        }
        finally { viewport.Free(); }
    }

    private static FixedJointTransform Basis(IReadOnlyList<short> m) => new(m[0], m[1], m[2], m[3], m[4], m[5], m[6], m[7], m[8], 0, 0, 0);
    private static int[] Matrix(FixedJointTransform m) => [m.M00, m.M01, m.M02, m.M10, m.M11, m.M12, m.M20, m.M21, m.M22];
}

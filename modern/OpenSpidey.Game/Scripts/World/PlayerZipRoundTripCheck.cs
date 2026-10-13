using Godot;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Schedules the original player through both zip directions and verifies body axes and exact skin storage.</summary>
internal sealed class PlayerZipRoundTripCheck(GodotPlayer player, int ceilingY, int floorY, string context,
    Action<bool, string> assert, Func<GodotPlayer, string, Task>? capture = null, bool requireVisibleKnots = false)
{
    private int stage;
    private Task? pendingCapture;
    private int lastVerifiedUpdate;
    private int poseSamples;
    private int strandSamples;
    private bool strandCaptured;
    private int splatSamples;
    private int knotSamples;
    public bool Finished { get; private set; }

    public void Advance()
    {
        if (Finished) return;
        if (player.SourceUpdates != lastVerifiedUpdate)
        {
            VerifyAxes();
            player.Model.VerifyCurrentPose();
            player.VerifyZipEffects();
            if (player.LiveZipStrands > 0) strandSamples++;
            if (player.LiveWebSplats > 0) splatSamples++;
            if (player.LiveWebKnots > 0) knotSamples++;
            lastVerifiedUpdate = player.SourceUpdates;
            poseSamples++;
        }
        if (pendingCapture is not null)
        {
            if (!pendingCapture.IsCompleted) return;
            pendingCapture.GetAwaiter().GetResult();
            pendingCapture = null;
            if (stage == 5) stage = 1;
            else if (stage == 3) { StartZip(); stage = 2; }
            else stage = 6;
            return;
        }
        if (stage == 6)
        {
            if (player.LiveWebSplats != 0) return;
            assert(splatSamples > 0, $"{context}: original impact splats rendered and retired after their source tint fade.");
            assert((!requireVisibleKnots || knotSamples > 0) && player.LiveWebKnots == 0,
                $"{context}: visible original knot sprites render when required and retire with their owner.");
            Complete();
            return;
        }
        if (capture is not null && stage == 1 && !strandCaptured && player.Controller.ZipWebVisible)
        {
            strandCaptured = true;
            stage = 5;
            pendingCapture = capture(player, "firing");
            return;
        }
        if (stage == 0)
        {
            if (player.SourceUpdates < 1 || !player.Controller.IsGrounded) return;
            StartZip();
            stage = 1;
            return;
        }
        if (player.Controller.Action != PlayerAction.Idle) return;
        if (stage == 1)
        {
            if (!player.Controller.OnCeiling) return;
            assert(Math.Abs((long)player.Controller.Position.Y - ceilingY) <= 4096 && player.Model.ClipId == 19 &&
                player.Controller.Orientation.Basis.M11 < -4090,
                $"{context}: the original player finishes the E zip with a complete ceiling basis and clearance.");
            VerifyAxes();
            player.Model.VerifyPlayback();
            if (capture is not null)
            {
                stage = 3;
                pendingCapture = capture(player, "ceiling");
                return;
            }
            StartZip();
            stage = 2;
            return;
        }
        if (player.Controller.OnCeiling) return;
        assert(!player.Controller.IsCrawling && player.Controller.IsGrounded &&
            Math.Abs((long)player.Controller.Position.Y - floorY) <= 4096 && player.Controller.Orientation.Basis.M11 > 4090 &&
            player.Controller.Velocity == default,
            $"{context}: the original actor zips back to the floor, rebuilds the floor basis and settles.");
        assert(strandSamples > 0 && player.LiveZipStrands == 0,
            $"{context}: the paired web polylines render during firing and retire after release.");
        VerifyAxes();
        player.Model.VerifyPlayback();
        if (capture is not null) { stage = 4; pendingCapture = capture(player, "floor"); }
        else stage = 6;
    }

    private void Complete()
    {
        Finished = true;
        GD.Print($"Verified {context} zip actor: {player.SourceUpdates} source updates, {poseSamples} live pose samples, floor/ceiling round trip and skin playback.");
    }

    private void VerifyAxes()
    {
        Basis basis = player.Transform.Basis;
        assert(Mathf.Abs(basis.Determinant() - 1) < 0.01f &&
            Mathf.Abs(basis.X.LengthSquared() - 1) < 0.01f && Mathf.Abs(basis.Y.LengthSquared() - 1) < 0.01f &&
            Mathf.Abs(basis.Z.LengthSquared() - 1) < 0.01f && Mathf.Abs(basis.X.Dot(basis.Y)) < 0.01f &&
            Mathf.Abs(basis.Y.Dot(basis.Z)) < 0.01f && Mathf.Abs(basis.Z.Dot(basis.X)) < 0.01f &&
            !player.Model.AdvanceAutomatically,
            $"{context}: zip preserves complete orthogonal actor axes and controller-owned animation.");
    }

    private void StartZip()
    {
        int update = player.SourceUpdates;
        player.SampleButtons = () => player.SourceUpdates == update ? new(PlayerButton.Zip, PlayerButton.Zip) : default;
    }
}

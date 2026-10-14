using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Game.UI;

namespace OpenSpidey.Game.World;

/// <summary>Private real-bank panel proof; it creates no production boss actors and never changes the mission's spool state.</summary>
internal static class ScorpionHealthBarImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive)
    {
        var libraries = new PsxLibraryRepository(archive);
        Reject<InvalidOperationException>(() => GodotScorpionHealthBarResources.Create(archive, libraries));
        Require(libraries.Loaded.Count == 0, "The presentation adapter must require authored loaded banks rather than preload a world.");
        libraries.Load("bits");
        libraries.Load("L2A2_G");
        foreach (Vector2I size in new[] { new Vector2I(640, 480), new Vector2I(1280, 720) })
            await VerifyViewportAsync(owner, archive, libraries, size);

        using var oldResources = GodotScorpionHealthBarResources.Create(archive, libraries);
        PsxAssetLibrary oldBank = oldResources.EncounterBank;
        ImageTexture[] oldUploads = oldResources.UploadedTextures.Select(texture => texture.Native).ToArray();
        libraries.Unload("L2A2_G");
        Require(oldResources.IsDisposed && oldUploads.All(texture => !GodotObject.IsInstanceValid(texture)),
            "Unloading the actual selected encounter bank must immediately invalidate and release every owned panel upload.");
        Reject<ObjectDisposedException>(() => oldResources.FindTexture("scorpion"));
        PsxAssetLibrary newBank = libraries.Load("L2A2_G");
        using var reloaded = GodotScorpionHealthBarResources.Create(archive, libraries);
        oldResources.Dispose();
        Require(!ReferenceEquals(oldBank, newBank) && ReferenceEquals(reloaded.EncounterBank, newBank) && !reloaded.IsDisposed &&
            reloaded.UploadedTextures.All(texture => GodotObject.IsInstanceValid(texture.Native)) &&
            !ReferenceEquals(oldBank.Textures.First(texture => texture.Checksum == 0x10F5DCCC), reloaded.FindTexture("scorpion").Source),
            "Reloaded bank generations must create fresh real source/cache/upload identities; stale disposal must preserve the replacement.");
        libraries.Unload("bits");
        Require(reloaded.IsDisposed && reloaded.UploadedTextures.All(texture => !GodotObject.IsInstanceValid(texture.Native)),
            "The real bits Sp3 generation is also an owned dependency and must retire the panel when it unloads.");
        libraries.Unload("L2A2_G");
        GD.Print("Verified native Scorpion/Jonah health display: whole-PC342-case frame contract, actual first GIF portrait/Boss names and Sp3 pixels, canonical3EC flash and signed health, original asymmetric owner guards, proportional4:3/wide group, reparenting and bank-generation cleanup. Boss actor registration remains separate.");
    }

    private static async Task VerifyViewportAsync(Node owner, PkrArchive archive, PsxLibraryRepository libraries, Vector2I size)
    {
        var viewport = new SubViewport { Name = "OriginalScorpionPanelProof", Size = size, Disable3D = true,
            TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        var resources = GodotScorpionHealthBarResources.Create(archive, libraries);
        var scorpion = new ScorpionState(0) { Health = 900 };
        var jonah = new JonahState(1) { Health = 120 };
        var damage = new ScorpionDamageState(scorpion, new ActorProcedureState(new()), new());
        var bars = new BossHealthBars<ScorpionEncounterBodyState, GodotBossHealthBarTexture>(body => body.Health);
        var lookups = new List<string>();
        // Actual L2A2 restart8 creates node2/Jonah then node1/Scorpion, so the primary creation retains the existing secondary body.
        bars.Create(jonah, JonahState.ActorType, _ => throw new InvalidOperationException("Jonah's default panel branch must not resolve textures."));
        bars.Create(scorpion, ScorpionState.ActorType, name => { lookups.Add(name); return resources.FindTexture(name); });
        var presenter = GodotScorpionHealthBarPresenter.Create(resources, bars,
            body => ReferenceEquals(body, damage.Body) ? damage.HitFlashUpdates : 0);
        var canvas = new ScorpionHealthBarProofCanvas { Presenter = presenter }; viewport.AddChild(canvas); canvas.AddChild(presenter);
        ImageTexture[] uploads = resources.UploadedTextures.Select(texture => texture.Native).ToArray();
        try
        {
            Require(lookups.SequenceEqual(new[] { "scorpion", "boss", "jonah", "scorpion_wounded" }) &&
                ReferenceEquals(bars.Primary, scorpion) && ReferenceEquals(bars.Secondary, jonah) &&
                bars.PrimaryInitialHealth == 900 && bars.SecondaryInitialHealth == 120,
                "Actual shared panel ownership must retain the constructor health baselines and ordered source texture requests.");
            Require(resources.FindTexture("scorpion").Source.Checksum == 0x10F5DCCC && resources.FindTexture("boss").Source.Checksum == 0x488ECF41 &&
                resources.FindTexture("jonah").Source.Checksum == 0x57CB89FF && resources.FindTexture("scorpion_wounded").Source.Checksum == 0xF0477367 &&
                NamedTextureTable.FindFirst(archive.Read("data/l2a2_g.tex"), "Boss HealthBarEnd") == resources.FindTexture("boss").Source.Checksum,
                "The original first GIF entries, including sscanf's Boss basename token, must win over later PNG aliases.");
            foreach (GodotBossHealthBarTexture texture in resources.UploadedTextures)
            {
                Require(resources.EncounterBank.Textures.Any(source => ReferenceEquals(source, texture.Source)) ||
                    resources.BitsBank.Textures.Any(source => ReferenceEquals(source, texture.Source)),
                    "Every panel texture must retain an actual decoded object from its selected loaded source-bank generation.");
                using Image image = texture.Native.GetImage();
                Require(image.GetWidth() == texture.Source.Width && image.GetHeight() == texture.Source.Height && image.GetData().SequenceEqual(texture.Source.RgbaPixels),
                    "The native panel upload must preserve all genuine decoded texture pixels and palette transparency.");
            }
            Reject<InvalidOperationException>(() => GodotScorpionHealthBarPresenter.Create(resources, bars, _ => 0));
            scorpion.Health = 450; jonah.Health = 60; damage.HitFlashUpdates = 30;
            await DrawAsync(owner, canvas);
            Require(canvas.FrameCount > 0 && presenter.LastFrame.Count == 17 &&
                ReferenceEquals(((BossHealthBarSprite<GodotBossHealthBarTexture>)presenter.LastFrame[0]).Texture, resources.FindTexture("scorpion_wounded")) &&
                presenter.LastFrame.OfType<BossHealthBarQuad<GodotBossHealthBarTexture>>().Where(quad => quad.Depth == 3).Select(quad => quad.Rectangle.Width).SequenceEqual(new float[] { 81, 19 }) &&
                scorpion.Health == 450 && jonah.Health == 60 && damage.HitFlashUpdates == 30 && lookups.Count == 4,
                "Draw must consume canonical signed health and actual nonzero3EC, preserve cache identity and leave all AI-owned fields unchanged.");
            await CaptureAsync(owner, viewport, presenter.LastFrame, size, "wounded", 0);

            var destination = new Node { Name = "ReparentedBossPanel" }; viewport.AddChild(destination);
            presenter.Reparent(destination);
            damage.HitFlashUpdates = 0; jonah.BodyFlags |= 0x40; canvas.ScreenOffsetY = 33;
            await DrawAsync(owner, canvas);
            Require(ReferenceEquals(((BossHealthBarSprite<GodotBossHealthBarTexture>)presenter.LastFrame[0]).Texture, resources.FindTexture("scorpion")) &&
                presenter.LastFrame.Count == 17 && ReferenceEquals(bars.Secondary, jonah) && uploads.All(GodotObject.IsInstanceValid) &&
                uploads.SequenceEqual(resources.UploadedTextures.Select(texture => texture.Native)),
                "A live reparent retains resources; actual3EC zero switches to the normal portrait, and a dead Jonah flag must not clear the source secondary display.");
            await CaptureAsync(owner, viewport, presenter.LastFrame, size, "normal", 33);

            scorpion.Health = -2700;
            await DrawAsync(owner, canvas);
            Require(presenter.LastFrame.Count == 16 && presenter.LastFrame.OfType<BossHealthBarQuad<GodotBossHealthBarTexture>>()
                .Any(quad => quad.Depth == 3 && quad.Rectangle.Width == 652),
                "Source signed health may extend the damage width beyond the meter, and the first gradient uses initial-health/2 rather than a normalized UI clamp.");
            GodotBossHealthBarTexture?[] cached = bars.Textures.ToArray();
            scorpion.BodyFlags |= 0x40;
            await DrawAsync(owner, canvas);
            Require(presenter.LastFrame.Count == 0 && bars.Primary is null && bars.Secondary is null && bars.Textures.SequenceEqual(cached) &&
                bars.PrimaryInitialHealth == 900 && bars.SecondaryInitialHealth == 120,
                "The primary40 guard clears both actual owners before rendering while preserving captured metadata and cache identities.");
            using Image cleared = viewport.GetTexture().GetImage();
            Require(cleared.GetData().Where((_, index) => (index & 3) == 3).All(alpha => alpha == 0),
                "The native panel must visibly clear after its real primary-death guard rather than leave a stale canvas draw.");
        }
        finally { viewport.Free(); }
        Require(presenter.ResourcesDisposed && uploads.All(texture => !GodotObject.IsInstanceValid(texture)),
            "Native scene destruction after reparenting must release all five portrait/label/pip uploads exactly once.");
    }

    private static async Task DrawAsync(Node owner, ScorpionHealthBarProofCanvas canvas)
    {
        canvas.QueueRedraw();
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private static async Task CaptureAsync(Node owner, SubViewport viewport,
        IReadOnlyList<BossHealthBarPrimitive<GodotBossHealthBarTexture>> frame, Vector2I size, string phase, int screenY)
    {
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image capture = viewport.GetTexture().GetImage();
        byte[] pixels = capture.GetData(); int visible = 0;
        var transform = new HudViewportTransform(size.X, size.Y);
        ScreenRectangle[] rectangles = frame.Select(primitive => primitive switch
        {
            BossHealthBarSprite<GodotBossHealthBarTexture> sprite => transform.Project(sprite.Quad, HudHorizontalAnchor.Right),
            BossHealthBarQuad<GodotBossHealthBarTexture> quad => transform.Project(quad.Rectangle, HudHorizontalAnchor.Right),
            _ => throw new InvalidDataException("Unexpected original boss panel primitive.")
        }).ToArray();
        float left = rectangles.Min(rectangle => rectangle.X), right = rectangles.Max(rectangle => rectangle.X + rectangle.Width);
        float top = rectangles.Min(rectangle => rectangle.Y), bottom = rectangles.Max(rectangle => rectangle.Y + rectangle.Height);
        Require(Math.Abs(left - (transform.ExtraWidth + 283 * transform.ScaleX)) < .001f &&
            Math.Abs(top - (screenY + 16) * transform.ScaleY) < .001f,
            "The complete source boss group must use one proportional right-edge transform and the actual authored panel Y offset.");
        for (int pixel = 0; pixel < pixels.Length; pixel += 4)
        {
            if (pixels[pixel + 3] == 0) continue;
            int index = pixel / 4, x = index % size.X, y = index / size.X; visible++;
            Require(x >= left - 1 && x <= right + 1 && y >= top - 1 && y <= bottom + 1,
                "Native portrait, label, pips and meters must retain their shared source spacing in4:3 and wide viewports.");
        }
        Require(visible >= 1024, "The genuine native health panel did not render visible portrait, pip and meter pixels.");
        if (capture.SavePng($"/tmp/openspidey-scorpion-health-{size.X}x{size.Y}-{phase}.png") != Error.Ok)
            throw new IOException("Could not save the genuine boss panel comparison.");
    }

    private static void Require(bool condition, string evidence)
    { if (!condition) throw new InvalidDataException($"Native Scorpion panel: {evidence}"); }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidDataException($"Native Scorpion panel expected {typeof(T).Name}.");
    }
}

internal partial class ScorpionHealthBarProofCanvas : Node2D
{
    public GodotScorpionHealthBarPresenter Presenter { get; set; } = null!;
    public int ScreenOffsetY { get; set; }
    public int FrameCount { get; private set; }
    public override void _Draw()
    {
        FrameCount++;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        Presenter.DrawOn(this, new HudViewportTransform(size.X, size.Y), ScreenOffsetY);
    }
}

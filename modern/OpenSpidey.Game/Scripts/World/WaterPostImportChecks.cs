using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.World;

/// <summary>Real untextured alpha tint submission/pixels across actual viewport sizes, source pause gating, retained state and original sky-target ownership.</summary>
internal static class WaterPostImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PsxLibraryRepository repository)
    {
        int checks = 0;
        var viewport = new SubViewport { OwnWorld3D = true, Size = new(640, 480),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            var background = new ColorRect { Color = Colors.Black, Size = viewport.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
            root.AddChild(background);
            uint clock = 0; int pause = 0;
            var water = new PlayerWaterEffectState { Value = 1 };
            var state = new SourceWaterPostState(water) { WordCountdown = 65535 };
            GodotSourceWaterEffect effect = GodotSourceWaterEffect.Create(state, () => clock, () => pause,
                () => (unchecked((uint)viewport.Size.X), unchecked((uint)viewport.Size.Y)), GodotGlowFlash.SourceBrightness());
            root.AddChild(effect); effect.SetProcess(false);
            foreach (Vector2I size in new[] { new Vector2I(640, 480), new(1280, 720), new(1920, 810) })
            {
                viewport.Size = size; background.Size = size;
                effect.SynchronizeSourceRender(); await Frame(); await Frame();
                Assert(effect.Submitted && state.Quad is { Width: 512, Height: 240, Argb: 0x30804032 } && effect.UploadedArgb == 0x30FF8064 &&
                    effect.UploadedRectangle.Position == Vector2.Zero && effect.UploadedRectangle.Size == new Vector2(size.X, size.Y) &&
                    effect.ZIndex == SourceWaterQuad.SourceSlot,
                    "Actual native water must submit original slot6 ARGB/brightness/alpha through the entire4:3,16:9 and wide viewport without a fixed gameplay pillarbox.");
                using Image image = viewport.GetTexture().GetImage();
                foreach (Vector2I point in new[] { new Vector2I(2, 2), size / 2, size - new Vector2I(3, 3) })
                {
                    Color actual = image.GetPixel(point.X, point.Y);
                    // One UNORM8 framebuffer store rounds the source-alpha blend by at most one byte.
                    float alpha = 48 / 255f;
                    Assert(Math.Abs(actual.R * 255 - 255 * alpha) <= 1 && Math.Abs(actual.G * 255 - 128 * alpha) <= 1 &&
                        Math.Abs(actual.B * 255 - 100 * alpha) <= 1 && actual.A == 1,
                        "Native source tint pixels must use direct ARGB red/cos,green/sin,blue50 and alpha48 after original brightness, without3D BGR/bias conversion.");
                }
            }
            Assert(state.WordCountdown == 65532, "Actual post ushort countdown must advance once per submitted source render, independently of two native draw-wait frames.");
            clock = uint.MaxValue; pause = 1;
            effect.SynchronizeSourceRender(); await Frame(); await Frame();
            Assert(!effect.Submitted && state.Quad is null && state.Timer == -48 && state.WordCountdown == 65531,
                "Actual source pause suppresses the quad while retaining wrapped48*clock and still decrementing post wordcountdown.");
            using (Image image = viewport.GetTexture().GetImage())
                Assert(image.GetPixel(viewport.Size.X / 2, viewport.Size.Y / 2) == Colors.Black,
                    "The native pause gate must remove the retained tint submission rather than leaving its previous rendered quad.");
            pause = 0; water.Value = 0; clock = 77;
            effect.SynchronizeSourceRender(); await Frame(); await Frame();
            Assert(!effect.Submitted && state.Timer == -48 && state.WordCountdown == 65530,
                "Zero gWaterEffect must retain the previous source post timer while clearing only draw submission and advancing its independent word.");
            water.Value = 1; clock = 7; effect.SynchronizeSourceRender();
            SourceWaterQuad quad = state.Quad!.Value;
            var parent = new Node3D(); root.AddChild(parent); effect.Reparent(parent);
            Assert(ReferenceEquals(effect.State, state) && ReferenceEquals(state.Water, water) && state.Quad == quad,
                "A live tint reparent must retain the one shared process water/post owner and its source frame.");
            effect.Reparent(root); parent.Free();

            PsxAssetLibrary objects = repository.Load("data/L1A1_O.psx");
            GodotRenderEnvironment environment = GodotRenderEnvironment.Create(root, () => objects, () => objects.Textures, () => null, () => clock);
            root.AddChild(environment); environment.SetProcess(false);
            environment.SetSkyColour(0x466973);
            Assert(environment.SkyColourTarget == 0x466973 && environment.SkyColour == 0x154070,
                "Source202 must retain its raw sky target while actual Db_UpdateSky applies the original magic-colour normalization only to current sky.");
            int restorations = environment.SkyRestorations;
            water.Value = 2; SourceWaterPostEffects.PreparePlayer(water, environment);
            Assert(water.Value == 2 && environment.SkyRestorations == restorations,
                "Native pre-player water restoration must preserve non1 values and avoid an extra source Db_UpdateSky visit.");
            water.Value = 1; SourceWaterPostEffects.PreparePlayer(water, environment);
            Assert(water.Value == 0 && environment.SkyRestorations == restorations + 1 && environment.SkyColourTarget == 0x466973 && environment.SkyColour == 0x154070,
                "Actual pre-player water1 must restore the real raw sky target through Db_UpdateSky normalization before clearing the shared process flag.");
            GD.Print($"PASS: {checks} actual water tint/sky ownership checks: slot6 directARGB brightness/alpha pixels, complete narrow/wide viewport, pause/clock/countdown, live reparent and source202 target/sky restoration.");
        }
        finally { viewport.Free(); }
        void Assert(bool condition, string evidence) { checks++; if (!condition) throw new InvalidDataException(evidence); }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}

using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Game.World;
using OpenSpidey.Game.Utilities;

namespace OpenSpidey.Game.Import;

/// <summary>Runs actual asset imports before gameplay systems are connected.</summary>
public partial class AssetWorkbench : Node3D
{
    private Camera3D camera = null!;
    private ImportedAnimatedModel? character;
    private Label label = null!;
    private string importSummary = "";
    private Node3D? worldCollision;
    private string? restartName;
    private Func<ImportedAnimatedModel>? surfacePlayerFactory;
    private PlayerEffectTextures? playerEffects;
    private EnemyPatrolImportChecks? enemyPatrolChecks;

    public override void _Ready()
    {
        if (!System.Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("OpenSpidey requires a 64-bit runtime.");
        string? directory = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS");
        string asset = System.Environment.GetEnvironmentVariable("OPENSPIDEY_SCENE") ?? "data/L1A1_G.psx";
        label = new Label { Position = new(20, 20) };
        var overlay = new CanvasLayer();
        overlay.AddChild(label);
        AddChild(overlay);
        if (string.IsNullOrWhiteSpace(directory))
        {
            label.Text = "Select your original game folder with OPENSPIDEY_ASSETS.\nAsset import workbench — gameplay port in progress.";
            if (OS.GetCmdlineUserArgs().Contains("--verify-collision")) AddChild(new CollisionImportChecks());
            else if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) GetTree().Quit();
            return;
        }
        try
        {
            using var archive = new PkrArchive(System.IO.Path.Combine(directory, "data.pkr"));
            byte[] bytes = archive.Read(asset);
            PsxScene scene = PsxSceneReader.Read(bytes);
            var textures = new List<DecodedTexture>();
            if (asset.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            {
                string library = asset[..^6] + "_L.psx";
                if (archive.Contains(library)) textures.AddRange(PsxTextureReader.Read(archive.Read(library)));
            }
            textures.AddRange(PsxTextureReader.Read(bytes));
            IAnimationPoseSource? animations = PsxCompressedAnimationReader.Read(bytes);
            animations ??= PsxMatrixAnimationReader.Read(bytes);
            Node3D geometry;
            if (animations is not null)
            {
                character = ImportedAnimatedModel.Build(scene, textures, animations,
                    asset.EndsWith("/spidey.psx", StringComparison.OrdinalIgnoreCase));
                string selected = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ANIMATION") ?? "0";
                character.SelectClip(int.Parse(selected, System.Globalization.CultureInfo.InvariantCulture));
                geometry = character;
            }
            else if (asset.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            {
                ImportedLevelWorld level = ImportedLevelWorld.Load(archive, asset);
                geometry = level;
                worldCollision = level.Collision;
                GD.Print($"Imported level: {level.Sources.Count} source libraries, {level.InstanceCount} instances; " +
                    $"{SceneNodes.Descendants<CollisionObject3D>(worldCollision).Count()} solid/trigger surface groups.");
            }
            else geometry = PsxGeometryBuilder.Build(scene, textures);
            AddChild(geometry);
            camera = new Camera3D { Current = true, Near = 0.1f, Far = 2000,
                KeepAspect = Camera3D.KeepAspectEnum.Height, Fov = 60 };
            AddChild(camera);
            FrameScene(geometry);
            restartName = System.Environment.GetEnvironmentVariable("OPENSPIDEY_RESTART");
            if (!string.IsNullOrWhiteSpace(restartName))
            {
                character = RestartPreview.Build(archive, asset, restartName);
                AddChild(character);
                character.SelectClip(int.Parse(System.Environment.GetEnvironmentVariable("OPENSPIDEY_ANIMATION") ?? "0",
                    System.Globalization.CultureInfo.InvariantCulture));
                FrameScene(character);
            }
            importSummary = $"{asset}: {scene.Models.Count} models, {scene.Instances.Count} instances\n64-bit Godot asset workbench — WASD / mouse to inspect, Esc to release mouse\n{textures.Count} imported textures; gameplay and mission execution are still being ported.";
            UpdateLabel();
            GD.Print($"Imported {asset}: {scene.Models.Count} primary models, {SceneNodes.Descendants<MeshInstance3D>(geometry).Count()} visible meshes, {textures.Count} primary textures; process={IntPtr.Size * 8}-bit");
            if (OS.GetCmdlineUserArgs().Contains("--smoke-test"))
            {
                character?.VerifyAllClips();
                character?.VerifyPlayback();
            }
            if (OS.GetCmdlineUserArgs().Contains("--verify-all-animations")) AnimationImportChecks.Run(archive, this);
            if (OS.GetCmdlineUserArgs().Contains("--verify-enemy-models"))
            {
                EnemyModelImportChecks.Run(archive, this);
                LineParticleImportChecks.Run(this);
            }
            if (OS.GetCmdlineUserArgs().Contains("--verify-enemy-patrol"))
            {
                if (!asset.Equals("data/L1A1_G.psx", StringComparison.OrdinalIgnoreCase) || worldCollision is null)
                    throw new InvalidOperationException("The original enemy patrol check requires data/L1A1_G.psx.");
                enemyPatrolChecks = EnemyPatrolImportChecks.Create(
                    OpenSpidey.Assets.Scripting.TriggerFile.Read(archive.Read("data/L1A1_T.trg")),
                    archive.Read("data/henchman.psx"), worldCollision, scene);
                AddChild(enemyPatrolChecks);
            }
            if (OS.GetCmdlineUserArgs().Contains("--verify-collision"))
            {
                byte[] playerBytes = archive.Read("data/spidey.psx");
                PsxScene playerScene = PsxSceneReader.Read(playerBytes);
                var playerTextures = PsxTextureReader.Read(playerBytes);
                var playerAnimations = PsxCompressedAnimationReader.Read(playerBytes)
                    ?? throw new InvalidDataException("The original player animation library is missing.");
                surfacePlayerFactory = () => ImportedAnimatedModel.Build(playerScene, playerTextures, playerAnimations, playerPartOrder: true);
                playerEffects = PlayerEffectTextures.Load(archive);
            }
        }
        catch (Exception exception)
        {
            label.Text = "Asset import failed: " + exception.Message;
            GD.PushError(exception.ToString());
            if (OS.GetCmdlineUserArgs().Contains("--smoke-test")) GetTree().Quit(1);
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--verify-collision")) AddChild(new CollisionImportChecks
            { LevelGeometry = worldCollision, PlayerModelFactory = surfacePlayerFactory, PlayerEffects = playerEffects });
        else if (enemyPatrolChecks is null && OS.GetCmdlineUserArgs().Contains("--smoke-test")) GetTree().Quit();
    }

    public override void _ExitTree() => playerEffects?.Dispose();

    public override void _PhysicsProcess(double delta)
    {
        if (enemyPatrolChecks?.Finished == true && OS.GetCmdlineUserArgs().Contains("--smoke-test")) GetTree().Quit();
    }

    private void FrameScene(Node3D geometry)
    {
        Aabb? bounds = geometry is ImportedAnimatedModel imported ? geometry.GlobalTransform * imported.ClipBounds : null;
        if (geometry is not ImportedAnimatedModel)
        foreach (MeshInstance3D child in SceneNodes.Descendants<MeshInstance3D>(geometry))
        {
            Aabb box = child.GlobalTransform * child.GetAabb();
            bounds = bounds is { } existing ? existing.Merge(box) : box;
        }
        if (bounds is not { } extent) return;
        Vector3 center = extent.GetCenter();
        float padding = geometry is ImportedAnimatedModel ? 0.95f : 0.65f;
        float radius = Mathf.Max(geometry is ImportedAnimatedModel ? 1 : 5, extent.Size.Length() * padding);
        camera.Position = center + new Vector3(radius * 0.5f, radius * 0.4f, radius);
        camera.LookAt(center);
    }

    private void UpdateLabel()
    {
        label.Text = importSummary;
        if (character is not null)
            label.Text += $"\n{character.ClipCount} clips — Left/Right select, Space pause. Clip {character.ClipId} ({character.FrameCount} frames).";
        if (!string.IsNullOrWhiteSpace(restartName))
            label.Text += $"\nPlayer placed at {restartName}; movement controls are still being ported.";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (camera is null) return;
        if (character is not null && @event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Space) character.Playing = !character.Playing;
            if (key.Keycode is Key.Left or Key.Right)
            {
                int count = character.ClipCount;
                int next = (character.ClipId + (key.Keycode == Key.Right ? 1 : count - 1)) % count;
                character.SelectClip(next);
                UpdateLabel();
                FrameScene(character);
            }
        }
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            Input.MouseMode = Input.MouseModeEnum.Captured;
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
            Input.MouseMode = Input.MouseModeEnum.Visible;
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            camera.RotateY(-motion.Relative.X * 0.003f);
            Vector3 rotation = camera.Rotation;
            rotation.X = Mathf.Clamp(rotation.X - motion.Relative.Y * 0.003f, -1.5f, 1.5f);
            camera.Rotation = rotation;
        }
    }

    public override void _Process(double delta)
    {
        if (camera is null || Input.MouseMode != Input.MouseModeEnum.Captured) return;
        Vector3 direction = new((Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.E) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Q) ? 1 : 0),
            (Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0));
        float speed = character is null ? 20 : 2;
        if (Input.IsPhysicalKeyPressed(Key.Shift)) speed *= 5;
        camera.Position += camera.Basis * direction.Normalized() * speed * (float)delta;
    }
}

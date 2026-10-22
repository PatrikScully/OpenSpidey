using Godot;

namespace OpenSpidey.Game.Frontend;

/// <summary>
/// The user's original game folder: OPENSPIDEY_ASSETS, else --assets=PATH (remembered), else the remembered folder.
/// The resolved folder is published as OPENSPIDEY_ASSETS so the level scenes read the same files.
/// </summary>
public static class GameFolder
{
    private const string SettingsPath = "user://openspidey.cfg";

    public static string? Resolve(IReadOnlyList<string> arguments)
    {
        string? folder = System.Environment.GetEnvironmentVariable("OPENSPIDEY_ASSETS");
        bool remember = false;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = arguments.FirstOrDefault(argument => argument.StartsWith("--assets=", StringComparison.Ordinal))?["--assets=".Length..];
            remember = !string.IsNullOrWhiteSpace(folder);
        }
        var settings = new ConfigFile();
        bool loaded = settings.Load(SettingsPath) == Error.Ok;
        if (string.IsNullOrWhiteSpace(folder) && loaded) folder = (string)settings.GetValue("game", "folder", "");
        if (string.IsNullOrWhiteSpace(folder)) return null;
        folder = System.IO.Path.GetFullPath(folder);
        if (!System.IO.File.Exists(System.IO.Path.Combine(folder, "SpideyPC.exe")) || !System.IO.File.Exists(System.IO.Path.Combine(folder, "data.pkr")))
            throw new System.IO.DirectoryNotFoundException($"{folder} does not contain the original SpideyPC.exe and data.pkr.");
        if (remember)
        {
            settings.SetValue("game", "folder", folder);
            Error saved = settings.Save(SettingsPath);
            if (saved != Error.Ok) GD.PushWarning($"Could not remember the game folder: {saved}.");
        }
        System.Environment.SetEnvironmentVariable("OPENSPIDEY_ASSETS", folder);
        return folder;
    }
}

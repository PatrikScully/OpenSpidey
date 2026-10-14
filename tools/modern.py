#!/usr/bin/env python3
"""Build, test and run the 64-bit Godot runtime without shell-specific scripts."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MODERN = ROOT / "modern"
ENGINE_VERSION = "4.7.2"
SDK_VERSION = json.loads((MODERN / "global.json").read_text())["sdk"]["version"]
CACHE = Path(os.environ.get("OPENSPIDEY_TOOLCHAINS", Path.home() / ".local/share/openspidey/toolchains"))


def executable(name, override, fallback):
    location = os.environ.get(override) or shutil.which(name)
    if location:
        return Path(location)
    if fallback.is_file():
        return fallback
    raise SystemExit(f"{name} is missing. Run python tools/modern.py bootstrap or set {override}.")


def engine_path():
    override = os.environ.get("OPENSPIDEY_GODOT") or shutil.which("godot") or shutil.which("godot4")
    if override:
        return Path(override)
    for path in sorted(CACHE.glob(f"Godot_v{ENGINE_VERSION}-stable_mono_*/**/*")):
        if path.is_file() and (
            path.name.endswith("_mono_linux.x86_64") or path.name.endswith("_mono_linux.arm64")
            or path.name.endswith("_mono_win64.exe") or path.name.endswith("_mono_windows_arm64.exe")
            or path.name == "Godot" and path.parent.name == "MacOS"
        ):
            return path
    raise SystemExit("Godot .NET is missing. Run python tools/modern.py bootstrap.")


def download_verified(url, checksum, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists() or hashlib.file_digest(destination.open("rb"), "sha512").hexdigest() != checksum.lower():
        print(f"Downloading {destination.name}", flush=True)
        temporary = destination.with_suffix(destination.suffix + ".partial")
        urllib.request.urlretrieve(url, temporary)
        with temporary.open("rb") as stream:
            actual = hashlib.file_digest(stream, "sha512").hexdigest()
        if actual != checksum.lower():
            temporary.unlink()
            raise SystemExit(f"Checksum mismatch: {destination.name}")
        temporary.replace(destination)


def bootstrap(godot_only):
    system = platform.system()
    arm = platform.machine().lower() in ("arm64", "aarch64")
    if platform.machine().lower() not in ("amd64", "x86_64", "arm64", "aarch64"):
        raise SystemExit("A 64-bit x64 or ARM64 host is required.")
    suffix = {
        "Linux": "linux_arm64" if arm else "linux_x86_64",
        "Windows": "windows_arm64" if arm else "win64",
        "Darwin": "macos.universal",
    }[system]
    name = f"Godot_v{ENGINE_VERSION}-stable_mono_{suffix}.zip"
    base = f"https://github.com/godotengine/godot-builds/releases/download/{ENGINE_VERSION}-stable/"
    checksums = urllib.request.urlopen(base + "SHA512-SUMS.txt").read().decode()
    checksum = next(line.split()[0] for line in checksums.splitlines() if line.split()[-1].lstrip("*") == name)
    archive = CACHE / name
    download_verified(base + name, checksum, archive)
    destination = CACHE / archive.stem
    with zipfile.ZipFile(archive) as bundle:
        bundle.extractall(destination)
    # Preserve the archive's Unix permissions (zipfile does not set executable bits).
    with zipfile.ZipFile(archive) as bundle:
        if system != "Windows":
            for member in bundle.infolist():
                mode = member.external_attr >> 16
                if mode & 0o111 and not member.is_dir():
                    (destination / member.filename).chmod(mode & 0o777)
    print(f"Verified Godot {ENGINE_VERSION}: {engine_path()}", flush=True)
    if godot_only:
        return
    rid = {"Linux": "linux", "Windows": "win", "Darwin": "osx"}[system] + ("-arm64" if arm else "-x64")
    metadata = json.load(urllib.request.urlopen("https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"))
    sdk = next(sdk for release in metadata["releases"] for sdk in release.get("sdks", []) if sdk["version"] == SDK_VERSION)
    artifact = next(file for file in sdk["files"] if file["rid"] == rid and file["url"].endswith((".zip", ".tar.gz")))
    archive = CACHE / Path(artifact["url"]).name
    download_verified(artifact["url"], artifact["hash"], archive)
    destination = CACHE / "dotnet"
    destination.mkdir(exist_ok=True)
    if zipfile.is_zipfile(archive):
        with zipfile.ZipFile(archive) as bundle:
            bundle.extractall(destination)
    else:
        with tarfile.open(archive) as bundle:
            bundle.extractall(destination, filter="data")
    print(f"Verified .NET SDK {SDK_VERSION}: {destination}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("bootstrap", "build", "test", "run", "play", "play-smoke", "story-smoke", "story-transition-smoke", "checkpoint-smoke", "smoke", "editor", "media", "voices", "cinema", "cinema-smoke"))
    parser.add_argument("--assets", type=Path, help="Original game folder (never copied into the repository)")
    parser.add_argument("--scene", default="data/L1A1_G.psx")
    parser.add_argument("--animation", type=int, default=0, help="Original clip ID for an animated model")
    parser.add_argument("--restart", help="Inspect Spider-Man at a named restart in the selected level")
    parser.add_argument("--capture", type=Path, help="Save the rendered player smoke check to a PNG")
    parser.add_argument("--capture-dir", type=Path, help="Save original overhang ceiling/floor frames during rendered physics checks")
    parser.add_argument("--rendered", action="store_true", help="Run smoke checks with the real renderer instead of headless mode")
    parser.add_argument("--all-animations", action="store_true", help="Smoke-check every animated model in the original archive")
    parser.add_argument("--enemy-models", action="store_true", help="Check original thug meshes, welds and every enemy animation frame")
    parser.add_argument("--enemy-patrol", action="store_true", help="Verify original first-level enemy floor placement, waypoint travel and animation in Godot")
    parser.add_argument("--physics-checks", action="store_true", help="Verify imported collision layers and a falling capsule in Godot")
    parser.add_argument("--godot-only", action="store_true")
    parser.add_argument("--media-cache", type=Path, default=Path.home() / ".local/share/openspidey/media")
    parser.add_argument("--cinema", type=int, default=11, help="Original Cinema_Run ID (11 opens the first level)")
    parser.add_argument("--finish-cinema", action="store_true", help="Verify natural movie completion instead of a permitted skip")
    args = parser.parse_args()
    if args.capture and (args.command not in ("play-smoke", "cinema-smoke") or not args.rendered):
        parser.error("--capture requires play-smoke or cinema-smoke with --rendered")
    if args.capture_dir and (args.command != "smoke" or not args.rendered or not args.physics_checks or not args.assets):
        parser.error("--capture-dir requires smoke --rendered --physics-checks --assets")
    if args.rendered and args.command not in ("smoke", "play-smoke", "story-smoke", "story-transition-smoke", "checkpoint-smoke", "cinema-smoke"):
        parser.error("--rendered applies to smoke checks")
    if args.command in ("story-smoke", "story-transition-smoke"):
        if not args.assets:
            parser.error("story-smoke requires --assets with your original game folder")
        if not re.fullmatch(r"data/l[1-8]a[1-7]_g\.psx", args.scene.lower()) or args.restart:
            parser.error("story-smoke verifies original empty-save startup of a campaign level (data/lXaY_g.psx)")
        if args.command == "story-transition-smoke" and args.scene.lower() not in ("data/l1a1_g.psx", "data/l1a2_g.psx", "data/l1a4_g.psx"):
            parser.error("story-transition-smoke verifies the authored transition from L1A1, L1A2 or L1A4")
    if (args.all_animations or args.enemy_models or args.enemy_patrol or args.physics_checks) and args.command != "smoke":
        parser.error("Animation, enemy and physics checks apply to asset smoke checks")
    if (args.enemy_models or args.enemy_patrol) and not args.assets:
        parser.error("Enemy checks require --assets with your original game folder")
    if args.enemy_patrol and args.physics_checks:
        parser.error("Run enemy patrol and player physics checks separately")
    if args.finish_cinema and args.command != "cinema-smoke":
        parser.error("--finish-cinema requires cinema-smoke")
    if args.command in ("media", "voices", "cinema", "cinema-smoke") and not args.assets:
        parser.error("Media preparation requires --assets with your original game folder")
    if args.command == "bootstrap":
        bootstrap(args.godot_only)
        return
    dotnet = executable("dotnet", "OPENSPIDEY_DOTNET", CACHE / "dotnet" / ("dotnet.exe" if os.name == "nt" else "dotnet"))
    environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_ROOT=str(dotnet.resolve().parent))
    environment["PATH"] = str(dotnet.parent) + os.pathsep + environment.get("PATH", "")
    if args.assets:
        environment["OPENSPIDEY_ASSETS"] = str(args.assets.resolve())
    if args.capture:
        args.capture.resolve().parent.mkdir(parents=True, exist_ok=True)
        environment["OPENSPIDEY_CAPTURE"] = str(args.capture.resolve())
    else:
        environment.pop("OPENSPIDEY_CAPTURE", None)
    if args.capture_dir:
        args.capture_dir.resolve().mkdir(parents=True, exist_ok=True)
        environment["OPENSPIDEY_VERIFY_CAPTURES"] = str(args.capture_dir.resolve())
    else:
        environment.pop("OPENSPIDEY_VERIFY_CAPTURES", None)
    environment["OPENSPIDEY_SCENE"] = args.scene
    environment["OPENSPIDEY_ANIMATION"] = str(args.animation)
    if args.restart:
        environment["OPENSPIDEY_RESTART"] = args.restart
    else:
        environment.pop("OPENSPIDEY_RESTART", None)

    def run(command):
        subprocess.run([str(value) for value in command], cwd=MODERN, env=environment, check=True)

    if args.command in ("voices", "play", "play-smoke", "story-smoke", "story-transition-smoke", "checkpoint-smoke") and args.assets:
        ffmpeg = executable("ffmpeg", "OPENSPIDEY_FFMPEG", Path("ffmpeg"))
        run([dotnet, "run", "--project", "OpenSpidey.MediaImporter", "--", args.assets.resolve(),
             args.media_cache.resolve(), "voices", ffmpeg])
        if args.command == "voices":
            return
        environment["OPENSPIDEY_MEDIA"] = str(args.media_cache.resolve())
        if args.command in ("play", "story-smoke", "story-transition-smoke"):
            scene_name = Path(args.scene).stem
            mission = (scene_name[:-2] if scene_name.lower().endswith("_g") else scene_name) + "_T"
            run([dotnet, "run", "--project", "OpenSpidey.MediaImporter", "--", args.assets.resolve(),
                 args.media_cache.resolve(), "level:" + mission, ffmpeg])
    if args.command in ("media", "cinema", "cinema-smoke"):
        ffmpeg = executable("ffmpeg", "OPENSPIDEY_FFMPEG", Path("ffmpeg"))
        run([dotnet, "run", "--project", "OpenSpidey.MediaImporter", "--", args.assets.resolve(),
             args.media_cache.resolve(), "all" if args.command == "media" else args.cinema, ffmpeg])
        if args.command == "media":
            return
        environment["OPENSPIDEY_MEDIA"] = str(args.media_cache.resolve())
        environment["OPENSPIDEY_CINEMA"] = str(args.cinema)
    if args.command == "test":
        command = [dotnet, "run", "--project", "OpenSpidey.Tests", "--"]
        if args.assets:
            command.append(args.assets.resolve())
        run(command)
        return
    run([dotnet, "build", "OpenSpidey.Game/OpenSpidey.Game.csproj", "--nologo"])
    if args.command == "build":
        run([dotnet, "build", "OpenSpidey.MediaImporter/OpenSpidey.MediaImporter.csproj", "--nologo"])
        return
    command = [engine_path(), "--path", MODERN / "OpenSpidey.Game"]
    if args.command in ("cinema", "cinema-smoke"):
        command.append("res://Scenes/CinemaPreview.tscn")
        if args.command == "cinema-smoke":
            if not args.rendered:
                command.append("--headless")
            command.extend(["--", "--smoke-cinema"])
            if args.finish_cinema:
                command.append("--finish-cinema")
    elif args.command in ("play", "play-smoke", "story-smoke", "story-transition-smoke", "checkpoint-smoke"):
        command.append("res://Scenes/LevelSession.tscn")
        if args.command in ("play-smoke", "story-smoke", "story-transition-smoke", "checkpoint-smoke"):
            command.extend(["--audio-driver", "Dummy"])
            if not args.rendered:
                command.append("--headless")
            check = "--smoke-player"
            if args.command == "checkpoint-smoke":
                check = "--smoke-checkpoint"
            elif args.command == "story-smoke":
                check = {
                    "data/l1a1_g.psx": "--smoke-mission-startup",
                    "data/l1a2_g.psx": "--smoke-mission-two-startup",
                    "data/l1a3_g.psx": "--smoke-mission-three-startup",
                    "data/l1a4_g.psx": "--smoke-mission-four-startup",
                    "data/l2a1_g.psx": "--smoke-mission-five-startup",
                    "data/l2a2_g.psx": "--smoke-mission-six-startup",
                }.get(args.scene.lower(), "--smoke-mission-generic-startup")
            elif args.command == "story-transition-smoke":
                check = {"data/l1a1_g.psx": "--smoke-story-transition", "data/l1a2_g.psx": "--smoke-story-two-transition",
                         "data/l1a4_g.psx": "--smoke-story-four-transition"}[args.scene.lower()]
            command.extend(["--", check])
    elif args.command == "smoke":
        if not args.rendered:
            command.append("--headless")
        command.extend(["--", "--smoke-test"])
        if args.all_animations:
            command.append("--verify-all-animations")
        if args.enemy_models:
            command.append("--verify-enemy-models")
        if args.enemy_patrol:
            command.append("--verify-enemy-patrol")
        if args.physics_checks:
            command.append("--verify-collision")
    elif args.command == "editor":
        command.append("--editor")
    if args.rendered and args.command.endswith("smoke") and offscreen(command, environment):
        return
    run(command)


def offscreen(command, environment):
    """Rendered smoke checks run on an invisible display unless OPENSPIDEY_SHOW_WINDOW=1."""
    if os.name == "nt" or environment.get("OPENSPIDEY_SHOW_WINDOW") == "1":
        return False
    if shutil.which("gamescope"):
        display = ["gamescope", "--backend", "headless", "-W", "1280", "-H", "720", "--"]
    elif shutil.which("xvfb-run"):
        display = ["xvfb-run", "-a", "-s", "-screen 0 1280x720x24"]
    else:
        return False
    environment = dict(environment)
    environment.pop("WAYLAND_DISPLAY", None)
    godot = [str(command[0]), "--display-driver", "x11", *map(str, command[1:])]
    # gamescope always exits 0, so the child records its own status.
    with tempfile.TemporaryDirectory(prefix="openspidey-offscreen-") as folder:
        status = Path(folder) / "status"
        script = 'status=$1; shift; "$@"; echo $? > "$status"'
        subprocess.run([*display, "sh", "-c", script, "sh", status, *godot], cwd=MODERN, env=environment)
        code = int(status.read_text()) if status.exists() else 1
    if code:
        raise subprocess.CalledProcessError(code, godot)
    return True


if __name__ == "__main__":
    main()

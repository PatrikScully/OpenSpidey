"""Saved preferences for the native OpenSpidey launcher."""

import json
import os
from pathlib import Path
import sys
import tempfile

try:
    from _version import VERSION
except ImportError:
    VERSION = "0.0.3"
DEFAULTS = {
    "schema": 1, "source_mode": "iso", "source_path": "", "game_dir": "",
    "window_mode": "windowed", "resolution": "1280x960", "msaa": 4,
    "anisotropy": 8, "mipmaps": True, "vsync": True,
    "master_volume": 100, "music_volume": 100, "sfx_volume": 100,
    "modern_controls": True, "mouse_sensitivity": 3.0, "invert_mouse_y": False,
    "skip_movies": False, "show_setup": False,
}


def user_paths(environ=None, windows=None):
    env = os.environ if environ is None else environ
    windows = os.name == "nt" if windows is None else windows
    home = Path(env.get("HOME", str(Path.home())))
    if windows:
        config = Path(env.get("APPDATA", str(home / "AppData/Roaming"))) / "OpenSpidey"
        data = Path(env.get("LOCALAPPDATA", str(home / "AppData/Local"))) / "OpenSpidey"
    else:
        config_base = Path(env.get("XDG_CONFIG_HOME", str(home / ".config")))
        data_base = Path(env.get("XDG_DATA_HOME", str(home / ".local/share")))
        config = (config_base if config_base.is_absolute() else home / ".config") / "openspidey"
        data = (data_base if data_base.is_absolute() else home / ".local/share") / "openspidey"
    return config / "settings.json", data


def normalise_settings(values):
    if not isinstance(values, dict) or values.get("schema", 1) != 1:
        raise ValueError("These settings use an unsupported format. Please set up the game again.")
    result = dict(DEFAULTS)
    for key in ("source_path", "game_dir"):
        if isinstance(values.get(key), str):
            result[key] = values[key]
    for key, options in (("source_mode", ("iso", "directory")),
                         ("window_mode", ("windowed", "borderless", "fullscreen")),
                         ("msaa", (0, 2, 4, 8)), ("anisotropy", (1, 2, 4, 8, 16))):
        if values.get(key) in options and (key not in ("msaa", "anisotropy") or type(values.get(key)) is int):
            result[key] = values[key]
    for key in ("mipmaps", "vsync", "modern_controls", "invert_mouse_y", "skip_movies", "show_setup"):
        if type(values.get(key)) is bool:
            result[key] = values[key]
    for key in ("master_volume", "music_volume", "sfx_volume"):
        if type(values.get(key)) is int:
            result[key] = max(0, min(100, values[key]))
    sensitivity = values.get("mouse_sensitivity")
    if type(sensitivity) in (int, float) and 0.1 <= sensitivity <= 20:
        result["mouse_sensitivity"] = float(sensitivity)
    resolution = values.get("resolution", "")
    try:
        width, height = map(int, resolution.split("x"))
        if 640 <= width <= 7680 and 480 <= height <= 4320:
            result["resolution"] = "%dx%d" % (width, height)
    except (AttributeError, TypeError, ValueError):
        pass
    return result


def load_settings(path):
    try:
        return normalise_settings(json.loads(Path(path).read_text(encoding="utf-8"))), ""
    except FileNotFoundError:
        return dict(DEFAULTS), ""
    except (OSError, ValueError) as error:
        return dict(DEFAULTS), "Saved settings could not be read: %s" % error


def save_settings(path, values):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent,
                                         prefix=".settings-", delete=False) as stream:
            temporary = Path(stream.name)
            json.dump(normalise_settings(values), stream, indent=2, ensure_ascii=False)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


def game_environment(values, binary, environ=None):
    values = normalise_settings(values)
    env = dict(os.environ if environ is None else environ)
    for key in ("SPIDEY_GAME_DIR", "SPIDEY_EXE", "SPIDEY_FULLSCREEN", "SPIDEY_SCALE"):
        env.pop(key, None)
    width, height = values["resolution"].split("x")
    mapped = {"SPIDEY_WINDOW_MODE": values["window_mode"], "SPIDEY_WIDTH": width,
              "SPIDEY_HEIGHT": height, "SPIDEY_MSAA": values["msaa"],
              "SPIDEY_ANISOTROPY": values["anisotropy"], "SPIDEY_MIPMAPS": int(values["mipmaps"]),
              "SPIDEY_VSYNC": int(values["vsync"]), "SPIDEY_MASTER_VOLUME": values["master_volume"],
              "SPIDEY_MUSIC_VOLUME": values["music_volume"], "SPIDEY_SFX_VOLUME": values["sfx_volume"],
              "SPIDEY_MODERN_CONTROLS": int(values["modern_controls"]),
              "SPIDEY_MOUSE_SENSITIVITY": values["mouse_sensitivity"],
              "SPIDEY_INVERT_MOUSE_Y": int(values["invert_mouse_y"])}
    env.update({key: str(value) for key, value in mapped.items()})
    if values["skip_movies"]:
        env["SPIDEY_SKIP_MOVIES"] = "1"
    else:
        env.pop("SPIDEY_SKIP_MOVIES", None)
    directory = str(Path(binary).resolve().parent)
    env["PATH"] = directory + os.pathsep + env.get("PATH", "")
    if os.name != "nt":
        original = env.get("LD_LIBRARY_PATH_ORIG", "") if getattr(sys, "frozen", False) else env.get("LD_LIBRARY_PATH", "")
        paths = [p for p in original.split(os.pathsep) if p]
        env["LD_LIBRARY_PATH"] = os.pathsep.join([directory] + paths)
    return env

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

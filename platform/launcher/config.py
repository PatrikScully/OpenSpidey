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

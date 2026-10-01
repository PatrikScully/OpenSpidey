"""First-run setup and launch for OpenSpidey. Game assets are supplied by the user."""

import argparse
from datetime import datetime
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading

from config import DEFAULTS, VERSION, game_environment, load_settings, normalise_settings, save_settings, user_paths
from assets import AssetError, ImportCancelled, import_iso, validate_game_dir

MODES = {"Windowed": "windowed", "Borderless fullscreen": "borderless", "Fullscreen": "fullscreen"}
RESOLUTIONS = ("640x480", "800x600", "1024x768", "1280x720", "1280x960", "1600x900",
               "1600x1200", "1920x1080", "1920x1440", "2560x1440", "2560x1920", "3840x2160")


def find_binary(explicit=None):
    if explicit:
        binary = Path(explicit).expanduser().resolve()
        if not binary.is_file():
            raise FileNotFoundError("The game executable is missing: %s" % binary)
        return binary
    root = Path(sys.executable).parent if getattr(sys, "frozen", False) else Path(__file__).resolve().parents[1]
    for directory in (root, root.parent):
        for name in ("spider.exe", "spider"):
            candidate = directory / name
            if candidate.is_file():
                return candidate
    raise FileNotFoundError("The game executable is missing. Extract the complete OpenSpidey download into one folder.")

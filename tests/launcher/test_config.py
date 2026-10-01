"""Preferences, first-run routing and process integration checks."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

LAUNCHER = Path(__file__).resolve().parents[2] / "platform/launcher"
sys.path.insert(0, str(LAUNCHER))
import config
import launcher


class LauncherConfigTests(unittest.TestCase):
    def test_preferences_recover_and_round_trip(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "nested/settings.json"
            values, warning = config.load_settings(path)
            self.assertEqual(values, config.DEFAULTS)
            self.assertFalse(warning)
            values.update(window_mode="borderless", resolution="1920x1080", source_path="/disc/my game.iso", show_setup=True)
            config.save_settings(path, values)
            self.assertEqual(config.load_settings(path), (values, ""))
            path.write_text("broken json")
            restored, warning = config.load_settings(path)
            self.assertEqual(restored, config.DEFAULTS)
            self.assertTrue(warning)
            with mock.patch("config.os.replace", side_effect=OSError("write denied")):
                with self.assertRaises(OSError):
                    config.save_settings(path, values)
            self.assertEqual(path.read_text(), "broken json")
            self.assertEqual(list(path.parent.iterdir()), [path])

    def test_invalid_values_cannot_reach_the_game(self):
        values = config.normalise_settings({"window_mode": "invalid", "resolution": "-1x9999999", "msaa": True,
                                           "mouse_sensitivity": float("nan"), "master_volume": -2, "music_volume": 130,
                                           "show_setup": "false", "anisotropy": 64})
        self.assertEqual(values["window_mode"], "windowed")
        self.assertEqual(values["resolution"], "1280x960")
        self.assertEqual(values["msaa"], 4)
        self.assertEqual(values["mouse_sensitivity"], 3)
        self.assertEqual((values["master_volume"], values["music_volume"]), (0, 100))
        self.assertIs(values["show_setup"], False)
        with self.assertRaises(ValueError):
            config.normalise_settings({"schema": 2})

    def test_platform_paths_and_child_environment(self):
        settings, data = config.user_paths({"HOME": "/users/test", "XDG_CONFIG_HOME": "relative"}, windows=False)
        self.assertEqual(settings, Path("/users/test/.config/openspidey/settings.json"))
        settings, data = config.user_paths({"HOME": "/users/test", "APPDATA": "/roaming", "LOCALAPPDATA": "/local"}, windows=True)
        self.assertEqual(settings, Path("/roaming/OpenSpidey/settings.json"))
        self.assertEqual(data, Path("/local/OpenSpidey"))
        values = dict(config.DEFAULTS, window_mode="fullscreen", resolution="1920x1080", master_volume=0,
                      modern_controls=False, invert_mouse_y=True, skip_movies=True)
        with mock.patch.object(config.sys, "frozen", True, create=True):
            env = config.game_environment(values, "/app/game/spider", {"PATH": "/bin", "SPIDEY_EXE": "/wrong.exe",
                                          "SPIDEY_FULLSCREEN": "1", "LD_LIBRARY_PATH": "/tmp/_MEI123:/app/_internal", "LD_LIBRARY_PATH_ORIG": "/driver"})
        self.assertEqual(env["SPIDEY_WIDTH"], "1920")
        self.assertEqual(env["SPIDEY_MODERN_CONTROLS"], "0")
        self.assertEqual(env["SPIDEY_MASTER_VOLUME"], "0")
        self.assertEqual(env["SPIDEY_INVERT_MOUSE_Y"], "1")
        if os.name != "nt":
            self.assertEqual(env["LD_LIBRARY_PATH"], "/app/game:/driver")
        self.assertNotIn("SPIDEY_EXE", env)
        self.assertNotIn("SPIDEY_FULLSCREEN", env)

    def test_saved_setup_autostarts_without_gui(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "settings.json"
            values = dict(config.DEFAULTS, game_dir="/my/game")
            config.save_settings(path, values)
            with mock.patch("launcher.user_paths", return_value=(path, Path(temp))), \
                 mock.patch("launcher.find_binary", return_value=Path("/app/spider")), \
                 mock.patch("launcher.validate_game_dir", return_value=Path("/my/game")), \
                 mock.patch("launcher.run_game", return_value=0) as run:
                self.assertEqual(launcher.main([]), 0)
                run.assert_called_once_with(Path("/app/spider"), values, Path(temp))

    @unittest.skipIf(os.name == "nt", "POSIX executable fixture")
    def test_process_uses_argument_list_and_persistent_logs(self):
        with tempfile.TemporaryDirectory(prefix="launcher path ") as temp:
            root = Path(temp)
            binary = root / "spider"
            binary.write_text("#!/usr/bin/env python3\nimport json,os,sys\nprint(json.dumps({'argv':sys.argv,'cwd':os.getcwd(),'volume':os.getenv('SPIDEY_MASTER_VOLUME')}))\n")
            binary.chmod(0o700)
            values = dict(config.DEFAULTS, game_dir=str(root), master_volume=37)
            with mock.patch("launcher.validate_game_dir", return_value=root):
                self.assertEqual(launcher.run_game(binary, values, root), 0)
            log = next((root / "logs").iterdir())
            result = json.loads(log.read_text())
            self.assertEqual(result["argv"], [str(binary), "."])
            self.assertEqual(result["cwd"], str(root))
            self.assertEqual(result["volume"], "37")
            binary.write_text("#!/bin/sh\nexit 7\n")
            with mock.patch("launcher.validate_game_dir", return_value=root), self.assertRaisesRegex(RuntimeError, "code 7"):
                launcher.run_game(binary, values, root)


if __name__ == "__main__":
    unittest.main()

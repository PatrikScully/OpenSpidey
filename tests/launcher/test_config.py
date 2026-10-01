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

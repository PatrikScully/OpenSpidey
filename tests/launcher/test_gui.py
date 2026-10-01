"""Exercise real Tk widgets and the import worker under a display server."""

import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "platform/launcher"))
import config
import launcher


@unittest.skipUnless(os.environ.get("DISPLAY") or os.name == "nt", "A display server is required")
class LauncherGuiTests(unittest.TestCase):
    def setUp(self):
        import tkinter as tk
        self.temp = tempfile.TemporaryDirectory()
        self.root = tk.Tk()
        self.path = Path(self.temp.name)
        self.window = launcher.SetupWindow(self.root, dict(config.DEFAULTS), self.path / "settings.json", self.path)
        self.root.update()

    def tearDown(self):
        try:
            self.root.destroy()
        except self.window.tk.TclError:
            pass
        self.temp.cleanup()

    def test_pages_and_controls_save_values(self):
        for name in self.window.pages:
            self.window.show_page(name)
            self.root.update()
            self.assertTrue(self.window.pages[name].winfo_ismapped())
        self.window.mode_label.set("Borderless fullscreen")
        self.window.vars["resolution"].set("1920x1080")
        self.window.vars["master_volume"].set(43)
        self.window.vars["mouse_sensitivity"].set(5)
        self.window.vars["invert_mouse_y"].set(True)
        values = self.window.read_values()
        self.assertEqual(values["window_mode"], "borderless")
        self.assertEqual(values["master_volume"], 43)
        self.assertEqual(values["mouse_sensitivity"], 5)
        self.assertTrue(values["invert_mouse_y"])
        self.assertLess(self.window.start_button.winfo_rooty() + self.window.start_button.winfo_height(), self.root.winfo_rooty() + self.root.winfo_height())

    def test_missing_source_does_not_save_or_start(self):
        self.window.start()
        self.assertFalse(self.window.busy)
        self.assertIsNone(self.window.result)
        self.assertFalse((self.path / "settings.json").exists())

    def test_worker_reports_progress_and_saves_after_validation(self):
        self.window.vars["source_path"].set(str(self.path / "disc.iso"))
        def importer(source, destination, progress, cancelled):
            progress(50, 100, "data.pkr")
            return Path(destination)
        with mock.patch("launcher.import_iso", side_effect=importer):
            self.window.start()
            deadline = time.monotonic() + 3
            while self.window.result is None and time.monotonic() < deadline:
                self.root.update()
                time.sleep(0.01)
        self.assertIsNotNone(self.window.result)
        self.assertEqual(config.load_settings(self.path / "settings.json")[0]["game_dir"], str(self.path / "game"))

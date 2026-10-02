"""Check layout, keyboard access and display mode behavior with real Tk widgets."""

import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "platform/launcher"))
import config
import launcher


@unittest.skipUnless(os.environ.get("DISPLAY") or os.name == "nt", "A display server is required")
class LauncherLayoutTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name)
        self.roots = []

    def tearDown(self):
        for root in self.roots:
            for timer in root.tk.call("after", "info"):
                root.after_cancel(timer)
            root.destroy()
        self.temp.cleanup()

    def window(self, scaling=96 / 72, reported_dpi=None):
        import tkinter as tk
        root = tk.Tk()
        root.tk.call("tk", "scaling", scaling)
        self.roots.append(root)
        settings = dict(config.DEFAULTS, resolution="1920x1080")
        dpi = root.winfo_fpixels("1i") if reported_dpi is None else reported_dpi
        with mock.patch.object(root, "winfo_fpixels", return_value=dpi):
            window = launcher.SetupWindow(root, settings, self.path / "settings.json", self.path)
        root.update()
        return root, window

    def test_hidpi_uses_font_metrics_when_reported_dpi_is_wrong(self):
        root, window = self.window(scaling=192 / 72, reported_dpi=96)
        self.assertGreaterEqual(window.scale, 1.8)
        self.assertGreaterEqual(window.sidebar_width, window.fonts["brand"].measure("SPIDEY") + window.pixels(48))
        self.assertLessEqual(root.winfo_width(), root.winfo_screenwidth())
        self.assertLessEqual(root.winfo_height(), root.winfo_screenheight())
        for name in window.pages:
            window.show_page(name)
            root.update()
            self.assertLessEqual(window.pages[name].winfo_reqwidth(), window.canvas.winfo_width())
            self.assertLess(window.start_button.winfo_rooty() + window.start_button.winfo_height(), root.winfo_rooty() + root.winfo_height())



if __name__ == "__main__":
    unittest.main()

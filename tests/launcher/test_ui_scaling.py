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
        self.assertGreater(window.scale, 1)
        self.assertGreaterEqual(window.pixels(20), window.fonts["body"].metrics("linespace"))
        self.assertGreaterEqual(window.sidebar_width, window.fonts["brand"].measure("SPIDEY") + window.pixels(48))
        self.assertLessEqual(root.winfo_width(), root.winfo_screenwidth())
        self.assertLessEqual(root.winfo_height(), root.winfo_screenheight())
        for name in window.pages:
            window.show_page(name)
            root.update()
            self.assertLessEqual(window.pages[name].winfo_reqwidth(), window.canvas.winfo_width())
            self.assertLess(window.start_button.winfo_rooty() + window.start_button.winfo_height(), root.winfo_rooty() + root.winfo_height())

    def test_borderless_preserves_the_chosen_resolution(self):
        root, window = self.window()
        window.mode_label.set("Borderless fullscreen")
        root.update()
        self.assertEqual(str(window.resolution_choice["state"]), "disabled")
        self.assertIn("desktop resolution automatically", window.resolution_hint["text"])
        self.assertEqual(window.read_values()["resolution"], "1920x1080")
        for mode in ("Fullscreen", "Windowed"):
            window.mode_label.set(mode)
            self.assertEqual(str(window.resolution_choice["state"]), "readonly")
            self.assertEqual(window.vars["resolution"].get(), "1920x1080")

    def test_small_window_scrolls_and_keeps_actions_visible(self):
        root, window = self.window()
        root.geometry("640x480")
        root.update()
        self.assertTrue(window.compact)
        self.assertTrue(window.topbar.winfo_ismapped())
        self.assertFalse(window.sidebar.winfo_ismapped())
        for name in window.pages:
            window.show_page(name)
            root.update()
            self.assertLessEqual(window.pages[name].winfo_reqwidth(), window.canvas.winfo_width())
            self.assertGreater(window.canvas.winfo_height(), 100)
            for button in (window.cancel_button, window.start_button):
                self.assertGreaterEqual(button.winfo_rootx(), root.winfo_rootx())
                self.assertLessEqual(button.winfo_rootx() + button.winfo_width(), root.winfo_rootx() + root.winfo_width())
                self.assertLess(button.winfo_rooty() + button.winfo_height(), root.winfo_rooty() + root.winfo_height())
        self.assertLess(window.canvas.yview()[1], 1)
        window.canvas.yview_moveto(1)
        window.show_page("Game files")
        root.update()
        self.assertEqual(window.canvas.yview()[0], 0)

    def test_keyboard_focus_reveals_an_offscreen_setting(self):
        root, window = self.window()
        root.geometry("640x480")
        window.show_page("Controls")
        root.update()
        skip = next(widget for widget in window.pages["Controls"].winfo_children()
                    if widget.winfo_class() == "TCheckbutton" and str(widget["variable"]) == str(window.vars["skip_movies"]))
        self.assertGreater(skip.winfo_rooty(), window.canvas.winfo_rooty() + window.canvas.winfo_height())
        skip.focus_force()
        root.update()
        self.assertGreater(window.canvas.yview()[0], 0)
        self.assertGreaterEqual(skip.winfo_rooty(), window.canvas.winfo_rooty())
        self.assertLessEqual(skip.winfo_rooty() + skip.winfo_height(), window.canvas.winfo_rooty() + window.canvas.winfo_height())

    def test_narrow_forms_stack_and_reflow_when_the_window_grows(self):
        root, window = self.window()
        root.minsize(480, 420)
        root.geometry("500x480")
        root.update()
        for frame, label, widget in window.form_rows:
            self.assertEqual(widget.grid_info()["column"], 0)
            self.assertEqual(widget.grid_info()["row"], 1)
        root.geometry("1000x760")
        root.update()
        self.assertFalse(window.compact)
        for frame, label, widget in window.form_rows:
            self.assertEqual(widget.grid_info()["column"], 1)
            self.assertEqual(widget.grid_info()["row"], 0)


if __name__ == "__main__":
    unittest.main()

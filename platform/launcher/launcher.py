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


PALETTE = {"paper": "#f7f3eb", "field": "#fffdf8", "ink": "#152132", "muted": "#657080",
           "red": "#da3342", "red_active": "#bc2534", "line": "#dedbd3",
           "sidebar_text": "#b6bfcc", "sidebar_active": "#253449", "web": "#344055"}

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


def run_game(binary, settings, data_path):
    directory = validate_game_dir(settings["game_dir"])
    logs = Path(data_path) / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    log = logs / ("game-%s.log" % datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    env = game_environment(settings, binary)
    frozen_windows = getattr(sys, "frozen", False) and os.name == "nt"
    if frozen_windows:
        import ctypes
    with log.open("w", encoding="utf-8") as output:
        flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
        if frozen_windows:
            ctypes.windll.kernel32.SetDllDirectoryW(None)
        try:
            game = subprocess.Popen([str(binary), "."], cwd=directory, env=env,
                                    stdout=output, stderr=subprocess.STDOUT, creationflags=flags)
        finally:
            if frozen_windows:
                ctypes.windll.kernel32.SetDllDirectoryW(getattr(sys, "_MEIPASS", None))
        result = game.wait()
    if result:
        raise RuntimeError("The game stopped unexpectedly (code %s).\n\nThe game log is saved here:\n%s" % (result, log))
    return result


class SetupWindow:
    def __init__(self, root, settings, config_path, data_path, message=""):
        import tkinter as tk
        from tkinter import ttk
        self.tk, self.ttk = tk, ttk
        self.root, self.config_path, self.data_path = root, config_path, data_path
        self.settings = dict(settings)
        self.result = None
        self.events = queue.Queue()
        self.cancelled = threading.Event()
        self.busy = False
        self.closing = False
        self.form_rows = []
        self.configure_style()
        p = self.pixels
        root.title("OpenSpidey • Setup")
        screen_width, screen_height = root.winfo_screenwidth(), root.winfo_screenheight()
        width = min(p(1000), max(320, screen_width - p(32)))
        height = min(p(760), max(320, screen_height - p(56)))
        root.minsize(min(p(580), width), min(p(420), height))
        root.geometry("%dx%d+%d+%d" % (width, height, max(0, (screen_width - width) // 2), max(0, (screen_height - height) // 2)))
        root.configure(background=PALETTE["paper"])
        root.protocol("WM_DELETE_WINDOW", self.close)
        self.vars = {}
        for key, value in settings.items():
            if key == "schema":
                continue
            if type(value) is bool:
                var_type = tk.BooleanVar
            elif key == "mouse_sensitivity":
                var_type = tk.DoubleVar
            elif type(value) is int:
                var_type = tk.IntVar
            else:
                var_type = tk.StringVar
            self.vars[key] = var_type(root, value=value)
        self.mode_label = tk.StringVar(root, next(label for label, value in MODES.items() if value == settings["window_mode"]))
        self.source_label = tk.StringVar(root, "ISO disc image" if settings["source_mode"] == "iso" else "Installed game folder")
        self.status = tk.StringVar(root, message or "Choose your game files, then press Start game.")
        self.shell = ttk.Frame(root)
        self.shell.pack(fill="both", expand=True)
        self.sidebar_width = max(p(220), self.fonts["brand"].measure("SPIDEY") + p(48))
        self.sidebar = tk.Frame(self.shell, background=PALETTE["ink"], width=self.sidebar_width)
        self.sidebar.pack(side="left", fill="y")
        self.sidebar.pack_propagate(False)
        tk.Frame(self.sidebar, background=PALETTE["red"], height=p(6)).pack(fill="x")
        tk.Label(self.sidebar, text="OPEN", background=PALETTE["ink"], foreground=PALETTE["paper"], font=self.fonts["brand"], anchor="w").pack(anchor="w", padx=p(24), pady=(p(34), 0))
        tk.Label(self.sidebar, text="SPIDEY", background=PALETTE["ink"], foreground=PALETTE["red"], font=self.fonts["brand"], anchor="w").pack(anchor="w", padx=p(24))
        tk.Label(self.sidebar, text="SPIDER-MAN / 2000\nNATIVE PC EDITION", background=PALETTE["ink"], foreground=PALETTE["sidebar_text"], font=self.fonts["small"], justify="left", anchor="w").pack(anchor="w", padx=p(24), pady=(p(12), p(36)))
        self.pages, self.navigation, self.compact_navigation = {}, {}, {}
        self.topbar = tk.Frame(self.shell, background=PALETTE["ink"])
        tk.Label(self.topbar, text="OPENSPIDEY", background=PALETTE["ink"], foreground=PALETTE["paper"], font=self.fonts["nav"], anchor="w").pack(side="left", padx=p(16), pady=p(12))
        top_navigation = tk.Frame(self.topbar, background=PALETTE["ink"])
        top_navigation.pack(side="right", fill="x", expand=True, padx=(0, p(8)), pady=p(8))
        self.content = ttk.Frame(self.shell, padding=p(32))
        self.content.pack(side="right", fill="both", expand=True)
        self.content.rowconfigure(0, weight=1)
        self.content.columnconfigure(0, weight=1)
        scroll_area = ttk.Frame(self.content)
        scroll_area.grid(row=0, column=0, sticky="nsew")
        self.canvas = tk.Canvas(scroll_area, background=PALETTE["paper"], highlightthickness=0, yscrollincrement=p(22), width=1, height=1)
        scrollbar = ttk.Scrollbar(scroll_area, orient="vertical", command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side="right", fill="y", padx=(p(10), 0))
        self.canvas.pack(side="left", fill="both", expand=True)
        self.page_container = ttk.Frame(self.canvas)
        self.page_container.columnconfigure(0, weight=1)
        page_item = self.canvas.create_window((0, 0), window=self.page_container, anchor="nw")
        self.canvas.bind("<Configure>", lambda event: self.resize_pages(event.width, page_item))
        self.page_container.bind("<Configure>", lambda event: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        for number, title in enumerate(("Game files", "Video", "Sound", "Controls"), 1):
            page = ttk.Frame(self.page_container)
            page.columnconfigure(0, weight=1)
            self.pages[title] = page
            button = tk.Button(self.sidebar, text="%02d   %s" % (number, title), anchor="w", relief="flat", borderwidth=0,
                               highlightthickness=p(2), highlightbackground=PALETTE["ink"], highlightcolor=PALETTE["paper"],
                               background=PALETTE["ink"], foreground=PALETTE["sidebar_text"], activebackground=PALETTE["sidebar_active"], activeforeground="white",
                               font=self.fonts["nav"], padx=p(24), pady=p(14), cursor="hand2", command=lambda name=title: self.show_page(name))
            button.pack(fill="x")
            self.navigation[title] = button
            button = tk.Button(top_navigation, text=title, relief="flat", borderwidth=0,
                               highlightthickness=p(1), highlightbackground=PALETTE["ink"], highlightcolor=PALETTE["paper"],
                               background=PALETTE["ink"], foreground=PALETTE["sidebar_text"], activebackground=PALETTE["sidebar_active"], activeforeground="white",
                               font=self.fonts["small"], padx=p(10), pady=p(8), cursor="hand2", command=lambda name=title: self.show_page(name))
            button.pack(side="left", fill="x", expand=True)
            self.compact_navigation[title] = button
        tk.Label(self.sidebar, text="OPENSPIDEY  /  " + VERSION, background=PALETTE["ink"], foreground=PALETTE["sidebar_text"], font=self.fonts["small"], anchor="w", wraplength=self.sidebar_width - p(48)).pack(side="bottom", anchor="w", padx=p(24), pady=p(24))
        self.draw_web()
        self.build_files()
        self.build_video()
        self.build_sound()
        self.build_controls()
        self.footer = ttk.Frame(self.content)
        self.footer.grid(row=1, column=0, sticky="ew", pady=(p(20), 0))
        self.footer.columnconfigure(0, weight=1)
        progress_track = ttk.Frame(self.footer, height=p(3))
        progress_track.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, p(12)))
        progress_track.pack_propagate(False)
        self.progress = ttk.Progressbar(progress_track, maximum=100)
        self.progress.pack(fill="both", expand=True)
        self.status_label = tk.Label(self.footer, textvariable=self.status, background=PALETTE["paper"], foreground=PALETTE["muted"], font=self.fonts["small"], height=2, anchor="nw", justify="left")
        self.status_label.grid(row=1, column=0, columnspan=2, sticky="ew", pady=(0, p(14)))
        self.setup_check = ttk.Checkbutton(self.footer, text="Show setup every time", variable=self.vars["show_setup"])
        self.setup_check.grid(row=2, column=0, sticky="w")
        self.actions = ttk.Frame(self.footer)
        self.actions.grid(row=2, column=1, sticky="e")
        self.cancel_button = ttk.Button(self.actions, text="Close", command=self.close)
        self.cancel_button.pack(side="left", padx=(0, p(10)))
        self.start_button = ttk.Button(self.actions, text="Start game", style="Start.TButton", command=self.start)
        self.start_button.pack(side="left")
        self.current_page = "Game files"
        self.show_page(self.current_page)
        self.compact = None
        self.resize_layout(width)
        root.bind("<Configure>", lambda event: self.resize_layout(event.width) if event.widget == root else None)
        root.bind("<Escape>", lambda event: self.close())
        root.bind("<MouseWheel>", self.scroll_pages)
        root.bind("<Button-4>", self.scroll_pages)
        root.bind("<Button-5>", self.scroll_pages)
        root.bind("<FocusIn>", self.reveal_focus, add="+")
        root.bind("<Destroy>", self.stop_poll, add="+")
        self.poll_timer = root.after(75, self.poll)

    def heading(self, page, title, description):
        self.ttk.Label(page, text=title, style="Title.TLabel").grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, 9))
        self.ttk.Label(page, text=description, style="Hint.TLabel", wraplength=540).grid(row=1, column=0, columnspan=2, sticky="w", pady=(0, 25))

    def choice(self, page, row, text, variable, options):
        self.ttk.Label(page, text=text).grid(row=row, column=0, sticky="w", padx=(0, 18), pady=10)
        widget = self.ttk.Combobox(page, textvariable=variable, values=options, state="readonly", width=18)
        widget.grid(row=row, column=1, sticky="ew", pady=10)
        return widget

    def build_files(self):
        page = self.pages["Game files"]
        self.heading(page, "Welcome back, web-head.", "Choose your Spider-Man (2000) PC disc image or an installed game folder. Setup remembers your choice.")
        self.choice(page, 2, "Game source", self.source_label, ("ISO disc image", "Installed game folder"))
        self.ttk.Label(page, text="Location").grid(row=3, column=0, sticky="w", pady=10)
        self.ttk.Entry(page, textvariable=self.vars["source_path"]).grid(row=3, column=1, sticky="ew", pady=10)
        self.ttk.Button(page, text="Browse…", command=self.browse).grid(row=4, column=1, sticky="e", pady=(0, 20))
        self.ttk.Label(page, text="ISO files are imported once. Keep the disc image anywhere you like; no mounting or Windows installer is needed.", style="Hint.TLabel", wraplength=530).grid(row=5, column=0, columnspan=2, sticky="w", pady=12)
        self.ttk.Label(page, text="Settings, imported files and game logs are stored in your user folder.", style="Hint.TLabel", wraplength=530).grid(row=6, column=0, columnspan=2, sticky="w", pady=12)

    def build_video(self):
        page = self.pages["Video"]
        self.heading(page, "Make the city look its best.", "Choose how the game appears on your screen. The original picture proportions are preserved.")
        self.choice(page, 2, "Display mode", self.mode_label, tuple(MODES))
        self.choice(page, 3, "Resolution", self.vars["resolution"], RESOLUTIONS)
        self.choice(page, 4, "Antialiasing", self.vars["msaa"], (0, 2, 4, 8))
        self.choice(page, 5, "Texture filtering", self.vars["anisotropy"], (1, 2, 4, 8, 16))
        self.ttk.Label(page, text="Antialiasing: 0 = off, 2 / 4 / 8 = samples.\nTexture filtering: 1 = standard, higher values sharpen distant textures.", style="Hint.TLabel", wraplength=530).grid(row=6, column=0, columnspan=2, sticky="w", pady=10)
        self.ttk.Checkbutton(page, text="Smooth distant textures", variable=self.vars["mipmaps"]).grid(row=7, column=0, columnspan=2, sticky="w", pady=7)
        self.ttk.Checkbutton(page, text="VSync (reduce screen tearing)", variable=self.vars["vsync"]).grid(row=8, column=0, columnspan=2, sticky="w", pady=7)

    def build_sound(self):
        page = self.pages["Sound"]
        self.heading(page, "Every quip. Every swing.", "Set the overall volume and balance the original music, voices and sound effects.")
        for row, (key, label) in enumerate((("master_volume", "Master volume"), ("music_volume", "Music and voices"), ("sfx_volume", "Sound effects")), 2):
            self.ttk.Label(page, text=label).grid(row=row, column=0, sticky="w", padx=(0, 18), pady=16)
            control = self.ttk.Frame(page)
            control.grid(row=row, column=1, sticky="ew", pady=16)
            self.ttk.Scale(control, from_=0, to=100, command=lambda value, variable=self.vars[key]: variable.set(round(float(value))), variable=self.vars[key]).pack(side="left", fill="x", expand=True)
            self.ttk.Label(control, textvariable=self.vars[key], width=4, anchor="e").pack(side="right")
        self.ttk.Label(page, text="Set Master volume to 0 to mute all audio, including cinematics.", style="Hint.TLabel", wraplength=530).grid(row=5, column=0, columnspan=2, sticky="w", pady=20)

    def build_controls(self):
        page = self.pages["Controls"]
        self.heading(page, "Ready to swing.", "Use modern mouse controls or return to the original keyboard layout.")
        self.ttk.Checkbutton(page, text="Modern controls (WASD and mouse look)", variable=self.vars["modern_controls"]).grid(row=2, column=0, columnspan=2, sticky="w", pady=10)
        self.choice(page, 3, "Mouse sensitivity", self.vars["mouse_sensitivity"], (0.5, 1, 2, 3, 4, 5, 7, 10, 15, 20))
        self.ttk.Checkbutton(page, text="Invert vertical mouse look", variable=self.vars["invert_mouse_y"]).grid(row=4, column=0, columnspan=2, sticky="w", pady=10)
        self.ttk.Checkbutton(page, text="Skip opening movies", variable=self.vars["skip_movies"]).grid(row=5, column=0, columnspan=2, sticky="w", pady=10)
        self.ttk.Label(page, text="WASD / arrows     Move\nSpace                   Jump\nLeft mouse            Punch\nRight mouse          Shoot webs\nEnter                    Select\nEsc                       Pause / skip eligible scenes\nF1                         Release or capture the mouse\nF12                       Quit", style="Hint.TLabel", justify="left").grid(row=6, column=0, columnspan=2, sticky="w", pady=(15, 0))

    def show_page(self, title):
        for page in self.pages.values():
            page.grid_remove()
        self.pages[title].grid(row=0, column=0, sticky="nsew")
        self.canvas.yview_moveto(0)
        for name, button in self.navigation.items():
            button.configure(background="#29394e" if name == title else "#172334", foreground="white" if name == title else "#bac6d6")

    def resize_pages(self, width, item):
        self.canvas.itemconfigure(item, width=width)
        for page in self.pages.values():
            for widget in page.winfo_children():
                if widget.winfo_class() == "TLabel" and int(widget.grid_info().get("columnspan", 1)) == 2:
                    widget.configure(wraplength=max(200, width - 8))

    def browse(self):
        from tkinter import filedialog
        if self.busy:
            return
        if self.source_label.get() == "ISO disc image":
            selected = filedialog.askopenfilename(parent=self.root, title="Choose your Spider-Man PC disc image", filetypes=(("ISO disc images", "*.iso *.ISO"), ("All files", "*")))
        else:
            selected = filedialog.askdirectory(parent=self.root, title="Choose the installed Spider-Man PC folder")
        if selected:
            self.vars["source_path"].set(selected)
            self.status.set("Ready to check your game files.")

    def read_values(self):
        values = {key: variable.get() for key, variable in self.vars.items()}
        values["window_mode"] = MODES[self.mode_label.get()]
        values["source_mode"] = "iso" if self.source_label.get() == "ISO disc image" else "directory"
        return normalise_settings(values)

    def start(self):
        if self.busy:
            return
        values = self.read_values()
        if not values["source_path"].strip():
            self.show_page("Game files")
            self.status.set("Choose an ISO disc image or installed game folder first.")
            return
        self.busy = True
        self.cancelled.clear()
        self.start_button.configure(state="disabled")
        self.cancel_button.configure(text="Cancel")
        self.status.set("Checking game files…")
        threading.Thread(target=self.prepare_assets, args=(values,), daemon=False).start()

    def prepare_assets(self, values):
        try:
            if values["source_mode"] == "iso":
                destination = self.data_path / "game"
                same_source = values["source_mode"] == self.settings["source_mode"] and values["source_path"] == self.settings["source_path"]
                if same_source and self.settings["game_dir"] and not Path(values["source_path"]).expanduser().exists():
                    directory = validate_game_dir(self.settings["game_dir"])
                else:
                    directory = import_iso(values["source_path"], destination,
                                           progress=lambda done, total, name: self.events.put(("progress", (done, total, name))),
                                           cancelled=self.cancelled.is_set)
            else:
                directory = validate_game_dir(Path(values["source_path"]).expanduser())
            if self.cancelled.is_set():
                raise ImportCancelled("Import cancelled.")
            values["game_dir"] = str(directory.resolve())
            save_settings(self.config_path, values)
            self.events.put(("ready", values))
        except Exception as error:
            self.events.put(("error", error))

    def poll(self):
        from tkinter import messagebox
        try:
            while True:
                kind, value = self.events.get_nowait()
                if kind == "progress":
                    done, total, name = value
                    self.progress["value"] = done / max(1, total) * 100
                    self.status.set("Importing %s · %d%%" % (name, done / max(1, total) * 100))
                elif kind == "ready":
                    self.busy = False
                    if not self.closing:
                        self.result = value
                    self.root.destroy()
                    return
                else:
                    self.busy = False
                    self.progress["value"] = 0
                    self.start_button.configure(state="normal")
                    self.cancel_button.configure(text="Close")
                    self.status.set(str(value))
                    if self.closing:
                        self.root.destroy()
                        return
                    if not isinstance(value, ImportCancelled):
                        messagebox.showerror("Game files could not be prepared", str(value), parent=self.root)
        except queue.Empty:
            pass
        self.root.after(75, self.poll)

    def close(self):
        if self.busy:
            self.closing = True
            self.cancelled.set()
            self.cancel_button.configure(state="disabled")
            self.status.set("Cancelling import…")
        else:
            self.root.destroy()


def main(argv=None):
    parser = argparse.ArgumentParser(description="OpenSpidey setup and game launcher")
    settings_shortcut = "settings" in Path(sys.executable if getattr(sys, "frozen", False) else sys.argv[0]).stem.lower()
    parser.add_argument("--settings", action="store_true", default=settings_shortcut, help="Open setup even when preferences are saved")
    parser.add_argument("--game-binary", help="Path to the native game executable")
    args = parser.parse_args(argv)
    config_path, data_path = user_paths()
    settings, warning = load_settings(config_path)
    try:
        binary = find_binary(args.game_binary)
        show_setup = args.settings or settings["show_setup"] or bool(warning) or not config_path.is_file()
        if not show_setup:
            try:
                validate_game_dir(settings["game_dir"])
            except (AssetError, OSError, ValueError) as error:
                warning = "Your game files need attention: %s" % error
                show_setup = True
        if show_setup:
            import tkinter as tk
            root = tk.Tk()
            window = SetupWindow(root, settings, config_path, data_path, warning)
            root.mainloop()
            if window.result is None:
                return 0
            settings = window.result
        return run_game(binary, settings, data_path)
    except Exception as error:
        if sys.stderr is not None:
            print("OpenSpidey: %s" % error, file=sys.stderr)
        try:
            import tkinter as tk
            from tkinter import messagebox
            root = tk.Tk()
            root.withdraw()
            messagebox.showerror("OpenSpidey could not start", str(error), parent=root)
            root.destroy()
        except Exception:
            pass
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

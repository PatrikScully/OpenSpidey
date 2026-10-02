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

    def configure_style(self):
        from tkinter import font
        available = set(font.families(self.root))
        body = next((name for name in ("Aptos", "Noto Sans", "Calibri", "DejaVu Sans") if name in available), "TkDefaultFont")
        display = next((name for name in ("Bahnschrift", "Franklin Gothic Demi Cond", "Liberation Sans Narrow", "DejaVu Sans") if name in available), body)
        self.fonts = {name: font.Font(self.root, family=family, size=size, weight=weight) for name, family, size, weight in (
            ("body", body, 11, "normal"), ("small", body, 9, "normal"), ("nav", body, 11, "bold"),
            ("title", display, 28, "bold"), ("brand", display, 28, "bold"), ("eyebrow", body, 9, "bold"))}
        # Font metrics also catch desktop font scaling that Tk's DPI does not report.
        self.scale = max(1.0, self.root.winfo_fpixels("1i") / 96.0, self.fonts["body"].metrics("linespace") / 20.0)
        p = self.pixels
        style = self.ttk.Style(self.root)
        style.theme_use("clam")
        style.configure(".", font=self.fonts["body"], background=PALETTE["paper"], foreground=PALETTE["ink"])
        style.configure("TFrame", background=PALETTE["paper"])
        style.configure("TLabel", background=PALETTE["paper"], foreground=PALETTE["ink"], font=self.fonts["body"])
        style.configure("Title.TLabel", font=self.fonts["title"])
        style.configure("Hint.TLabel", foreground=PALETTE["muted"], font=self.fonts["small"])
        style.configure("Eyebrow.TLabel", foreground=PALETTE["red"], font=self.fonts["eyebrow"])
        style.configure("TButton", padding=(p(18), p(11)), font=self.fonts["nav"], background=PALETTE["ink"], foreground="white", borderwidth=0, relief="flat", focuscolor=PALETTE["red"])
        style.map("TButton", background=[("disabled", PALETTE["line"]), ("active", PALETTE["sidebar_active"])], foreground=[("disabled", PALETTE["muted"])])
        style.configure("Start.TButton", background=PALETTE["red"], foreground="white", padding=(p(24), p(11)))
        style.map("Start.TButton", background=[("disabled", PALETTE["line"]), ("active", PALETTE["red_active"])])
        style.configure("TEntry", padding=(p(10), p(9)), fieldbackground=PALETTE["field"], bordercolor=PALETTE["line"], lightcolor=PALETTE["line"], darkcolor=PALETTE["line"], insertcolor=PALETTE["ink"])
        style.map("TEntry", bordercolor=[("focus", PALETTE["red"])], lightcolor=[("focus", PALETTE["red"])], darkcolor=[("focus", PALETTE["red"])])
        style.configure("TCombobox", padding=(p(10), p(9)), arrowsize=p(14), fieldbackground=PALETTE["field"], background=PALETTE["field"], bordercolor=PALETTE["line"], lightcolor=PALETTE["line"], darkcolor=PALETTE["line"], arrowcolor=PALETTE["ink"])
        style.map("TCombobox", fieldbackground=[("disabled", PALETTE["paper"]), ("readonly", PALETTE["field"])], foreground=[("disabled", PALETTE["muted"])], background=[("active", PALETTE["line"])], bordercolor=[("focus", PALETTE["red"])])
        style.configure("TCheckbutton", font=self.fonts["body"], background=PALETTE["paper"], indicatorsize=p(16), indicatormargin=(0, 0, p(9), 0), indicatorbackground=PALETTE["field"], indicatorforeground="white", upperbordercolor=PALETTE["line"], lowerbordercolor=PALETTE["line"], focuscolor=PALETTE["red"])
        style.map("TCheckbutton", background=[("active", PALETTE["paper"])], indicatorbackground=[("selected", PALETTE["red"]), ("active", PALETTE["line"])])
        style.configure("Horizontal.TScale", background=PALETTE["red"], troughcolor=PALETTE["line"], bordercolor=PALETTE["red"], lightcolor=PALETTE["red"], darkcolor=PALETTE["red"], sliderlength=p(20), sliderthickness=p(18), borderwidth=0)
        style.configure("Horizontal.TProgressbar", background=PALETTE["red"], troughcolor=PALETTE["line"], lightcolor=PALETTE["red"], darkcolor=PALETTE["red"], borderwidth=0, thickness=p(3))
        style.configure("Vertical.TScrollbar", background=PALETTE["line"], troughcolor=PALETTE["paper"], bordercolor=PALETTE["paper"], lightcolor=PALETTE["line"], darkcolor=PALETTE["line"], arrowcolor=PALETTE["muted"], arrowsize=p(10), borderwidth=0)
        self.root.option_add("*TCombobox*Listbox.font", self.fonts["body"])
        self.root.option_add("*TCombobox*Listbox.background", PALETTE["field"])
        self.root.option_add("*TCombobox*Listbox.foreground", PALETTE["ink"])
        self.root.option_add("*TCombobox*Listbox.selectBackground", PALETTE["red"])

    def pixels(self, value):
        return max(1, round(value * self.scale))

    def draw_web(self):
        p = self.pixels
        web = self.tk.Canvas(self.sidebar, width=self.sidebar_width, height=p(160), background=PALETTE["ink"], highlightthickness=0)
        web.pack(side="bottom", fill="x")
        origin = (self.sidebar_width + p(12), p(172))
        for x, y in ((0, p(145)), (0, p(62)), (p(70), 0), (p(155), 0), (self.sidebar_width, 0)):
            web.create_line(*origin, x, y, fill=PALETTE["web"], width=p(1))
        for radius in (46, 84, 122, 160, 198):
            web.create_arc(origin[0] - p(radius), origin[1] - p(radius), origin[0] + p(radius), origin[1] + p(radius), start=90, extent=95, style="arc", outline=PALETTE["web"], width=p(1))

    def resize_layout(self, width):
        p = self.pixels
        compact = width < self.sidebar_width + p(600)
        if compact != self.compact:
            self.compact = compact
            if compact:
                self.sidebar.pack_forget()
                self.topbar.pack(side="top", fill="x", before=self.content)
            else:
                self.topbar.pack_forget()
                self.sidebar.pack(side="left", fill="y", before=self.content)
            self.content.configure(padding=p(16) if compact else p(32))
        content_width = width - (0 if compact else self.sidebar_width) - p(32 if compact else 64)
        check_width = self.fonts["body"].measure("Show setup every time") + p(32)
        action_width = self.fonts["nav"].measure("CloseStart game") + p(94)
        stacked = content_width < check_width + action_width + p(16)
        self.actions.grid_configure(row=3 if stacked else 2, column=0 if stacked else 1, sticky="e", pady=(p(12), 0) if stacked else 0)
        self.setup_check.grid_configure(columnspan=2 if stacked else 1)
        self.status_label.configure(wraplength=max(p(100), content_width))

    def form_row(self, page, row, text):
        p = self.pixels
        frame = self.ttk.Frame(page)
        frame.grid(row=row, column=0, columnspan=2, sticky="ew", pady=p(8))
        frame.columnconfigure(1, weight=1)
        label = self.ttk.Label(frame, text=text)
        label.grid(row=0, column=0, sticky="w", padx=(0, p(20)))
        return frame, label

    def update_display_mode(self, *args):
        borderless = self.mode_label.get() == "Borderless fullscreen"
        self.resolution_choice.configure(state="disabled" if borderless else "readonly")
        self.resolution_hint.configure(text="Borderless fullscreen uses your desktop resolution automatically." if borderless else "Resolution applies to windowed and fullscreen modes.")

    def scroll_pages(self, event):
        if getattr(event, "num", None) in (4, 5):
            steps = -1 if event.num == 4 else 1
        else:
            delta = event.delta
            steps = max(1, abs(delta) // 120) if delta else 0
            if delta > 0:
                steps = -steps
        self.canvas.yview_scroll(steps, "units")

    def reveal_focus(self, event):
        widget = event.widget
        if widget != self.root.focus_get():
            return
        ancestor = widget
        while ancestor != self.page_container:
            ancestor = getattr(ancestor, "master", None)
            if ancestor is None:
                return
        top = widget.winfo_rooty() - self.canvas.winfo_rooty()
        bottom = top + widget.winfo_height()
        view_height = self.canvas.winfo_height()
        if top < 0 or bottom > view_height:
            position = self.canvas.canvasy(0) + (top if top < 0 else bottom - view_height)
            total = max(1, self.page_container.winfo_height())
            self.canvas.yview_moveto(max(0, position) / total)

    def stop_poll(self, event):
        if event.widget == self.root:
            timer = getattr(self, "poll_timer", None)
            if timer is not None:
                self.root.after_cancel(timer)

    def heading(self, page, title, description):
        p = self.pixels
        name = next(name for name, candidate in self.pages.items() if candidate == page)
        number = tuple(self.pages).index(name) + 1
        self.ttk.Label(page, text="%02d / %s" % (number, name.upper()), style="Eyebrow.TLabel").grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, p(14)))
        self.ttk.Label(page, text=title, style="Title.TLabel").grid(row=1, column=0, columnspan=2, sticky="w", pady=(0, p(12)))
        self.ttk.Label(page, text=description, style="Hint.TLabel", wraplength=p(600)).grid(row=2, column=0, columnspan=2, sticky="w", pady=(0, p(28)))

    def choice(self, page, row, text, variable, options):
        frame, label = self.form_row(page, row, text)
        widget = self.ttk.Combobox(frame, textvariable=variable, values=options, state="readonly", width=18, font=self.fonts["body"])
        widget.grid(row=0, column=1, sticky="ew")
        self.form_rows.append((frame, label, widget))
        return widget

    def build_files(self):
        p = self.pixels
        page = self.pages["Game files"]
        self.heading(page, "Let's swing.", "Your Spider-Man (2000) PC adventure starts here. Choose your disc image or installed game folder.")
        self.choice(page, 3, "Game source", self.source_label, ("ISO disc image", "Installed game folder"))
        frame, label = self.form_row(page, 4, "Location")
        entry = self.ttk.Entry(frame, textvariable=self.vars["source_path"], font=self.fonts["body"], width=18)
        entry.grid(row=0, column=1, sticky="ew")
        self.form_rows.append((frame, label, entry))
        self.ttk.Button(page, text="Browse…", command=self.browse).grid(row=5, column=0, columnspan=2, sticky="e", pady=(p(4), p(28)))
        self.ttk.Label(page, text="ONE IMPORT. THEN YOU'RE READY.", style="Eyebrow.TLabel").grid(row=6, column=0, columnspan=2, sticky="w", pady=(0, p(8)))
        self.ttk.Label(page, text="ISO files are imported once. Keep the disc image anywhere you like. No mounting or Windows installer is needed.", style="Hint.TLabel", wraplength=p(600)).grid(row=7, column=0, columnspan=2, sticky="w", pady=(0, p(16)))
        self.ttk.Label(page, text="Setup remembers your choice. Settings, imported files and game logs live in your user folder.", style="Hint.TLabel", wraplength=p(600)).grid(row=8, column=0, columnspan=2, sticky="w")

    def build_video(self):
        p = self.pixels
        page = self.pages["Video"]
        self.heading(page, "The city. Your screen.", "Choose your display and fine-tune the picture. The original proportions are preserved.")
        self.choice(page, 3, "Display mode", self.mode_label, tuple(MODES))
        self.resolution_choice = self.choice(page, 4, "Resolution", self.vars["resolution"], RESOLUTIONS)
        self.resolution_hint = self.ttk.Label(page, style="Hint.TLabel", wraplength=p(600))
        self.resolution_hint.grid(row=5, column=0, columnspan=2, sticky="w", pady=(0, p(12)))
        self.mode_label.trace_add("write", self.update_display_mode)
        self.update_display_mode()
        self.choice(page, 6, "Antialiasing", self.vars["msaa"], (0, 2, 4, 8))
        self.choice(page, 7, "Texture filtering", self.vars["anisotropy"], (1, 2, 4, 8, 16))
        self.ttk.Label(page, text="Antialiasing: 0 = off, 2 / 4 / 8 = samples.\nTexture filtering: 1 = standard. Higher values sharpen distant textures.", style="Hint.TLabel", wraplength=p(600)).grid(row=8, column=0, columnspan=2, sticky="w", pady=(p(12), p(16)))
        self.ttk.Checkbutton(page, text="Smooth distant textures", variable=self.vars["mipmaps"]).grid(row=9, column=0, columnspan=2, sticky="w", pady=p(8))
        self.ttk.Checkbutton(page, text="VSync (reduce screen tearing)", variable=self.vars["vsync"]).grid(row=10, column=0, columnspan=2, sticky="w", pady=p(8))

    def build_sound(self):
        p = self.pixels
        page = self.pages["Sound"]
        self.heading(page, "Every quip. Every swing.", "Balance the original music, voices and sound effects.")
        for row, (key, label) in enumerate((("master_volume", "Master volume"), ("music_volume", "Music and voices"), ("sfx_volume", "Sound effects")), 3):
            frame, caption = self.form_row(page, row, label)
            control = self.ttk.Frame(frame)
            control.grid(row=0, column=1, sticky="ew")
            self.ttk.Scale(control, from_=0, to=100, command=lambda value, variable=self.vars[key]: variable.set(round(float(value))), variable=self.vars[key]).pack(side="left", fill="x", expand=True, pady=p(10))
            self.ttk.Label(control, textvariable=self.vars[key], width=4, anchor="e", font=self.fonts["nav"]).pack(side="right", padx=(p(16), 0))
            self.form_rows.append((frame, caption, control))
        self.ttk.Label(page, text="Set Master volume to 0 to mute all audio, including cinematics.", style="Hint.TLabel", wraplength=p(600)).grid(row=6, column=0, columnspan=2, sticky="w", pady=p(28))

    def build_controls(self):
        p = self.pixels
        page = self.pages["Controls"]
        self.heading(page, "Ready to swing.", "Use modern mouse controls or the original keyboard layout.")
        self.ttk.Checkbutton(page, text="Modern controls (WASD and mouse look)", variable=self.vars["modern_controls"]).grid(row=3, column=0, columnspan=2, sticky="w", pady=p(8))
        self.choice(page, 4, "Mouse sensitivity", self.vars["mouse_sensitivity"], (0.5, 1, 2, 3, 4, 5, 7, 10, 15, 20))
        self.ttk.Checkbutton(page, text="Invert vertical mouse look", variable=self.vars["invert_mouse_y"]).grid(row=5, column=0, columnspan=2, sticky="w", pady=p(8))
        self.ttk.Checkbutton(page, text="Skip opening movies", variable=self.vars["skip_movies"]).grid(row=6, column=0, columnspan=2, sticky="w", pady=p(8))
        keys = self.ttk.Frame(page)
        keys.grid(row=7, column=0, columnspan=2, sticky="ew", pady=(p(24), 0))
        keys.columnconfigure(1, weight=1)
        for row, (key, action) in enumerate((("WASD / arrows", "Move"), ("Space", "Jump"), ("Left mouse", "Punch"), ("Right mouse", "Shoot webs"), ("Enter", "Select"), ("Esc", "Pause / skip eligible scenes"), ("F1", "Release or capture the mouse"), ("F12", "Quit"))):
            self.ttk.Label(keys, text=key, font=self.fonts["eyebrow"]).grid(row=row, column=0, sticky="nw", padx=(0, p(24)), pady=p(4))
            label = self.ttk.Label(keys, text=action, style="Hint.TLabel", wraplength=p(380))
            label.grid(row=row, column=1, sticky="w", pady=p(4))
        self.control_keys = keys

    def show_page(self, title):
        for page in self.pages.values():
            page.grid_remove()
        self.pages[title].grid(row=0, column=0, sticky="nsew")
        self.current_page = title
        self.canvas.yview_moveto(0)
        for navigation in (self.navigation, self.compact_navigation):
            for name, button in navigation.items():
                selected = name == title
                button.configure(background=PALETTE["red"] if selected else PALETTE["ink"], foreground="white" if selected else PALETTE["sidebar_text"], highlightbackground=PALETTE["red"] if selected else PALETTE["ink"])

    def resize_pages(self, width, item):
        p = self.pixels
        self.canvas.itemconfigure(item, width=width)
        for page in self.pages.values():
            for widget in page.winfo_children():
                if widget.winfo_class() == "TLabel" and int(widget.grid_info().get("columnspan", 1)) == 2:
                    widget.configure(wraplength=max(p(100), width - p(4)))
        stacked = width < p(520)
        for frame, label, widget in self.form_rows:
            label_width = max(self.fonts["body"].measure(caption.cget("text")) for candidate, caption, control in self.form_rows if candidate.master == frame.master) + p(20)
            frame.columnconfigure(0, weight=1 if stacked else 0, minsize=0 if stacked else label_width)
            frame.columnconfigure(1, weight=0 if stacked else 1)
            label.grid_configure(pady=(0, p(6)) if stacked else 0)
            widget.grid_configure(row=1 if stacked else 0, column=0 if stacked else 1)
        if hasattr(self, "control_keys"):
            key_width = max(self.fonts["eyebrow"].measure(widget.cget("text")) for widget in self.control_keys.winfo_children() if widget.grid_info()["column"] == 0)
            for widget in self.control_keys.winfo_children():
                if widget.grid_info()["column"] == 1:
                    widget.configure(wraplength=max(p(90), width - key_width - p(28)))

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

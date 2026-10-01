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

def run_game(binary, settings, data_path):
    directory = validate_game_dir(settings["game_dir"])
    logs = Path(data_path) / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    log = logs / ("game-%s.log" % datetime.now().strftime("%Y%m%d-%H%M%S-%f"))
    env = game_environment(settings, binary)
    frozen_windows = getattr(sys, "frozen", False) and os.name == "nt"
    if frozen_windows:
        import ctypes
        ctypes.windll.kernel32.SetDllDirectoryW(None)
    with log.open("w", encoding="utf-8") as output:
        flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
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
        root.title("OpenSpidey • Setup")
        width, height = min(860, root.winfo_screenwidth() - 40), min(650, root.winfo_screenheight() - 70)
        root.minsize(min(780, width), min(580, height))
        root.geometry("%dx%d+%d+%d" % (width, height, max(0, (root.winfo_screenwidth()-width)//2), max(0, (root.winfo_screenheight()-height)//2)))
        root.configure(background="#f4f5f7")
        root.protocol("WM_DELETE_WINDOW", self.close)
        style = ttk.Style(root)
        style.theme_use("clam")
        style.configure("TFrame", background="#f4f5f7")
        style.configure("TLabel", background="#f4f5f7", foreground="#202a38", font=("Segoe UI", 11))
        style.configure("Title.TLabel", font=("Segoe UI", 21, "bold"))
        style.configure("Hint.TLabel", foreground="#596779", font=("Segoe UI", 10))
        style.configure("TCheckbutton", background="#f4f5f7", font=("Segoe UI", 11))
        style.configure("TButton", padding=(12, 8), font=("Segoe UI", 10))
        style.configure("Start.TButton", background="#c62e42", foreground="white", font=("Segoe UI", 12, "bold"))
        style.map("Start.TButton", background=[("active", "#ad2337"), ("disabled", "#9fabb8")])
        self.vars = {}
        for key, value in settings.items():
            if key == "schema":
                continue
            var_type = tk.BooleanVar if type(value) is bool else tk.DoubleVar if key == "mouse_sensitivity" else tk.IntVar if type(value) is int else tk.StringVar
            self.vars[key] = var_type(root, value=value)
        self.mode_label = tk.StringVar(root, next(label for label, value in MODES.items() if value == settings["window_mode"]))
        self.source_label = tk.StringVar(root, "ISO disc image" if settings["source_mode"] == "iso" else "Installed game folder")
        self.status = tk.StringVar(root, message or "Choose your game files, then press Start game.")
        shell = ttk.Frame(root)
        shell.pack(fill="both", expand=True)
        compact = width < 800
        sidebar = tk.Frame(shell, background="#172334", width=140 if compact else 180)
        sidebar.pack(side="left", fill="y")
        sidebar.pack_propagate(False)
        tk.Label(sidebar, text="OPEN\nSPIDEY", background="#172334", foreground="white", font=("Segoe UI", 18 if compact else 23, "bold"), justify="left").pack(anchor="w", padx=16 if compact else 22, pady=(30, 4))
        tk.Label(sidebar, text="Spider-Man 2000\nNative PC edition", background="#172334", foreground="#bac6d6", font=("Segoe UI", 10), justify="left").pack(anchor="w", padx=22, pady=(0, 28))
        self.pages, self.navigation = {}, {}
        content = ttk.Frame(shell, padding=(16 if compact else 28, 18 if compact else 24))
        content.pack(side="right", fill="both", expand=True)
        content.rowconfigure(0, weight=1)
        content.columnconfigure(0, weight=1)
        scroll_area = ttk.Frame(content)
        scroll_area.grid(row=0, column=0, sticky="nsew")
        self.canvas = tk.Canvas(scroll_area, background="#f4f5f7", highlightthickness=0)
        scrollbar = ttk.Scrollbar(scroll_area, orient="vertical", command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side="right", fill="y")
        self.canvas.pack(side="left", fill="both", expand=True)
        self.page_container = ttk.Frame(self.canvas)
        self.page_container.columnconfigure(0, weight=1)
        page_item = self.canvas.create_window((0, 0), window=self.page_container, anchor="nw")
        self.canvas.bind("<Configure>", lambda event: self.resize_pages(event.width, page_item))
        self.page_container.bind("<Configure>", lambda event: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        for title in ("Game files", "Video", "Sound", "Controls"):
            page = ttk.Frame(self.page_container)
            page.columnconfigure(1, weight=1)
            self.pages[title] = page
            button = tk.Button(sidebar, text=title, anchor="w", relief="flat", borderwidth=0, highlightthickness=0,
                               background="#172334", foreground="#bac6d6", activebackground="#29394e", activeforeground="white",
                               font=("Segoe UI", 12), padx=22, pady=13, command=lambda name=title: self.show_page(name))
            button.pack(fill="x")
            self.navigation[title] = button
        tk.Label(sidebar, text="v" + VERSION, background="#172334", foreground="#8595a8", font=("Segoe UI", 10)).pack(side="bottom", anchor="w", padx=22, pady=20)
        self.build_files()
        self.build_video()
        self.build_sound()
        self.build_controls()
        footer = ttk.Frame(content)
        footer.grid(row=1, column=0, sticky="ew", pady=(15, 0))
        footer.columnconfigure(0, weight=1)
        self.progress = ttk.Progressbar(footer, maximum=100)
        self.progress.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 8))
        ttk.Label(footer, textvariable=self.status, style="Hint.TLabel", wraplength=width - (210 if compact else 250)).grid(row=1, column=0, columnspan=2, sticky="w", pady=(0, 15))
        ttk.Checkbutton(footer, text="Show setup every time", variable=self.vars["show_setup"]).grid(row=2, column=0, columnspan=2 if compact else 1, sticky="w")
        actions = ttk.Frame(footer)
        actions.grid(row=3 if compact else 2, column=1, sticky="e", pady=(8 if compact else 0, 0))
        self.cancel_button = ttk.Button(actions, text="Close", command=self.close)
        self.cancel_button.pack(side="left", padx=(0, 8))
        self.start_button = ttk.Button(actions, text="Start game", style="Start.TButton", command=self.start)
        self.start_button.pack(side="left")
        self.show_page("Game files")
        root.bind("<Escape>", lambda event: self.close())
        root.bind("<MouseWheel>", lambda event: self.canvas.yview_scroll(-int(event.delta / 120), "units"))
        root.bind("<Button-4>", lambda event: self.canvas.yview_scroll(-1, "units"))
        root.bind("<Button-5>", lambda event: self.canvas.yview_scroll(1, "units"))
        root.after(75, self.poll)

    def heading(self, page, title, description):
        self.ttk.Label(page, text=title, style="Title.TLabel").grid(row=0, column=0, columnspan=2, sticky="w", pady=(0, 9))
        self.ttk.Label(page, text=description, style="Hint.TLabel", wraplength=540).grid(row=1, column=0, columnspan=2, sticky="w", pady=(0, 25))

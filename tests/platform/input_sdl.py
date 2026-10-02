"""Exercise keyboard polling using real X11 input and window-close messages."""
import os
from pathlib import Path
import selectors
import subprocess
import sys

from Xlib import X, XK, display, protocol
from Xlib.ext import xtest


def read_line(process, selector):
    if not selector.select(timeout=5):
        raise AssertionError("Timed out waiting for the SDL fixture")
    line = process.stdout.readline().decode()
    if not line:
        raise AssertionError(f"SDL fixture exited with {process.poll()}")
    print(line, end="", flush=True)
    return line.strip()


def check(process, selector, label, key, down, quit):
    process.stdin.write(f"{label} {key} {int(down)} {int(quit)}\n".encode())
    process.stdin.flush()
    while read_line(process, selector) != f"PASS {label}":
        pass


def run(binary, mode):
    connection = display.Display()
    env = os.environ.copy()
    env.update(SDL_VIDEO_DRIVER="x11", SDL_AUDIO_DRIVER="dummy",
               SPIDEY_WINDOW_MODE="windowed", SPIDEY_VSYNC="0", SPIDEY_MSAA="0")
    process = subprocess.Popen([binary], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, bufsize=0, env=env)
    selector = selectors.DefaultSelector()
    selector.register(process.stdout, selectors.EVENT_READ)
    pressed = set()
    try:
        while True:
            line = read_line(process, selector)
            if line.startswith("READY "):
                window = connection.create_resource_object("window", int(line.split()[1]))
                break
        window.set_input_focus(X.RevertToParent, X.CurrentTime)
        connection.sync()
        check(process, selector, "initial", 0x1E, False, False)

        for name, dik in (("a", 0x1E), ("Escape", 0x01)):
            keycode = connection.keysym_to_keycode(XK.string_to_keysym(name))
            xtest.fake_input(connection, X.KeyPress, keycode)
            pressed.add(keycode)
            connection.sync()
            check(process, selector, name + "-down", dik, True, False)
            xtest.fake_input(connection, X.KeyRelease, keycode)
            pressed.remove(keycode)
            connection.sync()
            check(process, selector, name + "-up", dik, False, False)

        if mode == "f12":
            keycode = connection.keysym_to_keycode(XK.string_to_keysym("F12"))
            xtest.fake_input(connection, X.KeyPress, keycode)
            pressed.add(keycode)
            connection.sync()
            check(process, selector, "f12-down", 0x58, True, True)
            xtest.fake_input(connection, X.KeyRelease, keycode)
            pressed.remove(keycode)
            connection.sync()
            check(process, selector, "f12-up", 0x58, False, True)
        else:
            event = protocol.event.ClientMessage(
                window=window.id, client_type=connection.intern_atom("WM_PROTOCOLS"),
                data=(32, [connection.intern_atom("WM_DELETE_WINDOW"), X.CurrentTime, 0, 0, 0]))
            window.send_event(event, event_mask=0)
            connection.sync()
            check(process, selector, "window-close", 0x1E, False, True)

        process.stdin.close()
        assert process.wait(timeout=5) == 0
    finally:
        for keycode in pressed:
            xtest.fake_input(connection, X.KeyRelease, keycode)
        connection.sync()
        connection.close()
        selector.close()
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=5)


if __name__ == "__main__":
    binary = str(Path(sys.argv[1]).resolve())
    run(binary, "f12")
    run(binary, "close")
    print("PASS real X11 key down/up, F12 and close during keyboard-only polling")

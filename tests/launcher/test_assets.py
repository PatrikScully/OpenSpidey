"""Small synthetic discs cover import failures without sharing game assets."""

import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import struct
import tempfile
import unittest
from unittest import mock


SPEC = importlib.util.spec_from_file_location(
    "openspidey_assets", Path(__file__).resolve().parents[2] / "platform" / "launcher" / "assets.py")
assets = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(assets)
EXE = b"MZ" + b"test executable data" * 8
PAYLOAD = b"test asset data"
ARCHIVE = (b"PKR3" + struct.pack("<I", 8 + len(PAYLOAD)) + PAYLOAD
           + struct.pack("<III", 4, 1, 1)
           + b"data\\\x00".ljust(32, b"\x00") + struct.pack("<II", 0, 1)
           + b"test.bin\x00".ljust(32, b"\x00") + struct.pack("<IIIII", 0, 0xFFFFFFFE, 8,
                                                                  len(PAYLOAD), len(PAYLOAD)))
PAYLOADS = {"SpideyPC.exe": EXE, "data.pkr": ARCHIVE, "media.pkr": ARCHIVE, "texture.dat": b"x" * 40}


def make_record(name, extent, size, flags=0, joliet=False):
    identifier = name if isinstance(name, bytes) else name.encode("utf-16-be" if joliet else "ascii")
    record = bytearray(33 + len(identifier) + (len(identifier) % 2 == 0))
    record[0] = len(record)
    struct.pack_into("<I", record, 2, extent)
    struct.pack_into(">I", record, 6, extent)
    struct.pack_into("<I", record, 10, size)
    struct.pack_into(">I", record, 14, size)
    record[25] = flags
    record[28:32] = b"\x01\x00\x00\x01"
    record[32] = len(identifier)
    record[33:33 + len(identifier)] = identifier
    return record



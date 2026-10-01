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


def make_iso(path, joliet=False, names=None):
    image = bytearray(32 * assets.BLOCK_SIZE)
    names = list(PAYLOADS) if names is None else names
    descriptor = bytearray(assets.BLOCK_SIZE)
    descriptor[0:7] = b"\x01CD001\x01"
    struct.pack_into("<I", descriptor, 80, 32)
    struct.pack_into(">I", descriptor, 84, 32)
    descriptor[128:132] = b"\x00\x08\x08\x00"
    root = make_record(b"\x00", 20, assets.BLOCK_SIZE, 2)
    descriptor[156:156 + len(root)] = root
    image[16 * assets.BLOCK_SIZE:17 * assets.BLOCK_SIZE] = descriptor
    end_sector = 17
    if joliet:
        descriptor[0] = 2
        descriptor[88:91] = b"%/E"
        image[17 * assets.BLOCK_SIZE:18 * assets.BLOCK_SIZE] = descriptor
        end_sector = 18
    image[end_sector * assets.BLOCK_SIZE:end_sector * assets.BLOCK_SIZE + 7] = b"\xffCD001\x01"
    records = bytearray(root + make_record(b"\x01", 20, assets.BLOCK_SIZE, 2))
    offsets = {}
    for index, name in enumerate(names):
        payload = PAYLOADS.get(name, ARCHIVE)
        record = make_record(name + ";1", 24 + index, len(payload), joliet=joliet)
        offsets[name] = 20 * assets.BLOCK_SIZE + len(records)
        records.extend(record)
        image[(24 + index) * assets.BLOCK_SIZE:(24 + index) * assets.BLOCK_SIZE + len(payload)] = payload
    image[20 * assets.BLOCK_SIZE:20 * assets.BLOCK_SIZE + len(records)] = records
    path.write_bytes(image)
    return offsets


class AssetTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="openspidey-assets-test-")
        self.root = Path(self.temporary.name)
        self.iso = self.root / "Spidey disc.iso"
        self.destination = self.root / "game"
        self.supported = mock.patch.object(assets, "SUPPORTED_EXE_SHA256", hashlib.sha256(EXE).hexdigest())
        self.supported.start()

    def tearDown(self):
        self.supported.stop()
        self.temporary.cleanup()


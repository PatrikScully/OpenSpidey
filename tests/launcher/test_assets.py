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

    def test_iso_trees_and_import(self):
        for joliet in (False, True):
            with self.subTest(joliet=joliet):
                make_iso(self.iso, joliet=joliet)
                progress = []
                result = assets.import_iso(self.iso, self.destination, lambda *event: progress.append(event))
                self.assertEqual(result, self.destination)
                self.assertEqual(assets.validate_game_dir(result), result)
                self.assertEqual(set(assets.inspect_iso(self.iso)["files"]), set(PAYLOADS))
                for name, payload in PAYLOADS.items():
                    self.assertEqual((result / name).read_bytes(), payload)
                    self.assertTrue(os.access(result / name, os.W_OK))
                self.assertEqual(progress[0][0], 0)
                self.assertEqual(progress[-1][0], sum(map(len, PAYLOADS.values())))
                self.assertEqual([event[0] for event in progress], sorted(event[0] for event in progress))
                self.assertEqual(len(json.loads((result / assets.MANIFEST_NAME).read_text())["sha256"]), 4)

    def test_cache_reuse_and_repair(self):
        make_iso(self.iso)
        assets.import_iso(self.iso, self.destination)
        inode = (self.destination / "data.pkr").stat().st_ino
        with mock.patch.object(assets, "_copy_asset", side_effect=AssertionError("Cache was copied again")):
            assets.import_iso(self.iso, self.destination)
        self.assertEqual((self.destination / "data.pkr").stat().st_ino, inode)
        damaged = bytearray(ARCHIVE)
        damaged[8] ^= 1
        (self.destination / "data.pkr").write_bytes(damaged)
        assets.import_iso(self.iso, self.destination)
        self.assertEqual((self.destination / "data.pkr").read_bytes(), ARCHIVE)

    def test_source_change_preserves_saves(self):
        make_iso(self.iso)
        assets.import_iso(self.iso, self.destination)
        saves = self.destination / "save"
        saves.mkdir()
        (saves / "SPIDRMAN.DAT").write_bytes(b"player progress")
        previous = json.loads((self.destination / assets.MANIFEST_NAME).read_text())["source"]
        stat = self.iso.stat()
        os.utime(self.iso, ns=(stat.st_atime_ns, stat.st_mtime_ns + 1000000))
        assets.import_iso(self.iso, self.destination)
        current = json.loads((self.destination / assets.MANIFEST_NAME).read_text())["source"]
        self.assertNotEqual(previous, current)
        self.assertEqual((saves / "SPIDRMAN.DAT").read_bytes(), b"player progress")

    def test_cancel_and_changed_source_leave_existing_game(self):
        make_iso(self.iso)
        assets.import_iso(self.iso, self.destination)
        before = (self.destination / assets.MANIFEST_NAME).read_bytes()
        with self.assertRaises(assets.ImportCancelled):
            assets.import_iso(self.iso, self.destination, cancelled=lambda: True)
        with mock.patch.object(assets, "_valid_cache", return_value=False):
            progress = []
            with self.assertRaises(assets.ImportCancelled):
                assets.import_iso(self.iso, self.destination, lambda *event: progress.append(event),
                                  lambda: bool(progress and progress[-1][0]))
        self.assertEqual((self.destination / assets.MANIFEST_NAME).read_bytes(), before)
        self.assertEqual(list(self.root.glob(".game.import-*")), [self.root / ".game.import-lock"])
        with mock.patch.object(assets, "_valid_cache", return_value=False):
            with self.assertRaisesRegex(assets.AssetError, "changed during import"):
                assets.import_iso(self.iso, self.destination,
                                  lambda *unused: os.utime(self.iso, ns=(0, self.iso.stat().st_mtime_ns + 1000000)))
        self.assertEqual((self.destination / assets.MANIFEST_NAME).read_bytes(), before)

    def test_truncated_and_malformed_iso(self):
        for fault in ("header", "bounds", "endian", "extent", "name", "multiextent", "record", "duplicate", "cycle"):
            with self.subTest(fault=fault):
                offsets = make_iso(self.iso, names=list(PAYLOADS) + ["DATA.PKR"] if fault == "duplicate" else None)
                image = bytearray(self.iso.read_bytes())
                if fault == "header":
                    image[16 * assets.BLOCK_SIZE + 1] = 0
                elif fault == "bounds":
                    image = image[:-assets.BLOCK_SIZE]
                elif fault == "endian":
                    image[offsets["data.pkr"] + 6] ^= 1
                elif fault == "extent":
                    struct.pack_into("<I", image, offsets["data.pkr"] + 2, 99)
                    struct.pack_into(">I", image, offsets["data.pkr"] + 6, 99)
                elif fault == "name":
                    image[offsets["data.pkr"] + 33] = ord("/")
                elif fault == "multiextent":
                    image[offsets["data.pkr"] + 25] |= 0x80
                elif fault == "cycle":
                    end = offsets["texture.dat"] + image[offsets["texture.dat"]]
                    record = make_record("loop", 20, assets.BLOCK_SIZE, 2)
                    image[end:end + len(record)] = record
                elif fault == "duplicate":
                    pass
                else:
                    image[offsets["data.pkr"]] = 8
                self.iso.write_bytes(image)
                with self.assertRaises(assets.AssetError):
                    assets.import_iso(self.iso, self.destination)
                self.assertFalse(self.destination.exists())

    def test_wrong_disc_and_invalid_archives(self):
        for fault in ("missing", "exe", "magic", "count", "asset"):
            with self.subTest(fault=fault):
                make_iso(self.iso, names=list(PAYLOADS)[:-1] if fault == "missing" else None)
                image = bytearray(self.iso.read_bytes())
                if fault == "exe":
                    image[24 * assets.BLOCK_SIZE + 8] ^= 1
                elif fault == "magic":
                    image[25 * assets.BLOCK_SIZE] = 0
                elif fault == "count":
                    struct.pack_into("<I", image, 25 * assets.BLOCK_SIZE + 8 + len(PAYLOAD) + 8, 0xFFFFFFFF)
                elif fault == "asset":
                    struct.pack_into("<I", image, 25 * assets.BLOCK_SIZE + 8 + len(PAYLOAD) + 12 + 40 + 40,
                                     0xFFFFFFF0)
                self.iso.write_bytes(image)
                with self.assertRaises(assets.AssetError):
                    assets.inspect_iso(self.iso)

    def test_space_lock_and_destination_errors(self):
        make_iso(self.iso)
        usage = shutil.disk_usage(self.root)
        with mock.patch.object(assets.shutil, "disk_usage", return_value=usage._replace(free=0)):
            with self.assertRaisesRegex(assets.AssetError, "free space"):
                assets.import_iso(self.iso, self.destination)
        with assets._import_lock(self.destination):
            with self.assertRaisesRegex(assets.AssetError, "Another asset import"):
                assets.import_iso(self.iso, self.destination)
        assets.import_iso(self.iso, self.destination)
        contained = self.destination / "disc.iso"
        shutil.copyfile(self.iso, contained)
        with self.assertRaisesRegex(assets.AssetError, "outside"):
            assets.import_iso(contained, self.destination)
        if os.name != "nt":
            link = self.root / "cache-link"
            link.symlink_to(self.destination, target_is_directory=True)
            with self.assertRaisesRegex(assets.AssetError, "symbolic link"):
                assets.import_iso(self.iso, link)

    def test_missing_and_read_only_installed_assets(self):
        with self.assertRaisesRegex(assets.AssetError, "Missing"):
            assets.validate_game_dir(self.destination)
        make_iso(self.iso)
        assets.import_iso(self.iso, self.destination)
        with mock.patch.object(assets.os, "access", return_value=False):
            with self.assertRaisesRegex(assets.AssetError, "writable"):
                assets.validate_game_dir(self.destination)
        (self.destination / "texture.dat").write_bytes(b"truncated")
        with self.assertRaisesRegex(assets.AssetError, "damaged"):
            assets.validate_game_dir(self.destination)


if __name__ == "__main__":
    unittest.main()

"""Read the user's PC disc and copy the native engine's four data files."""

import hashlib
from contextlib import contextmanager
import json
import os
from pathlib import Path
import shutil
import struct
import tempfile
import uuid


REQUIRED_FILES = ("SpideyPC.exe", "data.pkr", "media.pkr", "texture.dat")
SUPPORTED_EXE_SHA256 = "0a11a49f3f63d4bb15650f322d654553fc47234b01885b852a058ff074689dea"
MANIFEST_NAME = ".openspidey-assets.json"
BLOCK_SIZE = 2048
CHUNK_SIZE = 1024 * 1024


class AssetError(ValueError):
    """The selected assets cannot be used by this build."""


class ImportCancelled(AssetError):
    """The user cancelled the import."""


def _validate_payload(name, header, size, digest=None):
    if name == "SpideyPC.exe":
        if digest != SUPPORTED_EXE_SHA256:
            raise AssetError("This PC executable is not supported by this build. "
                             "Use the original Spider-Man (2000) PC disc.")
    elif name.endswith(".pkr"):
        if len(header) < 8 or header[:4] != b"PKR3":
            raise AssetError("%s is not a Spider-Man asset archive." % name)
        directory = struct.unpack_from("<I", header, 4)[0]
        if not 8 <= directory <= size - 8:
            raise AssetError("%s has a damaged archive index." % name)
    elif size != 40:
        raise AssetError("texture.dat is missing or damaged.")


def _validate_archive(stream, offset, size, name):
    stream.seek(offset)
    header = stream.read(8)
    _validate_payload(name, header, size)
    directory = struct.unpack_from("<I", header, 4)[0]
    stream.seek(offset + directory)
    footer = stream.read(12)
    if len(footer) != 12:
        raise AssetError("%s has an incomplete archive index." % name)
    unused_alignment, directories, files = struct.unpack("<III", footer)
    if not 1 <= directories <= 16384 or not 1 <= files <= 100000:
        raise AssetError("%s has invalid archive counts." % name)
    index_size = 12 + directories * 40 + files * 52
    if index_size > size - directory:
        raise AssetError("%s has an incomplete archive index." % name)
    total_files = 0
    for unused in range(directories):
        record = stream.read(40)
        start, count = struct.unpack_from("<II", record, 32)
        if b"\x00" not in record[:32] or start != total_files or count > files - total_files:
            raise AssetError("%s has a damaged directory index." % name)
        total_files += count
    if total_files != files:
        raise AssetError("%s has inconsistent archive counts." % name)
    for unused in range(files):
        record = stream.read(52)
        method, position, expanded, compressed = struct.unpack_from("<IIII", record, 36)
        if (b"\x00" not in record[:32] or method not in (0, 1, 2, 3, 0xFFFFFFFE)
                or position < 8 or position > directory or compressed > directory - position
                or compressed == 0 or expanded == 0):
            raise AssetError("%s has an invalid asset entry." % name)



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


def _directory_record(record, image_size, joliet):
    if len(record) < 34 or record[0] != len(record):
        raise AssetError("The ISO has a damaged directory record.")
    length = record[32]
    if length == 0 or 33 + length > len(record):
        raise AssetError("The ISO has an invalid file name.")
    extent, big_extent = struct.unpack_from("<I", record, 2)[0], struct.unpack_from(">I", record, 6)[0]
    size, big_size = struct.unpack_from("<I", record, 10)[0], struct.unpack_from(">I", record, 14)[0]
    if extent != big_extent or size != big_size:
        raise AssetError("The ISO has inconsistent directory data.")
    offset = (extent + record[1]) * BLOCK_SIZE
    if offset > image_size or size > image_size - offset:
        raise AssetError("The ISO is incomplete or has an invalid file extent.")
    identifier = record[33:33 + length]
    if identifier in (b"\x00", b"\x01"):
        name = None
    else:
        try:
            name = identifier.decode("utf-16-be" if joliet else "ascii")
        except UnicodeDecodeError as error:
            raise AssetError("The ISO has an invalid file name.") from error
        name = name.rsplit(";", 1)[0] if ";" in name and name.rsplit(";", 1)[1].isdigit() else name
        if name in (".", "..") or any(ord(char) < 32 or char in "/\\:" for char in name):
            raise AssetError("The ISO has an unsafe file name.")
    return {"name": name, "offset": offset, "size": size, "directory": bool(record[25] & 2),
            "unsupported": bool(record[25] & 0x84 or record[26] or record[27])}


def _scan_iso(stream, image_size):
    primary = None
    supplementary = None
    descriptors = hashlib.sha256()
    terminated = False
    for sector in range(16, 80):
        stream.seek(sector * BLOCK_SIZE)
        descriptor = stream.read(BLOCK_SIZE)
        if len(descriptor) != BLOCK_SIZE or descriptor[1:6] != b"CD001" or descriptor[6] != 1:
            raise AssetError("Choose an ISO image of the Spider-Man (2000) PC disc.")
        descriptors.update(descriptor)
        if descriptor[0] == 255:
            terminated = True
            break
        if descriptor[0] == 1:
            primary = descriptor
        elif descriptor[0] == 2 and descriptor[88:91] in (b"%/@", b"%/C", b"%/E"):
            supplementary = descriptor
    if primary is None or not terminated:
        raise AssetError("The ISO volume descriptors are incomplete.")
    descriptor = supplementary or primary
    joliet = supplementary is not None
    block_size = struct.unpack_from("<H", descriptor, 128)[0]
    big_block_size = struct.unpack_from(">H", descriptor, 130)[0]
    volume_blocks = struct.unpack_from("<I", descriptor, 80)[0]
    big_volume_blocks = struct.unpack_from(">I", descriptor, 84)[0]
    if block_size != BLOCK_SIZE or big_block_size != BLOCK_SIZE:
        raise AssetError("This ISO uses an unsupported block size.")
    volume_size = volume_blocks * BLOCK_SIZE
    if volume_blocks != big_volume_blocks or volume_size > image_size or volume_size < 19 * BLOCK_SIZE:
        raise AssetError("The ISO is incomplete or has invalid volume bounds.")
    root_length = descriptor[156]
    root = _directory_record(descriptor[156:156 + root_length], volume_size, joliet)
    if not root["directory"] or root["unsupported"]:
        raise AssetError("The ISO root directory is invalid.")
    pending = [((), root)]
    visited = set()
    entries = {}
    count = 0
    while pending:
        parent, directory = pending.pop()
        key = (directory["offset"], directory["size"])
        if key in visited or len(parent) > 32 or directory["size"] > 16 * CHUNK_SIZE:
            raise AssetError("The ISO has an invalid directory tree.")
        visited.add(key)
        stream.seek(directory["offset"])
        data = stream.read(directory["size"])
        if len(data) != directory["size"]:
            raise AssetError("The ISO directory is incomplete.")
        position = 0
        while position < len(data):
            length = data[position]
            if length == 0:
                position = (position // BLOCK_SIZE + 1) * BLOCK_SIZE
                continue
            if position % BLOCK_SIZE + length > BLOCK_SIZE:
                raise AssetError("An ISO directory record crosses a block boundary.")
            record = _directory_record(data[position:position + length], volume_size, joliet)
            position += length
            if record["name"] is None:
                continue
            count += 1
            if count > 16384:
                raise AssetError("The ISO directory tree is too large.")
            path = parent + (record["name"],)
            normalized = "/".join(path).casefold()
            if normalized in entries:
                raise AssetError("The ISO has duplicate file names.")
            entries[normalized] = record
            if record["directory"]:
                if record["unsupported"]:
                    raise AssetError("The ISO uses unsupported directory storage.")
                pending.append((path, record))
    return entries, descriptors.hexdigest()


def _source_state(path):
    stat = path.stat()
    if not path.is_file():
        raise AssetError("Choose an ISO file, not a folder.")
    return {"path": str(path), "size": stat.st_size, "mtime_ns": stat.st_mtime_ns}


def inspect_iso(iso_path):
    path = Path(iso_path).expanduser().resolve()
    try:
        source = _source_state(path)
        with path.open("rb") as stream:
            entries, descriptor_hash = _scan_iso(stream, source["size"])
            parents = set()
            for key, entry in entries.items():
                if not entry["directory"] and key.rsplit("/", 1)[-1] == "spideypc.exe":
                    parents.add(key.rsplit("/", 1)[0] if "/" in key else "")
            complete = []
            for parent in sorted(parents):
                candidate = {}
                for name in REQUIRED_FILES:
                    entry = entries.get((parent + "/" if parent else "") + name.casefold())
                    if entry is None or entry["directory"]:
                        break
                    candidate[name] = entry
                if len(candidate) == len(REQUIRED_FILES):
                    complete.append(candidate)
            if len(complete) != 1:
                raise AssetError("The ISO must contain one complete Spider-Man PC installation "
                                 "with SpideyPC.exe, data.pkr, media.pkr and texture.dat.")
            files = complete[0]
            for name, entry in files.items():
                if entry["unsupported"]:
                    raise AssetError("%s uses unsupported ISO storage." % name)
                stream.seek(entry["offset"])
                header = stream.read(min(entry["size"], 8))
                digest = None
                if name == "SpideyPC.exe":
                    if entry["size"] > 16 * CHUNK_SIZE:
                        raise AssetError("The PC executable is too large for this build.")
                    stream.seek(entry["offset"])
                    payload = stream.read(entry["size"])
                    if len(payload) != entry["size"]:
                        raise AssetError("The ISO executable is incomplete.")
                    digest = hashlib.sha256(payload).hexdigest()
                _validate_payload(name, header, entry["size"], digest)
                if name.endswith(".pkr"):
                    _validate_archive(stream, entry["offset"], entry["size"], name)
        if _source_state(path) != source:
            raise AssetError("The ISO changed while it was being checked. Try again.")
        source["descriptors_sha256"] = descriptor_hash
        return {"source": source, "files": files, "total_bytes": sum(item["size"] for item in files.values())}
    except OSError as error:
        raise AssetError("Cannot read the selected ISO: %s" % error) from error


def validate_game_dir(game_dir):
    path = Path(game_dir).expanduser().resolve()
    try:
        for name in REQUIRED_FILES:
            asset = path / name
            if not asset.is_file():
                raise AssetError("Missing %s. Select the PC game disc ISO to import its assets." % name)
            size = asset.stat().st_size
            with asset.open("rb") as stream:
                header = stream.read(8)
                digest = None
                if name == "SpideyPC.exe":
                    if size > 16 * CHUNK_SIZE:
                        raise AssetError("The PC executable is too large for this build.")
                    stream.seek(0)
                    digest = hashlib.sha256(stream.read()).hexdigest()
            _validate_payload(name, header, size, digest)
            if name.endswith(".pkr"):
                with asset.open("rb") as stream:
                    _validate_archive(stream, 0, size, name)
            if name.endswith(".pkr") and not os.access(asset, os.W_OK):
                raise AssetError("%s must be writable. Import the ISO into your user data folder." % name)
        if not os.access(path, os.W_OK):
            raise AssetError("The game folder must be writable so progress can be saved.")
        return path
    except OSError as error:
        raise AssetError("Cannot read the game assets: %s" % error) from error


def _copy_asset(stream, entry, target, completed, total, progress, cancelled):
    digest = hashlib.sha256()
    remaining = entry["size"]
    stream.seek(entry["offset"])
    with target.open("xb") as output:
        while remaining:
            if cancelled and cancelled():
                raise ImportCancelled("Asset import cancelled.")
            block = stream.read(min(CHUNK_SIZE, remaining))
            if not block:
                raise AssetError("The ISO ended while reading %s." % target.name)
            output.write(block)
            digest.update(block)
            remaining -= len(block)
            completed += len(block)
            if progress:
                progress(completed, total, target.name)
        output.flush()
        os.fsync(output.fileno())
    target.chmod(0o600)
    return completed, digest.hexdigest()



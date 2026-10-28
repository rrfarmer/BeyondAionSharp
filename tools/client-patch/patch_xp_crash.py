"""Reversible patch for the verified Aion 4.8 x64 XP address-check crash.

No arguments that write have defaults. `inspect` only reads files.
Uses only Python's standard library; never downloads or launches client code.
"""

import argparse
import csv
import ctypes
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile


ORIGINAL_SHA256 = "d8645dadfb91e1c2af08a6c9ae313e3d5e07e59ed9b1d004dddcb7b516047296"
PATCHED_SHA256 = "0b103f3bdfa9a83b2a733f1b734ccf9ec35f444360f0757dd9375bed4ecad0ff"
IMAGE_BASE = 0x10000000
PATCH_RVA = 0x2CB6EA
ORIGINAL_BYTES = bytes.fromhex("83 0d fb c6 05 01 01")
PATCHED_BYTES = b"\x90" * 7
BACKUP_SUFFIX = ".xp-address-check.original.bak"


class PatchError(Exception):
    """Refused input or an unsuccessful operation."""


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked_path(value):
    path = Path(value).absolute()
    # Reject link aliases rather than resolving them to an unexpected target.
    if path.is_symlink() or path.resolve() != path:
        raise PatchError("Use the real DLL path, without symlinks or junction aliases.")
    if not path.is_file():
        raise PatchError(f"DLL is not a regular file: {path}")
    if path.stat().st_nlink != 1:
        raise PatchError("Refusing a DLL with multiple hard links.")
    return path


def raw_offset(data):
    """Map the known RVA through the PE section table, never as a file offset."""
    try:
        if data[:2] != b"MZ":
            raise ValueError("missing DOS signature")
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            raise ValueError("missing PE signature")
        machine, count = struct.unpack_from("<HH", data, pe + 4)
        optional_size = struct.unpack_from("<H", data, pe + 20)[0]
        optional = pe + 24
        if machine != 0x8664 or optional_size < 112:
            raise ValueError("expected an AMD64 PE32+ DLL")
        if struct.unpack_from("<H", data, optional)[0] != 0x20B:
            raise ValueError("expected PE32+")
        if struct.unpack_from("<Q", data, optional + 24)[0] != IMAGE_BASE:
            raise ValueError("unexpected preferred image base")
        matches = []
        for index in range(count):
            section = optional + optional_size + index * 40
            _, va, size, offset = struct.unpack_from("<IIII", data, section + 8)
            flags = struct.unpack_from("<I", data, section + 36)[0]
            if va <= PATCH_RVA and PATCH_RVA + len(ORIGINAL_BYTES) <= va + size:
                raw = offset + PATCH_RVA - va
                if not flags & 0x20000000 or raw + len(ORIGINAL_BYTES) > len(data):
                    raise ValueError("patch is not in file-backed executable code")
                matches.append(raw)
        if len(matches) != 1:
            raise ValueError("RVA must map to exactly one executable section")
        return matches[0]
    except (ValueError, struct.error) as error:
        raise PatchError(f"Invalid PE layout: {error}") from error


def identify(data):
    sha = digest(data)
    if sha == ORIGINAL_SHA256:
        state, expected = "original", ORIGINAL_BYTES
    elif sha == PATCHED_SHA256:
        state, expected = "patched", PATCHED_BYTES
    else:
        raise PatchError(f"Unknown DLL SHA-256 {sha}; no files changed.")
    offset = raw_offset(data)
    if data[offset:offset + len(expected)] != expected:
        raise PatchError("Instruction bytes disagree with the supported build.")
    return state, offset


def backup_path(path):
    return path.with_name(path.name + BACKUP_SUFFIX)


def read_backup(path):
    backup = backup_path(path)
    if backup.is_symlink() or backup.resolve() != backup.absolute():
        raise PatchError("Refusing a backup link or junction alias.")
    if not backup.is_file():
        raise PatchError(f"Verified original backup is required: {backup}")
    data = backup.read_bytes()
    if digest(data) != ORIGINAL_SHA256:
        raise PatchError(f"Backup hash mismatch; refusing to overwrite it or the DLL: {backup}")
    return data


def ensure_client_closed(path):
    """Check running Aion images from this DLL's directory on Windows."""
    if os.name != "nt":
        return
    from ctypes import wintypes

    result = subprocess.run(
        [str(Path(os.environ["SystemRoot"]) / "System32" / "tasklist.exe"),
         "/FO", "CSV", "/NH"],
        check=True, capture_output=True, text=True, errors="replace",
        creationflags=subprocess.CREATE_NO_WINDOW,
    )
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.QueryFullProcessImageNameW.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    for row in csv.reader(io.StringIO(result.stdout)):
        if len(row) < 2 or row[0].casefold() not in ("aion.bin", "aion.exe"):
            continue
        handle = kernel.OpenProcess(0x1000, False, int(row[1]))
        if not handle:
            raise PatchError("Cannot inspect a running Aion process. Close all Aion clients first.")
        try:
            name = ctypes.create_unicode_buffer(32768)
            length = wintypes.DWORD(len(name))
            if not kernel.QueryFullProcessImageNameW(handle, 0, name, ctypes.byref(length)):
                raise PatchError("Cannot locate a running Aion process. Close all Aion clients first.")
            if Path(name.value).resolve().parent == path.parent:
                raise PatchError(f"Close the Aion client using {path.parent} before changing its DLL.")
        finally:
            kernel.CloseHandle(handle)


def ensure_backup(path, original):
    backup = backup_path(path)
    if backup.exists() or backup.is_symlink():
        read_backup(path)
        return
    # Exclusive creation: never overwrite an existing backup, even after a race.
    with backup.open("xb") as stream:
        stream.write(original)
        stream.flush()
        os.fsync(stream.fileno())
    read_backup(path)


def replace_file(path, staged):
    if os.name == "nt":
        from ctypes import wintypes

        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        replace = kernel.ReplaceFileW
        replace.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.LPCWSTR,
                            wintypes.DWORD, ctypes.c_void_p, ctypes.c_void_p]
        replace.restype = wintypes.BOOL
        # No IGNORE_ACL_ERRORS / IGNORE_MERGE_ERRORS: retain target permissions.
        if not replace(str(path), str(staged), None, 0, None, None):
            raise ctypes.WinError(ctypes.get_last_error())
    else:
        os.replace(staged, path)


def commit(path, before, after):
    fd, name = tempfile.mkstemp(prefix=f".{path.name}.xp-patch-", suffix=".tmp", dir=path.parent)
    staged = Path(name)
    try:
        with os.fdopen(fd, "wb") as stream:
            stream.write(after)
            stream.flush()
            os.fsync(stream.fileno())
        shutil.copystat(path, staged)
        if staged.read_bytes() != after:
            raise PatchError("Staged DLL verification failed; target not replaced.")
        ensure_client_closed(path)
        checked_path(path)
        if path.read_bytes() != before:
            raise PatchError("DLL changed during preparation; target not replaced.")
        replace_file(path, staged)
        if path.read_bytes() != after:
            raise PatchError(f"Post-write verification failed. Retain the original backup: {backup_path(path)}")
    finally:
        if staged.exists():
            staged.unlink()


def inspect(path):
    data = path.read_bytes()
    state, offset = identify(data)
    backup = backup_path(path)
    backup_state = "missing"
    if backup.exists() or backup.is_symlink():
        read_backup(path)
        backup_state = "verified original"
    return {
        "dll": str(path), "state": state, "sha256": digest(data),
        "patch_rva": hex(PATCH_RVA), "file_offset": hex(offset),
        "original_bytes": ORIGINAL_BYTES.hex(" "), "patched_bytes": PATCHED_BYTES.hex(" "),
        "original_sha256": ORIGINAL_SHA256, "patched_sha256": PATCHED_SHA256,
        "backup": str(backup), "backup_state": backup_state,
        "effect": "Prevent XP address-check rejection from setting crash flag bit 0.",
        "runtime_validation": "Not yet tested in gameplay.",
    }


def apply(path):
    before = path.read_bytes()
    state, offset = identify(before)
    if state == "patched":
        read_backup(path)
        return {"result": "already patched; no files changed", **inspect(path)}
    ensure_client_closed(path)
    after = before[:offset] + PATCHED_BYTES + before[offset + len(ORIGINAL_BYTES):]
    if digest(after) != PATCHED_SHA256:
        raise PatchError("Proposed output hash is not the reviewed patched build.")
    ensure_backup(path, before)
    commit(path, before, after)
    return {"result": "patched; restart the client before testing", **inspect(path)}


def restore(path):
    before = path.read_bytes()
    state, _ = identify(before)
    if state == "original":
        return {"result": "already original; no files changed", **inspect(path)}
    original = read_backup(path)
    ensure_client_closed(path)
    commit(path, before, original)
    return {"result": "restored original; backup retained", **inspect(path)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("inspect", "apply", "restore"))
    parser.add_argument("dll", type=Path, help="Explicit path to bin64/game.dll (or a review copy)")
    args = parser.parse_args(argv)
    try:
        path = checked_path(args.dll)
        result = {"inspect": inspect, "apply": apply, "restore": restore}[args.command](path)
        print(json.dumps(result, indent=2))
        return 0
    except (PatchError, OSError, subprocess.SubprocessError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())

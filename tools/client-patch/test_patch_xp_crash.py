"""Patcher contracts; optional real DLL test only changes temporary copies.

python -m unittest discover -s tools/client-patch -v
Set AION_CLIENT_PATCH_TEST_DLL to enable the real-build round trip.
"""

import contextlib
import io
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

import patch_xp_crash as patch


def fixture():
    """A small PE whose RVA differs from its raw offset, unlike the real DLL."""
    data = bytearray(0x1200)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\0\0"
    struct.pack_into("<HH", data, 0x84, 0x8664, 1)
    struct.pack_into("<H", data, 0x94, 0xF0)
    struct.pack_into("<H", data, 0x98, 0x20B)
    struct.pack_into("<Q", data, 0xB0, patch.IMAGE_BASE)
    section = 0x188
    data[section:section + 8] = b".text\0\0\0"
    struct.pack_into("<IIII", data, section + 8, 0x1000, 0x2CB000, 0x1000, 0x200)
    struct.pack_into("<I", data, section + 36, 0x60000020)
    offset = 0x8EA
    data[offset:offset + 7] = patch.ORIGINAL_BYTES
    return bytes(data), offset


class PatcherTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "game.dll"
        self.original, self.offset = fixture()
        self.patched = (self.original[:self.offset] + patch.PATCHED_BYTES
                        + self.original[self.offset + 7:])
        self.path.write_bytes(self.original)
        self.hashes = mock.patch.multiple(
            patch, ORIGINAL_SHA256=patch.digest(self.original), PATCHED_SHA256=patch.digest(self.patched))
        self.hashes.start()
        self.addCleanup(self.hashes.stop)
        closed = mock.patch.object(patch, "ensure_client_closed")
        self.closed = closed.start()
        self.addCleanup(closed.stop)

    def test_inspect_is_read_only_and_maps_rva(self):
        before = self.path.stat()
        result = patch.inspect(self.path)
        self.assertEqual(result["state"], "original")
        self.assertEqual(result["file_offset"], hex(self.offset))
        self.assertEqual(result["backup_state"], "missing")
        self.assertEqual(self.path.stat().st_mtime_ns, before.st_mtime_ns)
        self.assertEqual(list(self.path.parent.iterdir()), [self.path])
        self.assertEqual(self.path.read_bytes(), self.original)
        self.closed.assert_not_called()

    def test_apply_changes_only_instruction_and_is_idempotent(self):
        patch.apply(self.path)
        self.assertEqual(self.path.read_bytes(), self.patched)
        differences = [i for i, pair in enumerate(zip(self.original, self.path.read_bytes()))
                       if pair[0] != pair[1]]
        self.assertEqual(differences, list(range(self.offset, self.offset + 7)))
        backup = patch.backup_path(self.path)
        self.assertEqual(backup.read_bytes(), self.original)
        timestamp = backup.stat().st_mtime_ns
        self.assertIn("already patched", patch.apply(self.path)["result"])
        self.assertEqual(backup.stat().st_mtime_ns, timestamp)

    def test_restore_exact_original_and_reapply(self):
        patch.apply(self.path)
        patch.restore(self.path)
        self.assertEqual(self.path.read_bytes(), self.original)
        self.assertEqual(patch.read_backup(self.path), self.original)
        self.assertIn("already original", patch.restore(self.path)["result"])
        patch.apply(self.path)
        self.assertEqual(self.path.read_bytes(), self.patched)

    def test_existing_valid_backup_is_reused(self):
        patch.backup_path(self.path).write_bytes(self.original)
        patch.apply(self.path)
        self.assertEqual(patch.read_backup(self.path), self.original)

    def test_unknown_dll_is_never_changed_even_with_valid_backup(self):
        patch.backup_path(self.path).write_bytes(self.original)
        self.path.write_bytes(self.original + b"unknown modification")
        for operation in (patch.inspect, patch.apply, patch.restore):
            with self.subTest(operation=operation.__name__), self.assertRaisesRegex(patch.PatchError, "Unknown DLL"):
                operation(self.path)
        self.assertEqual(self.path.read_bytes(), self.original + b"unknown modification")

    def test_corrupt_backup_blocks_apply_and_restore(self):
        backup = patch.backup_path(self.path)
        backup.write_bytes(b"bad backup")
        with self.assertRaisesRegex(patch.PatchError, "Backup hash mismatch"):
            patch.apply(self.path)
        self.assertEqual(self.path.read_bytes(), self.original)
        self.path.write_bytes(self.patched)
        with self.assertRaisesRegex(patch.PatchError, "Backup hash mismatch"):
            patch.restore(self.path)
        self.assertEqual(self.path.read_bytes(), self.patched)
        self.assertEqual(backup.read_bytes(), b"bad backup")

    def test_patched_without_backup_refuses_apply_and_restore(self):
        self.path.write_bytes(self.patched)
        for operation in (patch.apply, patch.restore):
            with self.subTest(operation=operation.__name__), self.assertRaisesRegex(patch.PatchError, "backup is required"):
                operation(self.path)
        self.assertEqual(self.path.read_bytes(), self.patched)

    def test_running_client_blocks_before_backup_creation(self):
        self.closed.side_effect = patch.PatchError("Close the Aion client")
        with self.assertRaisesRegex(patch.PatchError, "Close the Aion client"):
            patch.apply(self.path)
        self.assertEqual(list(self.path.parent.iterdir()), [self.path])
        self.assertEqual(self.path.read_bytes(), self.original)

    def test_failed_replace_keeps_verified_backup_and_original(self):
        with mock.patch.object(patch, "replace_file", side_effect=PermissionError("locked")):
            with self.assertRaises(PermissionError):
                patch.apply(self.path)
        self.assertEqual(self.path.read_bytes(), self.original)
        self.assertEqual(patch.read_backup(self.path), self.original)
        self.assertFalse(list(self.path.parent.glob("*.tmp")))

    def test_changed_target_during_staging_is_not_overwritten(self):
        def change_target(*_):
            self.path.write_bytes(self.original + b"external update")
        with mock.patch.object(patch.shutil, "copystat", side_effect=change_target):
            with self.assertRaisesRegex(patch.PatchError, "DLL changed during preparation"):
                patch.apply(self.path)
        self.assertEqual(self.path.read_bytes(), self.original + b"external update")
        self.assertEqual(patch.read_backup(self.path), self.original)
        self.assertFalse(list(self.path.parent.glob("*.tmp")))

    def test_malformed_pe_and_non_executable_section_are_refused(self):
        with self.assertRaisesRegex(patch.PatchError, "Invalid PE"):
            patch.raw_offset(b"MZ")
        data = bytearray(self.original)
        struct.pack_into("<I", data, 0x188 + 36, 0x40000040)
        with self.assertRaisesRegex(patch.PatchError, "executable code"):
            patch.raw_offset(data)

    def test_matching_hash_does_not_bypass_instruction_check(self):
        data = self.original[:self.offset] + b"\xcc" * 7 + self.original[self.offset + 7:]
        with mock.patch.object(patch, "ORIGINAL_SHA256", patch.digest(data)):
            with self.assertRaisesRegex(patch.PatchError, "Instruction bytes"):
                patch.identify(data)

    def test_no_arguments_cannot_modify_files(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            patch.main([])
        self.assertEqual(error.exception.code, 2)
        self.assertEqual(list(self.path.parent.iterdir()), [self.path])

    def test_cli_errors_are_nonzero_and_do_not_write(self):
        self.path.write_bytes(b"unknown")
        with contextlib.redirect_stderr(io.StringIO()) as stderr:
            self.assertEqual(patch.main(["apply", str(self.path)]), 1)
        self.assertIn("Unknown DLL", stderr.getvalue())
        self.assertEqual(self.path.read_bytes(), b"unknown")


@unittest.skipUnless(os.environ.get("AION_CLIENT_PATCH_TEST_DLL"), "set AION_CLIENT_PATCH_TEST_DLL for a real-build copy test")
class RealBuildTests(unittest.TestCase):
    def test_real_build_cli_round_trip_on_temporary_copy(self):
        source = Path(os.environ["AION_CLIENT_PATCH_TEST_DLL"])
        original = source.read_bytes()
        self.assertEqual(patch.digest(original), patch.ORIGINAL_SHA256)
        source_mtime = source.stat().st_mtime_ns
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "game.dll"
            target.write_bytes(original)

            def invoke(command):
                result = subprocess.run([sys.executable, str(Path(patch.__file__).resolve()),
                                         command, str(target)], capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stderr)
                return json.loads(result.stdout)

            self.assertEqual(invoke("inspect")["state"], "original")
            self.assertEqual(list(target.parent.iterdir()), [target])
            self.assertEqual(invoke("apply")["state"], "patched")
            changed = target.read_bytes()
            self.assertEqual(patch.digest(changed), patch.PATCHED_SHA256)
            offset = patch.raw_offset(original)
            self.assertEqual(changed[:offset], original[:offset])
            self.assertEqual(changed[offset:offset + 7], patch.PATCHED_BYTES)
            self.assertEqual(changed[offset + 7:], original[offset + 7:])
            self.assertEqual(patch.read_backup(target), original)
            self.assertIn("already patched", invoke("apply")["result"])
            self.assertEqual(invoke("restore")["state"], "original")
            self.assertEqual(target.read_bytes(), original)
            self.assertEqual(patch.read_backup(target), original)
            self.assertIn("already original", invoke("restore")["result"])
        self.assertEqual(source.read_bytes(), original)
        self.assertEqual(source.stat().st_mtime_ns, source_mtime)
        self.assertFalse(patch.backup_path(source).exists())


if __name__ == "__main__":
    unittest.main()

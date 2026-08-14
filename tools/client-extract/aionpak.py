"""Read Aion client .pak archives.

A .pak is a standard ZIP with two obfuscations:

1. The three 4-byte PK record signatures are XOR'd with 0xFF:
       local file header   50 4B 03 04  ->  AF B4 FC FB
       central directory   50 4B 01 02  ->  AF B4 FE FD
       end of central dir  50 4B 05 06  ->  AF B4 FA F9
   Every other header field is plaintext. Compression is raw deflate
   (method 8) or store (method 0); data descriptors are never used.

2. Only the FIRST 32 BYTES of each entry's compressed payload are XOR'd
   against a fixed key table, at an offset derived from the compressed size.
   Everything past byte 32 is untouched deflate -- which is why a naive
   inflate fails immediately with "invalid code lengths set".

       v2 (retail, closed beta onward):  table[(csize & 0x3FF) + i], 1056-byte table
       v1 (2008 open beta):              table[(csize & 0x1F) * 32 + i], 1024-byte table

The version is detected once per archive by trial-decrypting the first entry
with each table and checking its CRC32. Some archives (third-party repacks)
hold plain, unobfuscated ZIP entries; those are detected per entry from the
signature and passed through untouched.

Every entry is CRC32-verified on read, so a wrong table fails loudly.

CLI:
    python aionpak.py <file.pak> --list
    python aionpak.py <file.pak> <out_dir>
"""
from __future__ import annotations

import argparse
import pathlib
import struct
import zlib
from typing import Iterator, NamedTuple

KEYS = pathlib.Path(__file__).parent / "keys"
TABLE_V1 = (KEYS / "pak_table_v1.bin").read_bytes()
TABLE_V2 = (KEYS / "pak_table_v2.bin").read_bytes()

LFH_OBF, LFH = b"\xaf\xb4\xfc\xfb", b"PK\x03\x04"
CD_OBF, CD = b"\xaf\xb4\xfe\xfd", b"PK\x01\x02"
EOCD_OBF, EOCD = b"\xaf\xb4\xfa\xf9", b"PK\x05\x06"

HEADER_XOR_LEN = 32


class PakError(Exception):
    """Raised when an archive cannot be parsed or verified."""


class Entry(NamedTuple):
    name: str
    data: bytes


def _decrypt(payload: bytes, version: int) -> bytes:
    """Undo the header XOR on the first 32 bytes of a compressed payload."""
    if version == 2:
        table, offset = TABLE_V2, len(payload) & 0x3FF
    else:
        table, offset = TABLE_V1, (len(payload) & 0x1F) * 32
    out = bytearray(payload)
    for i in range(min(HEADER_XOR_LEN, len(payload))):
        out[i] ^= table[offset + i]
    return bytes(out)


def _inflate(payload: bytes, method: int) -> bytes:
    return zlib.decompress(payload, -15) if method == 8 else payload


class _Record(NamedTuple):
    name: str
    method: int
    crc: int
    usize: int
    payload: bytes
    obfuscated: bool


def _records(raw: bytes) -> Iterator[_Record]:
    """Walk the central directory, yielding raw (still-encrypted) records."""
    pos = raw.rfind(EOCD_OBF)
    if pos < 0:
        pos = raw.rfind(EOCD)
    if pos < 0:
        raise PakError("no end-of-central-directory record")

    count, _cd_size, cd_off = struct.unpack_from("<HII", raw, pos + 10)

    p = cd_off
    for _ in range(count):
        if raw[p:p + 4] not in (CD_OBF, CD):
            raise PakError(f"bad central-directory signature at {p:#x}")
        method, = struct.unpack_from("<H", raw, p + 10)
        crc, csize, usize = struct.unpack_from("<III", raw, p + 16)
        fn_len, extra_len, cmt_len = struct.unpack_from("<HHH", raw, p + 28)
        lfh_off, = struct.unpack_from("<I", raw, p + 42)
        name = raw[p + 46:p + 46 + fn_len].decode("utf-8", "replace")
        p += 46 + fn_len + extra_len + cmt_len

        # The local header carries its own name/extra lengths, which may
        # differ from the central directory's.
        sig = raw[lfh_off:lfh_off + 4]
        if sig not in (LFH_OBF, LFH):
            raise PakError(f"bad local header signature for {name!r}")
        lfn, lextra = struct.unpack_from("<HH", raw, lfh_off + 26)
        start = lfh_off + 30 + lfn + lextra
        yield _Record(name, method, crc, usize,
                      raw[start:start + csize], sig == LFH_OBF)


def _detect_version(records: list[_Record]) -> int:
    """Trial-decrypt the first obfuscated entry with each table."""
    for rec in records:
        if not rec.obfuscated:
            continue
        for version in (2, 1):
            try:
                data = _inflate(_decrypt(rec.payload, version), rec.method)
            except zlib.error:
                continue
            if len(data) == rec.usize and zlib.crc32(data) == rec.crc:
                return version
        raise PakError(f"payload of {rec.name!r} matches no known key table")
    return 2  # archive is entirely plain; version is irrelevant


def read_pak(path: str | pathlib.Path) -> Iterator[Entry]:
    """Yield every (name, decompressed data) pair, CRC32-verified."""
    raw = pathlib.Path(path).read_bytes()
    records = list(_records(raw))
    version = _detect_version(records)

    for rec in records:
        payload = _decrypt(rec.payload, version) if rec.obfuscated else rec.payload
        try:
            data = _inflate(payload, rec.method)
        except zlib.error as exc:
            raise PakError(f"{rec.name!r}: inflate failed ({exc})") from exc
        if len(data) != rec.usize or zlib.crc32(data) != rec.crc:
            raise PakError(f"{rec.name!r}: CRC/size mismatch")
        yield Entry(rec.name, data)


def main() -> None:
    ap = argparse.ArgumentParser(description="Extract an Aion client .pak archive.")
    ap.add_argument("pak")
    ap.add_argument("out_dir", nargs="?", help="omit with --list")
    ap.add_argument("--list", action="store_true", help="list entries without writing")
    args = ap.parse_args()

    if not args.list and not args.out_dir:
        ap.error("out_dir is required unless --list is given")

    out = pathlib.Path(args.out_dir) if args.out_dir else None
    count = 0
    for name, data in read_pak(args.pak):
        count += 1
        print(f"  {name}  ({len(data):,} bytes)")
        if out is not None:
            dest = out / name
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(data)
    tail = "" if out is None else f" -> {out}"
    print(f"{count} entries, all CRC-verified{tail}")


if __name__ == "__main__":
    main()

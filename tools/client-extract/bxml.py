"""Decode CryEngine-derived binary XML as shipped in Aion client .pak archives.

Most XML files inside a .pak are not text -- they begin with magic byte 0x80
and use a string-table encoding:

    u8      0x80
    varint  string-table size in BYTES
    bytes   string table: UTF-16LE, NUL-terminated strings. A string INDEX is a
            CHARACTER offset, so the byte offset is index * 2; index 0 is "".
    node:
      varint  name string index
      u8      flags
        bit0 (1): varint value index                    -> element text
        bit1 (2): varint count, then count * (key index, value index)
        bit2 (4): varint count, then count * <node>

`varint` is LEB128: 7 bits per byte, high bit signals continuation.

Not every archive member is binary XML (e.g. .txt files are plain), so callers
should branch on the magic -- `is_binary_xml` does that check.

CLI:
    python bxml.py <file> [out.xml]
"""
from __future__ import annotations

import argparse
import pathlib
import xml.etree.ElementTree as ET

MAGIC = 0x80


class BinaryXmlError(Exception):
    """Raised when a buffer is not decodable binary XML."""


def is_binary_xml(buf: bytes) -> bool:
    return bool(buf) and buf[0] == MAGIC


class _Reader:
    __slots__ = ("b", "p")

    def __init__(self, buf: bytes) -> None:
        self.b = buf
        self.p = 0

    def u8(self) -> int:
        value = self.b[self.p]
        self.p += 1
        return value

    def varint(self) -> int:
        value = shift = 0
        while True:
            byte = self.b[self.p]
            self.p += 1
            value |= (byte & 0x7F) << shift
            if not byte & 0x80:
                return value
            shift += 7


def decode(buf: bytes) -> ET.Element:
    """Decode a binary XML buffer into an ElementTree element."""
    if not is_binary_xml(buf):
        raise BinaryXmlError("missing 0x80 magic byte")

    r = _Reader(buf)
    r.u8()
    table_size = r.varint()
    table = r.b[r.p:r.p + table_size]
    r.p += table_size

    cache: dict[int, str] = {}

    def string(index: int) -> str:
        cached = cache.get(index)
        if cached is not None:
            return cached
        start = end = index * 2
        while end + 1 < len(table) and table[end:end + 2] != b"\x00\x00":
            end += 2
        value = table[start:end].decode("utf-16-le", "replace")
        cache[index] = value
        return value

    def node() -> ET.Element:
        el = ET.Element(string(r.varint()))
        flags = r.u8()
        if flags & 1:
            el.text = string(r.varint())
        if flags & 2:
            for _ in range(r.varint()):
                key = string(r.varint())
                el.set(key, string(r.varint()))
        if flags & 4:
            for _ in range(r.varint()):
                el.append(node())
        return el

    return node()


def decode_file(path: str | pathlib.Path) -> ET.Element:
    return decode(pathlib.Path(path).read_bytes())


def main() -> None:
    ap = argparse.ArgumentParser(description="Decode Aion binary XML to text XML.")
    ap.add_argument("path")
    ap.add_argument("out", nargs="?", help="write decoded XML here instead of summarizing")
    args = ap.parse_args()

    root = decode_file(args.path)
    if args.out:
        ET.ElementTree(root).write(args.out, encoding="utf-8", xml_declaration=True)
        print(f"<{root.tag}> with {len(root)} children -> {args.out}")
    else:
        print(f"<{root.tag}> with {len(root)} children")


if __name__ == "__main__":
    main()

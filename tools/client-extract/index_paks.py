"""Index every entry name in every .pak under a client install.

Reads only each archive's end-of-central-directory record and central
directory, so it never streams the ~35 GB of payload data. Useful for
answering "which pak holds <file>?" without extracting anything.

CLI:
    python index_paks.py "C:/Program Files (x86)/Beyond Aion" pak_index.tsv
"""
from __future__ import annotations

import argparse
import pathlib
import struct
from typing import Iterator

from aionpak import CD, CD_OBF, EOCD, EOCD_OBF, PakError

MAX_EOCD_TAIL = 65_557  # 22-byte EOCD + up to a 64 KiB archive comment


def entry_names(path: pathlib.Path) -> Iterator[str]:
    """Yield entry names from an archive, reading only its directory."""
    size = path.stat().st_size
    with path.open("rb") as fh:
        tail_len = min(size, MAX_EOCD_TAIL)
        fh.seek(size - tail_len)
        tail = fh.read(tail_len)
        pos = max(tail.rfind(EOCD_OBF), tail.rfind(EOCD))
        if pos < 0:
            raise PakError("no end-of-central-directory record")
        count, cd_size, cd_off = struct.unpack_from("<HII", tail, pos + 10)
        fh.seek(cd_off)
        cd = fh.read(cd_size)

    p = 0
    for _ in range(count):
        if cd[p:p + 4] not in (CD_OBF, CD):
            raise PakError(f"bad central-directory signature at {p:#x}")
        fn_len, extra_len, cmt_len = struct.unpack_from("<HHH", cd, p + 28)
        yield cd[p + 46:p + 46 + fn_len].decode("utf-8", "replace")
        p += 46 + fn_len + extra_len + cmt_len


def main() -> None:
    ap = argparse.ArgumentParser(description="Index all .pak entry names under a client root.")
    ap.add_argument("client_root")
    ap.add_argument("out_tsv")
    args = ap.parse_args()

    root = pathlib.Path(args.client_root)
    paks = sorted(root.rglob("*.pak"))
    total = failed = 0

    with open(args.out_tsv, "w", encoding="utf-8") as out:
        for pak in paks:
            rel = pak.relative_to(root).as_posix()
            try:
                for name in entry_names(pak):
                    out.write(f"{rel}\t{name}\n")
                    total += 1
            except (PakError, OSError, struct.error) as exc:
                failed += 1
                print(f"SKIP {rel}: {type(exc).__name__} {exc}")

    print(f"{len(paks)} archives ({failed} unreadable), {total:,} entries -> {args.out_tsv}")


if __name__ == "__main__":
    main()

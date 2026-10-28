# Reversible XP crash client patcher

Status: patcher verified on temporary DLL copies; **gameplay validation pending**.
Building and testing this tool does not patch the installed client. No DLL is
included in the repository. Python 3.10+ with the standard library is sufficient.

## Review first (read only)

From the repository root:

```powershell
python tools/client-patch/patch_xp_crash.py inspect "C:\Program Files (x86)\Beyond Aion\bin64\game.dll"
```

`inspect` prints the DLL state, both expected hashes, the file offset, the exact
before/after bytes, and the backup status. It writes no files. The DLL path and
command are always explicit; there is no default installation target or force mode.

## Exact change

| Property | Value |
|---|---|
| Supported client | Investigated Aion 4.8 AMD64 `bin64/game.dll` only |
| Original SHA-256 | `d8645dadfb91e1c2af08a6c9ae313e3d5e07e59ed9b1d004dddcb7b516047296` |
| Patched SHA-256 | `0b103f3bdfa9a83b2a733f1b734ccf9ec35f444360f0757dd9375bed4ecad0ff` |
| Preferred image base | `0x10000000` |
| Instruction RVA | `0x2CB6EA` (preferred VA `0x102CB6EA`) |
| Raw file offset in this build | `0x2CB6EA`, independently mapped through the PE sections |
| Original bytes | `83 0D FB C6 05 01 01` |
| Original instruction | `OR dword ptr [RIP + 0x0105C6FB], 1` (flag at preferred VA `0x11327DEC`) |
| Replacement bytes | `90 90 90 90 90 90 90` (seven single-byte NOPs) |

Only the XP callback's flag-setting instruction changes. It executes when the
rare address check rejects the selected game-server address. XP assignments,
UI updates, RNG advancement, address checking and player construction remain in
place. The following instruction is `ADD RSP, 0x28`, then `RET`; flags produced
by the removed OR are not consumed by a following conditional branch.

The independent zero-object-ID check and the null-write instruction are left
intact. This patch prevents the verified XP path from arming the flag; it does
not clear a flag in an already-running client or claim to fix unrelated crashes.

The original controlled reproduction and instruction listings are in the local
evidence folder `run/client-xp-live-20261019/`, particularly `findings.txt` and
`claim_reconstruction.c`. The XP handler assembly is in
`run/client-xp-20261019/pass1.log` (preferred VA `0x102CB6EA`). These evidence
folders are ignored by Git; the patcher's constants do not depend on them.
This is a native client correction with no corresponding Java server method;
server source and protocol behavior are unchanged.

## Apply later, after review

Close the client first. These are manual commands for a later authorized apply:

```powershell
python tools/client-patch/patch_xp_crash.py apply "C:\Program Files (x86)\Beyond Aion\bin64\game.dll"
python tools/client-patch/patch_xp_crash.py inspect "C:\Program Files (x86)\Beyond Aion\bin64\game.dll"
```

The patcher verifies the full original hash, PE layout, instruction bytes and
proposed output hash. Before replacing the DLL, it creates and verifies:

```text
bin64/game.dll.xp-address-check.original.bak
```

Keep this backup. It is created exclusively, never overwritten, and must match
the original full DLL hash. A repeated apply reports "already patched" only
when the exact patched DLL and verified backup are both present. A bad existing
backup or unknown DLL causes refusal. After replacement, the complete output
is read back and verified. Existing install permissions may require running the
manual apply/restore command in an elevated terminal; the tool never elevates itself.

Writes are staged and flushed in the same directory, then replace the complete
DLL. On Windows the tool uses
[`ReplaceFileW`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)
without flags that ignore ACL/metadata merge errors. It checks for running Aion
processes in the target directory before preparation and again before replacement.
Keep the client and updater closed throughout the operation. If replacement
fails, retain the backup and inspect the DLL before retrying; no client is
launched or modified in memory. An updater or repair can replace the patched DLL,
so inspect again after a client update. Unknown builds require separate analysis.

## Restore

Close the client, then run:

```powershell
python tools/client-patch/patch_xp_crash.py restore "C:\Program Files (x86)\Beyond Aion\bin64\game.dll"
```

Restore accepts the exact patched build and a verified original backup, replaces
the DLL with the backup contents, and verifies the complete original hash. The
backup is retained. Repeated restore of the exact original is a no-op. An unknown
or externally modified DLL is never overwritten, even if a valid backup exists.

## Test without applying to the installed client

```powershell
python -m unittest discover -s tools/client-patch -v

# Optional: read the real installed DLL, but mutate ONLY a temporary copy.
$env:AION_CLIENT_PATCH_TEST_DLL = 'C:\Program Files (x86)\Beyond Aion\bin64\game.dll'
python -m unittest discover -s tools/client-patch -v
Remove-Item Env:AION_CLIENT_PATCH_TEST_DLL
```

Tests cover inspection without writes, PE RVA mapping, exact patch scope,
backup preservation, apply/restore idempotence, missing/corrupt backups,
unknown input, running-client refusal, failed replacement and changed-target
refusal. The optional integration test runs actual CLI inspect/apply/restore on
a temporary copy and checks the installed source hash and modification time.

After approval and application, runtime validation still needs a fresh client
start, ordinary XP awards, a controlled replay that exercises rejection, player
and companion visibility, reconnects and leveling. Also test without instrumentation
to confirm the protected client accepts the disk modification. No gameplay success
is implied by passing the file-level tests.

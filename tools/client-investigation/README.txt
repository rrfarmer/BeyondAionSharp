CLIENT INVESTIGATION TOOLS - configured 2026-10-19

READY
- Published REA 6.7.0 installed globally (rea-agents), verified current on npm.
  The local ../rea source checkout was fast-forwarded 53 commits to origin/main
  (833136507dd79d959340b09862a53af344e4f1b3) on 2026-10-19.
  All five dependency repositories fetched successfully. Four already match
  REA's pinned commits; unblob's pre-existing local changes were preserved.
- Codex registration: C:\Users\ryanf\.codex\config.toml, MCP server name rea.
  Direct Node launch, Ghidra provider, explicit existing Ghidra/JDK paths.
  Startup timeout 30 seconds; tool timeout 450 seconds.
- Previous Codex configuration: config.toml.rea.backup beside config.toml.
  Parsed comparison verified that only the REA registration was added.
- Matching REA skill: C:\Users\ryanf\.agents\skills\reverse-engineer-anything.
- Ghidra 12.1.4 and full JDK 21.0.12.1 already existed and were not reinstalled.
- Frida 17.23.1 and Python are already available for explicitly scoped live work.

ACTIVATE IN CODEX
Restart Codex or reconnect its MCP servers, then check that REA tools appear in
the new session. Installing configuration cannot add tools to an already-running
chat. A successful external health probe does not prove the active chat connected.
REA's actual stdio MCP was initialized and verified: 207 tools, 6 prompts, and
a successful binary_session request with the expected package/catalog identity.

CLI WITHOUT RESTARTING CODEX
From the server repository root, the helper borrows the existing Codex registration
and environment for that one invocation, restoring the shell environment afterward:

  powershell -NoProfile -ExecutionPolicy Bypass -File tools/client-investigation/Invoke-Rea.ps1 --version
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/client-investigation/Invoke-Rea.ps1 doctor --client codex --json
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/client-investigation/Invoke-Rea.ps1 decompile "C:\path\sample.dll" 0xADDRESS --provider ghidra --format json

Addresses and paths are explicit. Never paste the placeholder 0xADDRESS unchanged.
MCP native workflow: open_binary with an explicit path and provider_id=ghidra,
then use focused function, instruction and reference tools advertised by that
server. Use inspect_managed_artifact for a copied C# server DLL.

VERIFIED SCOPE AND LIMITATIONS
REA's Windows native admission, Java, Ghidra and configuration checks passed.
An actual native decompilation of C:\Windows\System32\where.exe entry
0x140001300 succeeded through Ghidra. A copied live Aion.GameServer.dll was
successfully inspected through REA's separate managed static provider.
No client process was attached, launched or changed during this setup.
This does not establish a complete REA decompilation of the protected game.dll.

Windows Ghidra uses an immutable snapshot and a temporary analysis project;
it does not reuse or change the existing aion48 Ghidra database. Preserve that
database and the previous focused Ghidra scripts for client-specific section
and protection work. Protected stubs and inferred no-return boundaries still
need evidence checks. Windows P0 no-return repair is disabled in this provider.
Not all 207 REA tools support Windows. For example, inspect_binary_layout is a
Linux ELF operation and rejects this host; the native Ghidra workflow is the
correct route for PE decompilation. Detailed inspect_packet_capture is Linux-only;
the separate pcap summary family supports Windows when Wireshark tools are supplied.

PACKET LOGGING
The Aion server already records decrypted client payloads and clear server frames,
with timestamps, opcodes, parsed fields and character state. Prefer that evidence
for gameplay/packet bugs because it avoids injecting the protected client.
Current live Docker settings: AION_RECORD=false; output /app/recordings is already
mounted to this checkout's run/recordings. The live server was not recreated.

At the next deliberate recording/deployment session, set:
  AION_RECORD=true
  AION_RECORD_ACCOUNTS=rrfarmer
  AION_RECORD_DIR=/app/recordings

Use explicit account names; an empty account filter records all accounts. These
are startup settings, so changing the shell alone does not enable an existing
container. A server recreation/restart is needed; coordinate it with active play
and the intended image version. No such restart was performed during this setup.
See docs/session-recording.md for the existing deployment instructions.

Read recordings with the existing tool:
  python tools/recording/recording.py check <recording.jsonl>
  python tools/recording/recording.py summary <recording.jsonl>
  python tools/recording/recording.py timeline <recording.jsonl> --limit 100
  python tools/recording/recording.py path <recording.jsonl> --out <path.json>

The reader validated an existing 10,739-entry recording with zero structural
problems. Recordings contain session tokens and hardware identifiers; keep raw
captures private and remove those fields before sharing evidence publicly.

NEXT TOOLS TO CONSIDER (NOT INSTALLED OR ENABLED BY THIS SETUP)
- WinDbg and per-application Windows Error Reporting LocalDumps for AION.bin:
  inspect a saved crash and its stack without relying on live hook detachment.
  https://learn.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps
- Wireshark/TShark (and capinfos for REA's Windows pcap summaries): TCP timing,
  disconnects, retransmissions and saved captures. Raw TCP captures do not by
  themselves decode this game's encrypted application protocol. Keep server
  recordings for the decrypted gameplay payloads.
  https://www.wireshark.org/docs/man-pages/tshark.html
- Continue Frida only for scoped live questions static evidence cannot answer.
  The earlier XP test had an ntdll crash at observer detachment. Keep controlled
  replays distinct from real packets and always include a fresh, unhooked run.

REPEATABLE INVESTIGATION
1. State the gameplay action or packet question and record exact client/Docker
   DLL hashes, build identity, UTC times and reproduction steps.
2. Capture one account's server-side packets and logs around that action.
3. Use Java as the server behavior reference; compare the exact deployed C# build.
4. Read only the relevant client packet parser, its callers and consumers with
   REA/Ghidra. Preserve addresses, file offsets, pseudocode, instruction evidence
   and explicit uncertainty at protected code.
5. Use controlled runtime observation only when needed, followed by ordinary
   gameplay without hooks. Preserve raw evidence and a concise conclusion.

Setup health and smoke-test evidence is in run/tooling-setup (ignored by Git).
Repository update and fetch audit evidence is in run/repo-update-audit.
Official Codex MCP configuration reference:
https://learn.chatgpt.com/docs/extend/mcp?surface=cli

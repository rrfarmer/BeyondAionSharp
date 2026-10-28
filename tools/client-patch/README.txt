BEYOND AION - XP CRASH PATCHER

HOW TO USE
1. Extract the entire ZIP into a folder.
2. Double-click "Start Patcher.bat".
3. Click Browse and select your Aion installation folder.
   You can also select its bin64 folder directly.
4. Read the inspection result. Selecting a folder makes no changes.
5. To apply later: close the game and updater, click Apply patch, and confirm.
   If Windows denies access, click Restart as administrator and inspect again.
6. Start a fresh client after applying.

UNDO
Click Browse, select the same installation, then click Restore original.
The original DLL is restored exactly. The original backup is kept beside it:
  bin64/game.dll.xp-address-check.original.bak
Keep this file. Do not delete it or share a different client's backup.
Restore also works if a failed replacement left game.dll missing.

REQUIREMENTS
Windows 10/11 with built-in Windows PowerShell 5.1 and .NET Framework.
No Python, additional downloads or server access are needed.
Keep all three script files together. The tool never downloads or starts Aion.

WHAT IT CHANGES
Only the investigated 4.8 AMD64 game.dll is accepted. The full DLL hash and PE
layout are checked before writing. Unknown or modified builds are refused.
Exactly seven bytes at RVA/file offset 0x2CB6EA are changed:
  Original: 83 0D FB C6 05 01 01
  Patched:  90 90 90 90 90 90 90
This prevents the XP address check from setting the confirmed crash flag.

Original SHA-256:
d8645dadfb91e1c2af08a6c9ae313e3d5e07e59ed9b1d004dddcb7b516047296
Patched SHA-256:
0b103f3bdfa9a83b2a733f1b734ccf9ec35f444360f0757dd9375bed4ecad0ff

The backup is created exclusively, checked against the original hash, and never
overwritten. Apply and Restore verify the complete DLL after writing. Operations
use a transaction rollback copy; failed operations retain recovery files instead
of deleting them. Never run a client updater during patching. Unknown DLLs are
never overwritten, including during restore.

VALIDATION STATUS
File operations and recovery are tested on disposable copies. Local gameplay
testing on October 19, 2026 passed XP updates and companion visibility with the
patched client. This targets one confirmed crash mechanism; it does not claim
to fix every client crash. A client repair or update can undo the patch, so
inspect again afterward.

FOR MAINTAINERS
Core: Patcher.Core.ps1. Interface: ClientPatcher.ps1. Launcher: Start Patcher.bat.
Maintainer tests and original investigation evidence are in the server checkout,
outside this user package. This is native-client code; there is no Java server
counterpart. The package contains no game DLLs or personal client backups.

# Repository split

On 2026-07-13, the C# subtree was extracted from `C:\Users\ryanf\Documents\GitHub\aion-server` into this repository with history preserved and `dotnetConversion/` promoted to the root.

- Original mixed-repository C# head: `88e304cfc3a4ccd10ffacd08a289e4abc5a4d5e6`
- Filtered C# head before standalone assets: `e93638b4d`
- Java fork target after split: `beyond-aion/aion-server` branch `4.8`
- Shared runtime assets copied: module config and SQL, game static data, geo data, packet data, three handler XML resources, parity fixtures, and C# Docker deployment files.
- Excluded: Java application/handler source, local `my*.properties`, caches, logs, build output, and IDE state.

Historical commits retain their original messages and authors, but their hashes changed when `dotnetConversion/` was promoted to the repository root.


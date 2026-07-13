# BeyondAionSharp

Independent C# implementation of the Beyond Aion 4.8 server emulator, targeting .NET 10. This is a standalone repository with its own history, branches, releases, and issue tracking. The Java project is used only as a behavioral reference for parity and upstream bug fixes.

## Build and test

```powershell
dotnet build AionServer.slnx
dotnet test AionServer.slnx
```

See [RUNNING.md](RUNNING.md) for local startup and [docker/README.md](docker/README.md) for the container stack.

## Java reference

The expected local layout is:

```text
GitHub/
|-- aion-server/       # separate Java reference checkout
`-- BeyondAionSharp/   # this repository
```

This repository has no Java Git remote. `scripts/upstream/list-pending.ps1` reads and updates the separate sibling checkout at `../aion-server`. Current upstream ports are recorded in [docs/upstream-port-log.md](docs/upstream-port-log.md); the process and automation contract are in [docs/upstream-porting.md](docs/upstream-porting.md).

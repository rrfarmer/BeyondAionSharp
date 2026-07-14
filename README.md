# BeyondAionSharp

Independent C# implementation of the Beyond Aion 4.8 server emulator, targeting .NET 10. This is a standalone repository with its own history, branches, releases, and issue tracking. The Java project is used only as a behavioral reference for parity and upstream bug fixes.

## Build and test

```powershell
dotnet build AionServer.slnx
dotnet test AionServer.slnx
```

CI runs a clean rebuild through the warning ratchet, then runs every test project in the solution and the structural fidelity check:

```powershell
& ./scripts/ci/check-warning-baseline.ps1
dotnet test AionServer.slnx --no-build --no-restore
python scripts/parity/check_fidelity.py
```

The checked-in warning baseline is a ceiling, not an accepted end state. New warning codes and per-code or total count increases fail. After fixing existing warnings, review the build and lower the baseline with `& ./scripts/ci/check-warning-baseline.ps1 -UpdateBaseline`; never update it to bless new debt. `CS0184`, `CS0472`, and `CS8605` are kept at zero as build errors.

The solution-wide test command includes the normal static-data, cross-server bridge, and deterministic database-boundary suites. Tests that require a separately running MySQL instance remain explicitly environment-gated.

See [RUNNING.md](RUNNING.md) for local startup and [docker/README.md](docker/README.md) for the container stack.

## Java reference

The expected local layout is:

```text
GitHub/
|-- aion-server/       # separate Java reference checkout
`-- BeyondAionSharp/   # this repository
```

This repository has no Java Git remote. `scripts/upstream/list-pending.ps1` reads and updates the separate sibling checkout at `../aion-server`. Current upstream ports are recorded in [docs/upstream-port-log.md](docs/upstream-port-log.md); the process and automation contract are in [docs/upstream-porting.md](docs/upstream-porting.md).

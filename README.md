# BeyondAionSharp

C# conversion of the Beyond Aion 4.8 server emulator, targeting .NET 10. The Java project remains the behavioral reference, but this repository owns the C# source, tests, runtime data, configuration schemas, SQL, and parity fixtures.

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
|-- aion-server/       # clean Java fork
`-- BeyondAionSharp/   # this repository
```

The `java-upstream` remote tracks `https://github.com/beyond-aion/aion-server.git`. Current upstream ports are recorded in [docs/upstream-port-log.md](docs/upstream-port-log.md); the process and automation contract are in [docs/upstream-porting.md](docs/upstream-porting.md).


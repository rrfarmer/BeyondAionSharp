using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.GeoEngine.Models;

namespace Aion.GameServer.GeoEngine;

/// <summary>
/// Java parity: geoEngine/GeoWorldLoader (loads terrain PNG heightmaps/materials + .geo mesh nodes + collision
/// data for every GeoMap). The full geo/collision engine (Terrain, Mesh, Node tree, DDS/PNG decode, parallel
/// mesh-collision preload) is a deferred engine pillar; this faithful-defer entry point keeps the GeoService
/// load path compiling and is only reached when GeoDataConfig.GEO_ENABLE is set. [[gameplay-faithful-infra-idiomatic]]
/// </summary>
public static class GeoWorldLoader
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(GeoWorldLoader));

    // Java parity: GeoWorldLoader.load(Collection<GeoMap>).
    public static void Load(ICollection<GeoMap> maps)
    {
        // Geo terrain/mesh loading is deferred (engine pillar not yet ported). No terrains/meshes are loaded,
        // matching the "No terrains were loaded" warning branch of the Java loader.
        log.LogWarning("Geo world data loading is not yet implemented; no terrains or meshes were loaded.");
    }
}

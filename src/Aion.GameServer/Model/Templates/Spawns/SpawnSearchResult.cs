namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/SpawnSearchResult.</summary>
public sealed class SpawnSearchResult
{
    public SpawnSearchResult(int worldId, SpawnSpotTemplate spot) { WorldId = worldId; Spot = spot; }
    public SpawnSpotTemplate Spot  { get; }
    public int               WorldId { get; }
    public SpawnSpotTemplate GetSpot()    => Spot;
    public int               GetWorldId() => WorldId;
}

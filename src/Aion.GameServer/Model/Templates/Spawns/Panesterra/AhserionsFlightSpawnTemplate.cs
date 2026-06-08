using Aion.GameServer.Services.Panesterra.Ahserion;

namespace Aion.GameServer.Model.Templates.Spawns.Panesterra;

/// <summary>Java parity: model/templates/spawns/panesterra/AhserionsFlightSpawnTemplate.</summary>
public class AhserionsFlightSpawnTemplate : SpawnTemplate
{
    private int               _stage;
    private PanesterraFaction _faction;

    public AhserionsFlightSpawnTemplate(SpawnGroup spawnGroup, SpawnSpotTemplate spot)
        : base(spawnGroup, spot) { }

    public int               GetStage()   => _stage;
    public PanesterraFaction GetFaction() => _faction;
    public void SetStage(int stage)       => _stage   = stage;
    public void SetPanesterraTeam(PanesterraFaction faction) => _faction = faction;
}

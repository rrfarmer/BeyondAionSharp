using Aion.GameServer.Model;

namespace Aion.GameServer.Custom.Instance;

/// <summary>Java parity: custom/instance/CustomInstanceRankedPlayer.</summary>
public class CustomInstanceRankedPlayer : CustomInstanceRank
{
    private readonly string name;
    private readonly PlayerClass playerClass;

    public CustomInstanceRankedPlayer(int playerId, int rank, long lastEntry, int maxRank, int dps, string name, PlayerClass playerClass)
        : base(playerId, rank, lastEntry, maxRank, dps)
    {
        this.name = name;
        this.playerClass = playerClass;
    }

    public string GetName()
    {
        return name;
    }

    public PlayerClass GetPlayerClass()
    {
        return playerClass;
    }
}

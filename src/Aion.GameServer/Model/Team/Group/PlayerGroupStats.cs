namespace Aion.GameServer.Model.Team.Group;

/// <summary>Java parity: model/team/group/PlayerGroupStats (ATracer).</summary>
public class PlayerGroupStats
{
    private readonly PlayerGroup group;
    private int minExpPlayerLevel;
    private int maxExpPlayerLevel;

    private Aion.GameServer.Model.GameObjects.Players.Player minLevelPlayer;
    private Aion.GameServer.Model.GameObjects.Players.Player maxLevelPlayer;

    internal PlayerGroupStats(PlayerGroup group)
    {
        this.group = group;
    }

    public void OnAddPlayer(PlayerGroupMember member)
    {
        UpdateMinMaxLevelPlayers();
        CalculateExpLevels();
    }

    public void OnRemovePlayer(PlayerGroupMember member)
    {
        UpdateMinMaxLevelPlayers();
    }

    private void CalculateExpLevels()
    {
        minExpPlayerLevel = minLevelPlayer.GetLevel();
        maxExpPlayerLevel = maxLevelPlayer.GetLevel();
        minLevelPlayer = null;
        maxLevelPlayer = null;
    }

    private void UpdateMinMaxLevelPlayers()
    {
        group.ForEach(player =>
        {
            if (minLevelPlayer == null || maxLevelPlayer == null)
            {
                minLevelPlayer = player;
                maxLevelPlayer = player;
            }
            else
            {
                if (player.GetCommonData().GetExp() < minLevelPlayer.GetCommonData().GetExp())
                {
                    minLevelPlayer = player;
                }
                if (!player.IsMentor() && player.GetCommonData().GetExp() > maxLevelPlayer.GetCommonData().GetExp())
                {
                    maxLevelPlayer = player;
                }
            }
        });
    }

    public int GetMinExpPlayerLevel()
    {
        return minExpPlayerLevel;
    }

    public int GetMaxExpPlayerLevel()
    {
        return maxExpPlayerLevel;
    }
}

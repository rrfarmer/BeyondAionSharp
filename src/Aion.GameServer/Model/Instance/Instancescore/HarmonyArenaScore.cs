using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/HarmonyArenaScore (xTz) : PvPArenaScore. stream filter.findFirst.orElse→FirstOrDefault; sorted(comparing)→OrderBy; mapToInt.sum→Sum. HarmonyGroupReward/AGPlayer red-tolerated.</summary>
public class HarmonyArenaScore : PvPArenaScore
{
    private readonly List<HarmonyGroupReward> groups = new();

    public HarmonyArenaScore(WorldMapInstance instance) : base(instance)
    {
    }

    public HarmonyGroupReward GetGroupReward(int playerId)
    {
        return groups.Where(reward => reward.ContainsPlayer(playerId)).FirstOrDefault();
    }

    public List<HarmonyGroupReward> GetHarmonyGroupInside()
    {
        List<HarmonyGroupReward> harmonyGroups = new();
        foreach (HarmonyGroupReward group in groups)
        {
            foreach (AGPlayer agp in group.GetAssociatedPlayers())
            {
                Player p = instance.GetPlayer(agp.ObjectId());
                if (p != null)
                {
                    harmonyGroups.Add(group);
                    break;
                }
            }
        }
        return harmonyGroups;
    }

    public List<Player> GetPlayersInside(HarmonyGroupReward group)
    {
        List<Player> players = new();
        foreach (Player playerInside in instance.GetPlayersInside())
        {
            if (group.ContainsPlayer(playerInside.GetObjectId()))
            {
                players.Add(playerInside);
            }
        }
        return players;
    }

    public void AddHarmonyGroup(HarmonyGroupReward reward)
    {
        groups.Add(reward);
    }

    public List<HarmonyGroupReward> GetGroups()
    {
        return groups;
    }

    public override int GetRank(PvPArenaPlayerReward reward)
    {
        List<HarmonyGroupReward> sortedByPoints = groups.OrderBy(r => r.GetScorePoints()).ToList();

        int rank = -1;
        foreach (PvPArenaPlayerReward r in sortedByPoints)
        {
            if (r.GetScorePoints() >= reward.GetScorePoints())
                rank++;
        }
        return rank;
    }

    public override int GetTotalPoints()
    {
        return groups.Sum(r => r.GetScorePoints());
    }

    public override void Clear()
    {
        groups.Clear();
        base.Clear();
    }
}

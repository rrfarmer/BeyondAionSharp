using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Instance.Playerreward;

/// <summary>Java parity: model/instance/playerreward/HarmonyGroupReward. Java byte buffId → sbyte.</summary>
public class HarmonyGroupReward : PvPArenaPlayerReward
{
    private readonly List<AGPlayer> players = new List<AGPlayer>();
    private readonly int grpObjectId;

    public HarmonyGroupReward(int objectId, int timeBonus, sbyte buffId, int grpObjectId)
        : base(objectId, timeBonus, buffId)
    {
        this.grpObjectId = grpObjectId;
        SetCourageInsignia(new ArenaRewardItem(186000137, 0, 0, 0));
    }

    public List<AGPlayer> GetAssociatedPlayers()
    {
        return players;
    }

    public bool ContainsPlayer(int objectId)
    {
        return players.Any(agp => agp.ObjectId == objectId);
    }

    public AGPlayer GetAGPlayer(int objectId)
    {
        foreach (AGPlayer agp in players)
        {
            if (agp.ObjectId == objectId)
            {
                return agp;
            }
        }
        return null;
    }

    public void AddPlayer(AGPlayer player)
    {
        players.Add(player);
    }

    public int GetGrpObjectId()
    {
        return grpObjectId;
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/TiamatsIncarnationAI (@author Estrayl).</summary>
[AIName("tiamats_incarnation")]
public class TiamatsIncarnationAI : AggressiveNpcAI
{
    public TiamatsIncarnationAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleActivate()
    {
        base.HandleActivate();
        ScheduleSummons(20000);
    }

    private void ScheduleSummons(int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead() && GetTarget() != null)
            {
                List<Player> nearbyPlayers = GetNearbyPlayers();
                if (nearbyPlayers.Count > 1)
                {
                    Player first = RemoveAt(nearbyPlayers, Rnd.NextInt(nearbyPlayers.Count));
                    Player second = RemoveAt(nearbyPlayers, Rnd.NextInt(nearbyPlayers.Count));
                    int summonId = Rnd.Get(GetSummonNpcIds().ToArray());
                    Spawn(summonId, first.GetX(), first.GetY(), first.GetZ(), (sbyte)0);
                    Spawn(summonId, second.GetX(), second.GetY(), second.GetZ(), (sbyte)0);
                    ScheduleSummons(30000);
                }
            }
            return ValueTask.CompletedTask;
        }, delay);
    }

    private static Player RemoveAt(List<Player> list, int index)
    {
        Player p = list[index];
        list.RemoveAt(index);
        return p;
    }

    private List<Player> GetNearbyPlayers()
    {
        return GetKnownList().StreamPlayers().Where(player => !player.IsDead() && IsInRange(player, 30)).ToList();
    }

    protected override void HandleDespawned()
    {
        foreach (int id in GetSummonNpcIds())
            GetPosition().GetWorldMapInstance().GetNpcs(id).ToList().ForEach(npc => npc.GetController().Delete());
        base.HandleDespawned();
    }

    private List<int> GetSummonNpcIds()
    {
        switch (GetNpcId())
        {
            case 219366: // Graviwing
                return new List<int> { 282727, 282729 }; // Gravity Whirlpool, Thunderbolt Whirlpool
            case 236279: // Graviwing HM
                return new List<int> { 856074, 856076 };
            case 219368: // Petriscale
                return new List<int> { 282731 }; // Petrification Crystal
            case 236281: // Petriscale HM
                return new List<int> { 856072 };
            case 219365: // Fissure Fang
                return new List<int> { 282735, 282737 }; // Cavity of Earth, Collapsing Earth
            case 236278: // Fissure Fang HM
                return new List<int> { 856068, 856070 };
            default:
                return new List<int>();
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.ALLOW_DECAY:
                return false;
            default:
                return base.Ask(question);
        }
    }
}

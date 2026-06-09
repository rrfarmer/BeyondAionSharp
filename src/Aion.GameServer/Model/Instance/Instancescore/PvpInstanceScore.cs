using Aion.GameServer.Model;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/PvpInstanceScore. synchronized → lock(this).</summary>
public class PvpInstanceScore<T> : InstanceScore<T> where T : InstancePlayerReward
{
    private readonly int winnerApReward;
    private readonly int loserApReward;
    private readonly int drawApReward;
    private int asmodiansPoints;
    private int elyosPoints;
    private int asmodiansKills;
    private int elyosKills;

    public PvpInstanceScore(int winnerApReward, int loserApReward, int drawApReward)
    {
        this.winnerApReward = winnerApReward;
        this.loserApReward = loserApReward;
        this.drawApReward = drawApReward;
    }

    public int GetWinnerApReward()
    {
        return winnerApReward;
    }

    public int GetLoserApReward()
    {
        return loserApReward;
    }

    public int GetDrawApReward()
    {
        return drawApReward;
    }

    public int GetAsmodiansPoints()
    {
        return asmodiansPoints;
    }

    public int GetElyosPoints()
    {
        return elyosPoints;
    }

    public int GetPointsByRace(Race race)
    {
        return race == Race.ELYOS ? elyosPoints : asmodiansPoints;
    }

    public int GetAsmodiansKills()
    {
        return asmodiansKills;
    }

    public int GetElyosKills()
    {
        return elyosKills;
    }

    public Race GetRaceWithHighestPoints()
    {
        return elyosPoints == asmodiansPoints ? Race.NONE : elyosPoints > asmodiansPoints ? Race.ELYOS : Race.ASMODIANS;
    }

    public void AddPointsByRace(Race race, int points)
    {
        lock (this)
        {
            switch (race)
            {
                case Race.ELYOS:
                    elyosPoints = System.Math.Max(0, elyosPoints + points);
                    break;
                case Race.ASMODIANS:
                    asmodiansPoints = System.Math.Max(0, asmodiansPoints + points);
                    break;
            }
        }
    }

    public void IncrementKillsByRace(Race race)
    {
        lock (this)
        {
            switch (race)
            {
                case Race.ELYOS:
                    elyosKills++;
                    break;
                case Race.ASMODIANS:
                    asmodiansKills++;
                    break;
            }
        }
    }
}

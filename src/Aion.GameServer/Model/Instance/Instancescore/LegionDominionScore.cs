using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/LegionDominionScore (Yeats) : InstanceScore&lt;InstancePlayerReward&gt;. Plain score holder.</summary>
public class LegionDominionScore : InstanceScore<InstancePlayerReward>
{
    private int points;
    private int finalGP;
    private int rank = 7;
    private int finalAP;
    private int rewardItem1;
    private int rewardItem2;
    private int rewardItem3;
    private int rewardItem4;
    private int rewardItem1Count;
    private int rewardItem2Count;
    private int rewardItem3Count;
    private int rewardItem4Count;

    public void AddPoints(int points)
    {
        this.points += points;
    }

    public int GetPoints()
    {
        return points;
    }

    public int GetFinalGP()
    {
        return finalGP;
    }

    public int GetRank()
    {
        return rank;
    }

    public int GetFinalAp()
    {
        return finalAP;
    }

    public int GetRewardItem1()
    {
        return rewardItem1;
    }

    public int GetRewardItem2()
    {
        return rewardItem2;
    }

    public int GetRewardItem3()
    {
        return rewardItem3;
    }

    public int GetRewardItem4()
    {
        return rewardItem4;
    }

    public int GetRewardItem1Count()
    {
        return rewardItem1Count;
    }

    public int GetRewardItem2Count()
    {
        return rewardItem2Count;
    }

    public int GetRewardItem3Count()
    {
        return rewardItem3Count;
    }

    public int GetRewardItem4Count()
    {
        return rewardItem4Count;
    }

    public void SetPoints(int points)
    {
        this.points = points;
    }

    public void SetFinalGP(int finalGP)
    {
        this.finalGP = finalGP;
    }

    public void SetRank(int rank)
    {
        this.rank = rank;
    }

    public void SetFinalAP(int finalAP)
    {
        this.finalAP = finalAP;
    }

    public void SetRewardItem1(int rewardItem1)
    {
        this.rewardItem1 = rewardItem1;
    }

    public void SetRewardItem2(int rewardItem2)
    {
        this.rewardItem2 = rewardItem2;
    }

    public void SetRewardItem3(int rewardItem3)
    {
        this.rewardItem3 = rewardItem3;
    }

    public void SetRewardItem4(int rewardItem4)
    {
        this.rewardItem4 = rewardItem4;
    }

    public void SetRewardItem1Count(int rewardItem1Count)
    {
        this.rewardItem1Count = rewardItem1Count;
    }

    public void SetRewardItem2Count(int rewardItem2Count)
    {
        this.rewardItem2Count = rewardItem2Count;
    }

    public void SetRewardItem3Count(int rewardItem3Count)
    {
        this.rewardItem3Count = rewardItem3Count;
    }

    public void SetRewardItem4Count(int rewardItem4Count)
    {
        this.rewardItem4Count = rewardItem4Count;
    }
}

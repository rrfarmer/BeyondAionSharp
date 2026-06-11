using System;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Instance.Playerreward;

/// <summary>Java parity: model/instance/playerreward/PvPArenaPlayerReward. Java byte buffId → sbyte; nullable PlayerClass → PlayerClass?.</summary>
public class PvPArenaPlayerReward : InstancePlayerReward
{
    private readonly PlayerClass? playerClass;
    private readonly string playerName;
    private int position;
    private int timeBonus;
    private float timeBonusModifier;
    // Default values for training arenas
    private ArenaRewardItem ap = new ArenaRewardItem(0, 0, 0, 0);
    private ArenaRewardItem gp = new ArenaRewardItem(0, 0, 0, 0);
    private ArenaRewardItem crucibleInsignia = new ArenaRewardItem(0, 0, 0, 0);
    private ArenaRewardItem courageInsignia = new ArenaRewardItem(0, 0, 0, 0);
    private RewardItem rewardItem1;
    private RewardItem rewardItem2;
    private long logoutTime;
    private bool isRewarded = false;
    private InstanceBuff boostMorale;

    public PvPArenaPlayerReward(Player player, int timeBonus, sbyte buffId)
        : this(player.GetObjectId(), player.GetPlayerClass(), player.GetName(), timeBonus, buffId)
    {
    }

    public PvPArenaPlayerReward(int objectId, int timeBonus, sbyte buffId)
        : this(objectId, null, null, timeBonus, buffId)
    {
    }

    private PvPArenaPlayerReward(int objectId, PlayerClass? playerClass, string playerName, int timeBonus, sbyte buffId)
        : base(objectId)
    {
        SetPoints(13000);
        this.playerClass = playerClass;
        this.playerName = playerName;
        this.timeBonus = timeBonus;
        timeBonusModifier = ((float) this.timeBonus / (float) 660000);
        boostMorale = new InstanceBuff(buffId);
    }

    public void AddPoints(int points, PvPArenaScore instanceScore)
    {
        base.AddPoints(points);
        if (GetPoints() > instanceScore.GetUpperScoreCap())
            SetPoints(instanceScore.GetUpperScoreCap());
        else if (GetPoints() < instanceScore.GetLowerScoreCap())
            SetPoints(instanceScore.GetLowerScoreCap());
    }

    public PlayerClass? GetPlayerClass()
    {
        return playerClass;
    }

    public string GetPlayerName()
    {
        return playerName;
    }

    public int GetPosition()
    {
        return position;
    }

    public void SetPosition(int position)
    {
        this.position = position;
    }

    public int GetTimeBonus()
    {
        return Math.Max(timeBonus, 0);
    }

    public void UpdateLogoutTime()
    {
        logoutTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateBonusTime()
    {
        int offlineTime = (int) (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - logoutTime);
        timeBonus -= (int) (offlineTime * timeBonusModifier);
    }

    public bool IsRewarded()
    {
        return isRewarded;
    }

    public void SetRewarded()
    {
        isRewarded = true;
    }

    public float GetParticipation()
    {
        return (float) GetTimeBonus() / timeBonus;
    }

    public ArenaRewardItem GetAp()
    {
        return ap;
    }

    public void SetAp(ArenaRewardItem ap)
    {
        this.ap = ap;
    }

    public ArenaRewardItem GetGp()
    {
        return gp;
    }

    public void SetGp(ArenaRewardItem gp)
    {
        this.gp = gp;
    }

    public ArenaRewardItem GetCrucibleInsignia()
    {
        return crucibleInsignia;
    }

    public void SetCrucibleInsignia(ArenaRewardItem crucibleInsignia)
    {
        this.crucibleInsignia = crucibleInsignia;
    }

    public ArenaRewardItem GetCourageInsignia()
    {
        return courageInsignia;
    }

    public void SetCourageInsignia(ArenaRewardItem courageInsignia)
    {
        this.courageInsignia = courageInsignia;
    }

    public RewardItem GetRewardItem1()
    {
        return rewardItem1;
    }

    public void SetRewardItem1(RewardItem rewardItem1)
    {
        this.rewardItem1 = rewardItem1;
    }

    public RewardItem GetRewardItem2()
    {
        return rewardItem2;
    }

    public void SetRewardItem2(RewardItem rewardItem2)
    {
        this.rewardItem2 = rewardItem2;
    }

    public int GetScorePoints()
    {
        return timeBonus + GetPoints();
    }

    public int GetRemainingTime()
    {
        return boostMorale.GetRemainingTime();
    }

    public bool HasBoostMorale()
    {
        return boostMorale.GetRemainingTime() > 0;
    }

    public void ApplyBoostMoraleEffect(Player player, int duration)
    {
        boostMorale.ApplyEffect(player, duration);
    }

    public void EndBoostMoraleEffect(Player player)
    {
        boostMorale.EndEffect(player);
    }
}

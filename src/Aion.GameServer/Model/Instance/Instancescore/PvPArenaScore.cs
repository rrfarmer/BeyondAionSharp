using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instanceposition;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Instance.Instancescore;

/// <summary>Java parity: model/instance/instancescore/PvPArenaScore (xTz). InstanceScore&lt;PvPArenaPlayerReward&gt;. switch-expr w/ case null,default→C# switch (enum can't be null, default covers); Collections.addAll→AddRange; Map<Integer,Boolean>→Dictionary<int,bool>; List.remove(index)→[i]+RemoveAt; synchronized→lock(this); signed byte→sbyte; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; IntSummaryStatistics→Max/Min; hex npc-skill literals verbatim. WorldMapType/InstancePosition/PvPArenaPlayerReward red-tolerated.</summary>
public class PvPArenaScore : InstanceScore<PvPArenaPlayerReward>
{
    private Dictionary<int, bool> positions = new();
    private List<int> zones = new();
    private int round = 1;
    private int? zone;
    private int bonusTime;
    private int difficultyId;
    private int lowerScoreCap;
    private int upperScoreCap;
    private int maxScoreGap;
    private long instanceTime;
    private readonly sbyte buffId;
    protected WorldMapInstance instance;
    private GeneralInstancePosition instancePosition;

    public PvPArenaScore(WorldMapInstance instance)
    {
        this.instance = instance;

        WorldMapType wmt = WorldMapTypeExtensions.GetWorld(instance.GetMapId());
        bool isSolo = IsSoloArena(wmt);
        buffId = (sbyte)(isSolo ? 8 : 7);
        zones.AddRange(isSolo ? new[] { 1, 2, 3, 4 } : new[] { 1, 2, 3, 4, 5, 6 });

        int positionSize = 0;
        switch (wmt)
        {
            case WorldMapType.DISCIPLINE_TRAINING_GROUNDS:
            case WorldMapType.ARENA_OF_DISCIPLINE:
                bonusTime = 8100;
                positionSize = 4;
                instancePosition = new DisciplineInstancePosition();
                break;
            case WorldMapType.ARENA_OF_GLORY:
                bonusTime = 10800;
                positionSize = 8;
                instancePosition = new GloryInstancePosition();
                break;
            case WorldMapType.HARMONY_TRAINING_GROUNDS:
            case WorldMapType.ARENA_OF_HARMONY:
                bonusTime = 12000;
                positionSize = 12;
                instancePosition = new HarmonyInstancePosition();
                break;
            case WorldMapType.CHAOS_TRAINING_GROUNDS:
            case WorldMapType.ARENA_OF_CHAOS:
                bonusTime = 12000;
                positionSize = 12;
                instancePosition = new ChaosInstancePosition();
                break;
            default:
                return;
        }

        instancePosition.Initialize(instance.GetMapId(), instance.GetInstanceId());
        for (int i = 1; i <= positionSize; i++)
        {
            positions[i] = false;
        }
        SetRndZone();
    }

    public bool IsSoloArena(WorldMapType wmt)
    {
        return wmt == WorldMapType.DISCIPLINE_TRAINING_GROUNDS || wmt == WorldMapType.ARENA_OF_DISCIPLINE;
    }

    public void SetRndZone()
    {
        int index = Rnd.NextInt(zones.Count);
        zone = zones[index];
        zones.RemoveAt(index);
    }

    private List<int> GetFreePositions()
    {
        List<int> p = new();
        foreach (int key in positions.Keys)
        {
            if (!positions[key])
            {
                p.Add(key);
            }
        }
        return p;
    }

    public void SetRndPosition(int objectId)
    {
        lock (this)
        {
            PvPArenaPlayerReward reward = GetPlayerReward(objectId);
            int position = reward.GetPosition();
            if (position != 0)
            {
                ClearPosition(position, false);
            }
            List<int> freePositions = GetFreePositions();
            if (freePositions.Count != 0)
            {
                int key = Rnd.Get(freePositions);
                ClearPosition(key, true);
                reward.SetPosition(key);
            }
        }
    }

    public void ClearPosition(int position, bool result)
    {
        lock (this)
        {
            positions[position] = result;
        }
    }

    public int GetRound()
    {
        return round;
    }

    public void IncrementRound()
    {
        this.round++;
        if (round > 3)
            round = 3;
    }

    public bool RegPlayerReward(Player player)
    {
        if (!ContainsPlayer(player.GetObjectId()))
        {
            AddPlayerReward(new PvPArenaPlayerReward(player, bonusTime, buffId));
            return true;
        }
        return false;
    }

    public void PortToPosition(Player player)
    {
        int objectId = player.GetObjectId();
        RegPlayerReward(player);
        SetRndPosition(objectId);
        instancePosition.Port(player, zone.Value, GetPlayerReward(objectId).GetPosition());
    }

    public virtual int GetRank(PvPArenaPlayerReward reward)
    {
        List<PvPArenaPlayerReward> sortedByPoints = GetPlayerRewards().OrderBy(r => r.GetScorePoints()).ToList();

        int rank = -1;
        foreach (PvPArenaPlayerReward r in sortedByPoints)
        {
            if (r.GetScorePoints() >= reward.GetScorePoints())
                rank++;
        }
        return rank;
    }

    public bool ReachedScoreGap()
    {
        List<int> points = GetPlayerRewards().Select(r => r.GetPoints()).ToList();
        int maxPoints = points.Max();
        int minPoints = points.Min();
        return maxPoints - minPoints >= maxScoreGap;
    }

    public virtual int GetTotalPoints()
    {
        return GetPlayerRewards().Sum(r => r.GetScorePoints());
    }

    public bool CanReward()
    {
        return WorldMapTypeExtensions.GetWorld(instance.GetMapId()) switch
        {
            WorldMapType.ARENA_OF_DISCIPLINE or WorldMapType.ARENA_OF_GLORY or WorldMapType.ARENA_OF_CHAOS or WorldMapType.ARENA_OF_HARMONY => true,
            _ => false,
        };
    }

    public int GetNpcBonusSkill(int npcId)
    {
        switch (npcId)
        {
            case 701175: // Plaza Flame Thrower
            case 701176:
            case 701177:
            case 701178:
                return 0x4E5732; // 20055 50
            case 701189: // Plaza Flame Thrower
            case 701190:
            case 701191:
            case 701192:
                return 0x4FDB3C; // 20443 60
            case 701317: // Illusion of Hope level 47
                return 0x4f8532; // 20357 50
            case 701318: // Illusion of Hope level 51
                return 0x4f8537; // 20357 55
            case 701319: // Illusion of Hope level 56
                return 0x4f853C; // 20357 60
            case 701220: // Blesed Relic
                return 0x4E5537; // 20053 55 //20068, 20072
            case 207118:
            case 207119:
                return 0x50C101; // 20673 1
            case 207100:
                return 0x50BE01; // 20670 1
            default:
                return 0;
        }
    }

    public int GetTime()
    {
        long result = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - instanceTime;
        if (IsRewarded())
        {
            return 0;
        }
        if (result < 120000)
        {
            return (int)(120000 - result);
        }
        else
        {
            return (int)(180000 * GetRound() - (result - 120000));
        }
    }

    public int GetDifficultyId()
    {
        return difficultyId;
    }

    public void SetDifficultyId(int difficultyId)
    {
        this.difficultyId = difficultyId;
    }

    public int GetLowerScoreCap()
    {
        return lowerScoreCap;
    }

    public void SetLowerScoreCap(int lowerScoreCap)
    {
        this.lowerScoreCap = lowerScoreCap;
    }

    public int GetUpperScoreCap()
    {
        return upperScoreCap;
    }

    public void SetUpperScoreCap(int upperScoreCap)
    {
        this.upperScoreCap = upperScoreCap;
    }

    public void SetMaxScoreGap(int maxScoreGap)
    {
        this.maxScoreGap = maxScoreGap;
    }

    public void SetInstanceStartTime()
    {
        this.instanceTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public sbyte GetBuffId()
    {
        return buffId;
    }

    public override void Clear()
    {
        base.Clear();
        positions.Clear();
    }
}

using Aion.GameServer.Utils.Stats;
using System.Collections.Generic;
using System.Linq;
using Aion.Commons.Nio;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;

namespace Aion.GameServer.Network.Aion.Instanceinfo;

/// <summary>
/// Java parity: network/aion/instanceinfo/PvpInstanceScoreWriter (Estrayl) : InstanceScoreWriter&lt;PvpInstanceScore&lt;PvpInstancePlayerReward&gt;&gt;.
/// Multi-mode writer (4 ctors) switched on InstanceScoreType. switch-arrow -> C# switch statement; stream().filter().toList() -> LINQ
/// Where().ToList(); Race SCREAMING (ELYOS/ASMODIANS). writeS(name,52) fixed. PvpInstanceScore/InstanceProgressionType red-tolerated.
/// </summary>
public class PvpInstanceScoreWriter : InstanceScoreWriter<PvpInstanceScore<PvpInstancePlayerReward>>
{
    private const int MAXIMUM_PLAYER_COUNT_PER_FACTION = 24;
    private readonly InstanceScoreType instanceScoreType;
    private List<Player> participants;
    private Race race;
    private int objectId;
    private int status;

    public PvpInstanceScoreWriter(PvpInstanceScore<PvpInstancePlayerReward> reward, InstanceScoreType instanceScoreType)
        : base(reward)
    {
        this.instanceScoreType = instanceScoreType;
    }

    public PvpInstanceScoreWriter(PvpInstanceScore<PvpInstancePlayerReward> reward, InstanceScoreType instanceScoreType, Race race)
        : base(reward)
    {
        this.instanceScoreType = instanceScoreType;
        this.race = race;
    }

    public PvpInstanceScoreWriter(PvpInstanceScore<PvpInstancePlayerReward> reward, InstanceScoreType instanceScoreType, int objectId, int status)
        : base(reward)
    {
        this.instanceScoreType = instanceScoreType;
        this.objectId = objectId;
        this.status = status;
    }

    public PvpInstanceScoreWriter(PvpInstanceScore<PvpInstancePlayerReward> reward, InstanceScoreType instanceScoreType, List<Player> participants)
        : base(reward)
    {
        this.instanceScoreType = instanceScoreType;
        this.participants = participants;
    }

    protected override void WriteMe(ByteBuffer buf)
    {
        WriteC(buf, instanceScoreType.GetId());
        switch (instanceScoreType)
        {
            case InstanceScoreType.UPDATE_INSTANCE_PROGRESS:
                WriteD(buf, instanceScore.GetInstanceProgressionType() == InstanceProgressionType.START_PROGRESS ? 0 : 2);
                break;
            case InstanceScoreType.INIT_PLAYER:
                WriteD(buf, 0); // unk
                WriteD(buf, status); // player is dead: 60 else 0
                WriteD(buf, objectId);
                WriteD(buf, instanceScore.GetPlayerReward(objectId).GetRace().GetRaceId());
                break;
            case InstanceScoreType.UPDATE_PLAYER_BUFF_STATUS:
                WriteD(buf, 0);
                WriteD(buf, status); // Spawn(0) - Dead(60)
                WriteD(buf, objectId); // PlayerObjectId
                break;
            case InstanceScoreType.SHOW_REWARD:
                WriteShowReward(buf);
                break;
            case InstanceScoreType.UPDATE_INSTANCE_BUFFS_AND_SCORE:
                WriteUpdateScoreToBuffer(buf);
                break;
            case InstanceScoreType.UPDATE_ALL_PLAYER_INFO:
                WritePlayerFullData(buf);
                break;
            case InstanceScoreType.PLAYER_QUIT:
                WriteD(buf, objectId);
                break;
            case InstanceScoreType.UPDATE_FACTION_SCORE:
                WriteC(buf, 0);
                WriteD(buf, race == Race.ELYOS ? instanceScore.GetElyosKills() : instanceScore.GetAsmodiansKills());
                WriteD(buf, race == Race.ELYOS ? instanceScore.GetElyosPoints() : instanceScore.GetAsmodiansPoints());
                WriteD(buf, race.GetRaceId());
                WriteD(buf, instanceScore.GetRaceWithHighestPoints().GetRaceId());
                break;
        }
    }

    private void WriteShowReward(ByteBuffer buf)
    {
        PvpInstancePlayerReward reward = instanceScore.GetPlayerReward(objectId);
        WriteD(buf, 100); // Participation
        WriteD(buf, reward.GetBaseAp());
        WriteD(buf, reward.GetBonusAp());
        WriteD(buf, reward.GetBaseGp());
        WriteD(buf, reward.GetBonusGp());
        WriteD(buf, reward.GetReward1ItemId());
        WriteD(buf, reward.GetReward1Count());
        WriteD(buf, reward.GetReward1BonusCount());
        WriteD(buf, reward.GetReward2ItemId());
        WriteD(buf, reward.GetReward2Count());
        WriteD(buf, reward.GetReward2BonusCount());
        WriteD(buf, reward.GetReward3ItemId());
        WriteD(buf, reward.GetReward3Count());
        WriteD(buf, reward.GetReward4ItemId());
        WriteD(buf, reward.GetReward4Count());
        WriteD(buf, reward.GetBonusRewardItemId());
        WriteD(buf, reward.GetBonusRewardCount());
        WriteC(buf, reward.GetBonusRewardItemId() > 0 ? 1 : 0); // showBonusReward flag
    }

    private void WriteUpdateScoreToBuffer(ByteBuffer buf)
    {
        WriteD(buf, 100); // Should be differentiated between asmos and elyos
        WritePlayerBuffInfo(buf, participants.Where(p => p.GetRace() == Race.ELYOS).ToList());
        WritePlayerBuffInfo(buf, participants.Where(p => p.GetRace() == Race.ASMODIANS).ToList());
        WriteC(buf, 0);
        WriteD(buf, instanceScore.GetElyosKills());
        WriteD(buf, instanceScore.GetElyosPoints());
        WriteD(buf, Race.ELYOS.GetRaceId());
        WriteD(buf, 0);
        WriteC(buf, 0);
        WriteD(buf, instanceScore.GetAsmodiansKills());
        WriteD(buf, instanceScore.GetAsmodiansPoints());
        WriteD(buf, Race.ASMODIANS.GetRaceId());
        WriteD(buf, instanceScore.GetRaceWithHighestPoints().GetRaceId());
    }

    private void WritePlayerFullData(ByteBuffer buf)
    {
        WritePlayerFullData(buf, participants.Where(p => p.GetRace() == Race.ELYOS).ToList());
        WritePlayerFullData(buf, participants.Where(p => p.GetRace() == Race.ASMODIANS).ToList());
    }

    private void WritePlayerFullData(ByteBuffer buf, List<Player> players)
    {
        foreach (Player player in players)
        { // 69 bytes per player
            PvpInstancePlayerReward reward = instanceScore.GetPlayerReward(player.GetObjectId());
            if (reward == null)
                continue;
            WriteD(buf, player.GetObjectId());
            WriteC(buf, player.GetPlayerClass().GetClassId());
            WriteC(buf, player.GetAbyssRank().GetRank().GetId());
            WriteC(buf, 0);
            WriteH(buf, 0);
            WriteD(buf, reward.GetPvPKills());
            WriteD(buf, reward.GetPoints());
            WriteS(buf, player.GetName(), 52);
        }
        WriteEmptyDataToBuffer(buf, 69, MAXIMUM_PLAYER_COUNT_PER_FACTION - players.Count);
    }

    private void WritePlayerBuffInfo(ByteBuffer buf, List<Player> players)
    {
        foreach (Player player in players)
        {
            WriteD(buf, 0); // should be instance buff ID
            WriteD(buf, player.IsDead() ? 60 : 0); // was previously used for remaining instance buff time
            WriteD(buf, player.GetObjectId());
        }
        WriteEmptyDataToBuffer(buf, 12, MAXIMUM_PLAYER_COUNT_PER_FACTION - players.Count);
    }

    private void WriteEmptyDataToBuffer(ByteBuffer buf, int dataSize, int missingPlayerCount)
    {
        WriteB(buf, new byte[dataSize * missingPlayerCount]);
    }
}

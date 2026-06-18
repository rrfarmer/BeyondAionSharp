using System.Collections.Generic;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Instance.Playerreward;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>
/// Java parity: instance/crucible/CrucibleInstance (xTz) : GeneralInstanceHandler. Base for CrucibleChallenge/EmpyreanCrucible.
/// PlayerReviveService ns Services.Players; TeleportService ns Services.Teleport. 1:1.
/// </summary>
public class CrucibleInstance : GeneralInstanceHandler
{
    protected bool isInstanceDestroyed = false;
    protected StageType stageType = StageType.DEFAULT;
    protected InstanceScore<CruciblePlayerReward> instanceScore;

    public CrucibleInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnEnterInstance(Player player)
    {
        if (!instanceScore.ContainsPlayer(player.GetObjectId()))
        {
            AddPlayerReward(player);
        }
    }

    public override void OnInstanceCreate()
    {
        instanceScore = new InstanceScore<CruciblePlayerReward>();
    }

    private void AddPlayerReward(Player player)
    {
        instanceScore.AddPlayerReward(new CruciblePlayerReward(player.GetObjectId()));
    }

    protected virtual CruciblePlayerReward GetPlayerReward(int objectId)
    {
        return instanceScore.GetPlayerReward(objectId);
    }

    public override InstanceScore GetInstanceScore()
    {
        return instanceScore;
    }

    protected virtual List<Npc> GetNpcs(int npcId)
    {
        if (!isInstanceDestroyed)
        {
            return instance.GetNpcs(npcId);
        }
        return null;
    }

    protected virtual bool IsInZone(ZoneName zone, Player player)
    {
        return player.IsInsideZone(zone);
    }

    protected virtual void Teleport(Player player, float x, float y, float z, byte h)
    {
        Aion.GameServer.Services.Teleport.TeleportService.TeleportTo(player, instance, x, y, z, h);
    }

    public override StageType GetStage()
    {
        return stageType;
    }

    public override bool OnReviveEvent(Player player)
    {
        Aion.GameServer.Services.Players.PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        return true;
    }

    public override void OnInstanceDestroy()
    {
        isInstanceDestroyed = true;
        instanceScore.Clear();
    }

    public override bool AllowSelfReviveBySkill()
    {
        return false;
    }

    public override bool AllowSelfReviveByItem()
    {
        return false;
    }
}

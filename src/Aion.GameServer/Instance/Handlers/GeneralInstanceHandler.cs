using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Instance.Handlers;

/// <summary>Java parity: instance/handlers/GeneralInstanceHandler (ATracer) implements InstanceHandler. Concrete base of all instance handlers. Methods virtual (Java overridable). slf4j "INSTANCE_LOG"→ILogger named (info/warn→LogInformation/LogWarning, parameterized {}→named placeholders); UnsupportedOperationException→NotSupportedException; getClass()→GetType(); InstanceScore&lt;?&gt;→non-generic InstanceScore. World/Item/Storage/SpawnEngine red-tolerated.</summary>
public class GeneralInstanceHandler : IInstanceHandler
{
    protected static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("INSTANCE_LOG");
    protected readonly WorldMapInstance instance;
    protected readonly int mapId;

    public GeneralInstanceHandler(WorldMapInstance instance)
    {
        this.instance = instance;
        this.mapId = instance.GetMapId();
    }

    public virtual void OnInstanceCreate()
    {
    }

    public virtual void OnInstanceDestroy()
    {
    }

    public virtual void OnPlayerLogin(Player player)
    {
    }

    public virtual void OnPlayerLogout(Player player)
    {
    }

    public virtual void OnEnterInstance(Player player)
    {
    }

    public virtual void LeaveInstance(Player player)
    {
    }

    public virtual void OnLeaveInstance(Player player)
    {
        player.GetEffectController().RemoveInstanceEffects();
        RemoveInstanceItems(player);
    }

    public virtual void OnOpenDoor(int door)
    {
    }

    public virtual void OnEnterZone(Player player, ZoneInstance zone)
    {
    }

    public virtual void OnLeaveZone(Player player, ZoneInstance zone)
    {
    }

    public virtual void OnPlayMovieEnd(Player player, int movieId)
    {
    }

    public virtual bool OnReviveEvent(Player player)
    {
        return false;
    }

    protected virtual VisibleObject Spawn(int npcId, float x, float y, float z, byte heading)
    {
        SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(mapId, npcId, x, y, z, heading);
        return SpawnEngine.SpawnObject(template, instance.GetInstanceId());
    }

    protected virtual VisibleObject Spawn(int npcId, float x, float y, float z, byte heading, int staticId)
    {
        SpawnTemplate template = SpawnEngine.NewSingleTimeSpawn(mapId, npcId, x, y, z, heading);
        template.SetStaticId(staticId);
        return SpawnEngine.SpawnObject(template, instance.GetInstanceId());
    }

    protected virtual VisibleObject SpawnAndSetRespawn(int npcId, float x, float y, float z, byte heading, int respawnTime)
    {
        SpawnTemplate template = SpawnEngine.NewSpawn(mapId, npcId, x, y, z, heading, respawnTime);
        return SpawnEngine.SpawnObject(template, instance.GetInstanceId());
    }

    protected virtual Npc GetNpc(int npcId)
    {
        return instance.GetNpc(npcId);
    }

    protected virtual void DeleteAliveNpcs(params int[] npcIds)
    {
        foreach (Npc n in instance.GetNpcs(npcIds))
            n.GetController().DeleteIfAliveOrCancelRespawn();
    }

    /// <summary>Sends a message to all players in this instance.</summary>
    protected virtual void SendMsg(SM_SYSTEM_MESSAGE msg)
    {
        SendMsg(msg, 0);
    }

    /// <summary>Sends a message to all players in this instance, after the specified delay (in milliseconds).</summary>
    protected virtual void SendMsg(SM_SYSTEM_MESSAGE msg, int delay)
    {
        PacketSendUtility.BroadcastToMap(instance, msg, delay);
    }

    public virtual void DoReward(Player player)
    {
    }

    public virtual bool OnDie(Player player, Creature lastAttacker)
    {
        return false;
    }

    public virtual void OnStopTraining(Player player)
    {
    }

    public virtual void OnDespawn(Npc npc)
    {
        if (npc.GetPosition().IsInstanceMap() && IsBoss(npc) && !npc.IsDead())
            LogNpcWithReason(npc, "despawned without dying.");
    }

    public virtual void OnDie(Npc npc)
    {
        if (npc.GetPosition().IsInstanceMap() && IsBoss(npc))
            LogNpcWithReason(npc, "was killed.");
    }

    public void LogNpcWithReason(Npc npc, string reason)
    {
        if (instance.GetPlayersInside().Any())
        {
            log.LogInformation("[{MapName}] {NpcName} (ID:{NpcId}) {Reason} Player(s) in instance: {Players}", DataManager.WORLD_MAPS_DATA.GetTemplate(mapId).GetName(), npc.GetName(),
                npc.GetNpcId(), reason,
                string.Join(", ", instance.GetPlayersInside().Select(p => string.Format("{0} (ID:{1})", p.GetName(), p.GetObjectId()))));
        }
    }

    public bool IsBoss(Npc npc)
    {
        return npc.GetLevel() >= 60 && (npc.GetRating() == NpcRating.HERO || npc.GetRating() == NpcRating.LEGENDARY);
    }

    public virtual void OnSpawn(VisibleObject obj)
    {
    }

    public virtual void OnAggro(Npc npc)
    {
    }

    public virtual void OnChangeStage(StageType type)
    {
    }

    public virtual void OnChangeStageList(StageList list)
    {
    }

    public virtual StageType GetStage()
    {
        return StageType.DEFAULT;
    }

    public virtual void OnDropRegistered(Npc npc, int winnerObj)
    {
    }

    public virtual void OnGather(Player player, Gatherable gatherable)
    {
    }

    public virtual InstanceScore GetInstanceScore()
    {
        return null;
    }

    public virtual bool OnPassFlyingRing(Player player, string flyingRing)
    {
        return false;
    }

    public virtual void HandleUseItemFinish(Player player, Npc npc)
    {
    }

    public virtual void OnEndCastSkill(Skill skill)
    {
    }

    public virtual void OnStartEffect(Effect effect)
    {
    }

    public virtual void OnEndEffect(Effect effect)
    {
    }

    public virtual void OnCreatureDetected(Npc detector, Creature detected)
    {
    }

    public virtual void OnSpecialEvent(Npc npc)
    {
    }

    public virtual void OnBackHome(Npc npc)
    {
    }

    public virtual void PortToStartPosition(Player player)
    {
        throw new NotSupportedException();
    }

    public virtual bool CanEnter(Player player)
    {
        return true;
    }

    public virtual float GetExpMultiplier()
    {
        return instance.GetParent().IsInstanceType() ? 1.5f : 1.25f; // on retail, instances reward more exp than regular world maps
    }

    public virtual float GetApMultiplier()
    {
        return 1f;
    }

    public virtual bool AllowSelfReviveBySkill()
    {
        return true; // see skill_prohibit_set_id in client /data/world/worldid.xml + data/skills/client_skill_prohibit.xml
    }

    public virtual bool AllowSelfReviveByItem()
    {
        return true;
    }

    public virtual bool AllowKiskRevive()
    {
        return !instance.GetTemplate().IsInstance();
    }

    public virtual bool AllowInstanceRevive()
    {
        return instance.GetTemplate().IsInstance() && GetType() != typeof(GeneralInstanceHandler) || instance.GetTemplate().GetWorldType() == WorldType.PANESTERRA;
    }

    protected virtual bool IsRestrictedToInstance(Item item)
    {
        return item.GetItemTemplate().IsItemRestrictedToWorld(instance.GetMapId());
    }

    private void RemoveInstanceItems(Player player)
    {
        foreach (Item item in player.GetInventory().GetItems())
            if (IsRestrictedToInstance(item))
                player.GetInventory().DecreaseByObjectId(item.GetObjectId(), item.GetItemCount());
        foreach (Storage storage in player.GetPetBags())
        {
            foreach (Item item in storage.GetItems())
                if (IsRestrictedToInstance(item))
                    storage.DecreaseByObjectId(item.GetObjectId(), item.GetItemCount());
        }
    }
}

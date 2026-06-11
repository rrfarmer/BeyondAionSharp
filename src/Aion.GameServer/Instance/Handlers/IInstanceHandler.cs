using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Instance.Handlers;

/// <summary>Java parity: instance/handlers/InstanceHandler (ATracer) interface. Named IInstanceHandler (matches IPersistable convention). `InstanceScore&lt;?&gt; getInstanceScore()`→non-generic `InstanceScore GetInstanceScore()` (wildcard erasure via non-generic base). StageList/StageType/ZoneInstance/Effect/Skill red-tolerated.</summary>
public interface IInstanceHandler
{
    /// <summary>Executed during instance creation. This method will run after spawns are loaded.</summary>
    void OnInstanceCreate();

    /// <summary>Executed during instance destroy. Runs after all spawns unloaded. All class-shared objects should be cleaned in handler.</summary>
    void OnInstanceDestroy();

    void OnPlayerLogin(Player player);

    void OnPlayerLogout(Player player);

    void OnEnterInstance(Player player);

    void LeaveInstance(Player player);

    void OnLeaveInstance(Player player);

    void OnOpenDoor(int door);

    void OnEnterZone(Player player, ZoneInstance zone);

    void OnLeaveZone(Player player, ZoneInstance zone);

    void OnPlayMovieEnd(Player player, int movieId);

    bool OnReviveEvent(Player player);

    void DoReward(Player player);

    bool OnDie(Player player, Creature lastAttacker);

    void OnStopTraining(Player player);

    void OnDespawn(Npc npc);

    void OnDie(Npc npc);

    void OnSpawn(VisibleObject obj);

    void OnChangeStage(StageType type);

    void OnChangeStageList(StageList list);

    StageType GetStage();

    void OnDropRegistered(Npc npc, int winnerObj);

    void OnGather(Player player, Gatherable gatherable);

    InstanceScore GetInstanceScore();

    bool OnPassFlyingRing(Player player, string flyingRing);

    void HandleUseItemFinish(Player player, Npc npc);

    void OnEndCastSkill(Skill skill);

    void OnAggro(Npc npc);

    void OnStartEffect(Effect effect);

    void OnEndEffect(Effect effect);

    void OnCreatureDetected(Npc detector, Creature detected);

    void OnSpecialEvent(Npc npc);

    void OnBackHome(Npc npc);

    void PortToStartPosition(Player player);

    bool CanEnter(Player player);

    float GetExpMultiplier();

    float GetApMultiplier();

    bool AllowSelfReviveBySkill();
    bool AllowSelfReviveByItem();
    bool AllowKiskRevive();
    bool AllowInstanceRevive();
}

using System.Collections.Generic;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/NpcAI extends AITemplate&lt;Npc&gt; (@author ATracer).</summary>
public abstract class NpcAI : AITemplate<Npc>
{
    private static readonly HashSet<Race> apRewardingRaces = new HashSet<Race>
    {
        Race.ASMODIANS, Race.DARK, Race.DRAGON, Race.DRAGONET, Race.DRAKAN, Race.ELYOS,
        Race.GCHIEF_DARK, Race.GCHIEF_DRAGON, Race.GCHIEF_LIGHT, Race.GHENCHMAN_DARK, Race.GHENCHMAN_LIGHT, Race.LIGHT, Race.LIZARDMAN, Race.NAGA,
        Race.SIEGEDRAKAN
    };

    public NpcAI(Npc owner) : base(owner)
    {
    }

    protected Aion.GameServer.Model.Templates.Npc.NpcTemplate GetObjectTemplate()
    {
        return GetOwner().GetObjectTemplate();
    }

    protected Aion.GameServer.Model.Templates.Spawns.SpawnTemplate GetSpawnTemplate()
    {
        return GetOwner().GetSpawn();
    }

    // Java parity: getLifeStats() is protected — Java protected includes same-package access (HpPhases). C# protected internal = subclass + same-assembly.
    protected internal NpcLifeStats GetLifeStats()
    {
        return GetOwner().GetLifeStats();
    }

    protected Race GetRace()
    {
        return GetOwner().GetRace();
    }

    protected TribeClass GetTribe()
    {
        return GetOwner().GetTribe();
    }

    protected EffectController GetEffectController()
    {
        return GetOwner().GetEffectController();
    }

    protected KnownList GetKnownList()
    {
        return GetOwner().GetKnownList();
    }

    protected AggroList GetAggroList()
    {
        return GetOwner().GetAggroList();
    }

    protected Aion.GameServer.Model.Skill.NpcSkillList GetSkillList()
    {
        return GetOwner().GetSkillList();
    }

    protected VisibleObject GetCreator()
    {
        return GetOwner().GetCreator();
    }

    /// <summary>DEPRECATED as movements will be processed as commands only from ai.</summary>
    protected Aion.GameServer.Controllers.Movement.NpcMoveController GetMoveController()
    {
        return GetOwner().GetMoveController();
    }

    protected int GetNpcId()
    {
        return GetOwner().GetNpcId();
    }

    protected int GetCreatorId()
    {
        return GetOwner().GetCreatorId();
    }

    protected bool IsInRange(VisibleObject obj, int range)
    {
        return Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), obj, range);
    }

    protected override void HandleActivate()
    {
        Aion.GameServer.Ai.Handler.ActivateEventHandler.OnActivate(this);
    }

    protected override void HandleDeactivate()
    {
        if (Aion.GameServer.Configs.Main.SiegeConfig.BALAUR_AUTO_ASSAULT && GetOwner() is Aion.GameServer.Model.GameObjects.Siege.SiegeNpc || GetOwner().IsRaidMonster())
            return;
        Aion.GameServer.Ai.Handler.ActivateEventHandler.OnDeactivate(this);
    }

    protected override void HandleBeforeSpawned()
    {
        Aion.GameServer.Ai.Handler.SpawnEventHandler.OnBeforeSpawn(this);
    }

    protected override void HandleSpawned()
    {
        Aion.GameServer.Ai.Handler.SpawnEventHandler.OnSpawn(this);
        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnSpawn(this);
    }

    protected override void HandleDespawned()
    {
        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnBeforeDespawn(this);
        Aion.GameServer.Ai.Handler.SpawnEventHandler.OnDespawn(this);
    }

    protected override void HandleDied()
    {
        Aion.GameServer.Ai.Handler.DiedEventHandler.OnDie(this);
    }

    protected override void HandleMoveArrived()
    {
        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnReachedWalkPoint(this);
    }

    protected override void HandleTargetChanged(Creature creature)
    {
        Aion.GameServer.Ai.Handler.ShoutEventHandler.OnSwitchedTarget(this, creature);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.CAN_SHOUT => Aion.GameServer.Configs.Main.AIConfig.SHOUTS_ENABLE && Aion.GameServer.Services.NpcShoutsService.GetInstance().MayShout(GetOwner()),
            AIQuestion.ALLOW_RESPAWN => Aion.GameServer.Services.SiegeService.GetInstance().IsRespawnAllowed(GetOwner()),
            AIQuestion.ALLOW_DECAY or AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.REWARD_LOOT => true,
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => GetOwner().IsBoss() || GetOwner().HasStatic(),
            AIQuestion.REWARD_AP => RewardApFor(GetOwner().GetWorldType()),
            AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE => !GetOwner().IsInInstance(),
            _ => false
        };
    }

    private bool RewardApFor(WorldType wt)
    {
        return wt == WorldType.ABYSS || wt != WorldType.ELYSEA && wt != WorldType.ASMODAE && apRewardingRaces.Contains(GetRace());
    }

    public override bool IsDestinationReached()
    {
        return GetState() switch
        {
            AIState.CONFUSE or AIState.FEAR => Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), GetOwner().GetMoveController().GetTargetX2(),
                GetOwner().GetMoveController().GetTargetY2(), GetOwner().GetMoveController().GetTargetZ2(), 1),
            AIState.FIGHT => Aion.GameServer.Ai.Manager.SimpleAttackManager.IsTargetInAttackRange(GetOwner()),
            AIState.RETURNING => ReturningReached(),
            AIState.FOLLOWING => Aion.GameServer.Ai.Handler.FollowEventHandler.IsInRange(this, GetOwner().GetTarget()),
            AIState.WALKING or AIState.FORCED_WALKING => GetSubState() == AISubState.TALK || Aion.GameServer.Ai.Manager.WalkManager.IsArrivedAtPoint(this),
            _ => true
        };
    }

    private bool ReturningReached()
    {
        Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawn = GetOwner().GetSpawn();
        return Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), spawn.GetX(), spawn.GetY(), spawn.GetZ(), 1);
    }

    protected override void HandleMoveValidate()
    {
        Aion.GameServer.Ai.Handler.MoveEventHandler.OnMoveValidate(this);
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        Aion.GameServer.Ai.Handler.CreatureEventHandler.OnCreatureMoved(this, creature);
    }

    public bool IsMoveSupported()
    {
        return GetOwner().GetGameStats().GetMovementSpeed().GetCurrent() > 0 && !IsInSubState(AISubState.FREEZE);
    }

    /// <summary>NCsoft uses different non-visible npcs as a sensor to trigger different events.</summary>
    public virtual void HandleCreatureDetected(Creature creature)
    {
    }
}

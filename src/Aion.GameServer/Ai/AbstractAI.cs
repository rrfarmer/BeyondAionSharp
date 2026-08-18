using System;
using System.Reflection;
using System.Threading;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/AbstractAI&lt;T extends Creature&gt; implements AI (@author ATracer).
/// Non-generic base + generic shim (no C# wildcard generics; Creature holds the non-generic AbstractAI).
/// </summary>
public abstract class AbstractAI : AI
{
    private static readonly ThreadLocal<int> DEPTH = new ThreadLocal<int>(() => 0);

    private readonly Creature owner;
    private AIState currentState;
    private AISubState currentSubState;
    private bool thinking;

    private bool logging = false;

    private volatile AIEventLog eventLog;

    protected AbstractAI(Creature owner)
    {
        this.owner = owner;
        this.currentState = AIState.CREATED;
        this.currentSubState = AISubState.NONE;
    }

    public AIEventLog GetEventLog()
    {
        return eventLog;
    }

    public virtual AIState GetState()
    {
        return currentState;
    }

    public bool IsInState(AIState state)
    {
        return currentState == state;
    }

    public virtual AISubState GetSubState()
    {
        return currentSubState;
    }

    public bool IsInSubState(AISubState subState)
    {
        return currentSubState == subState;
    }

    public virtual string GetName()
    {
        AIName annotation = GetType().GetCustomAttribute<AIName>();
        return annotation == null ? "noname" : annotation.Value;
    }

    protected virtual bool CanHandleEvent(AiEventType eventType)
    {
        switch (eventType)
        {
            case AiEventType.CreatureMoved:
            case AiEventType.DialogStart:
            case AiEventType.DialogFinish:
                return currentState == AIState.IDLE || currentState == AIState.WALKING;
        }
        return true;
    }

    public bool SetStateIfNot(AIState newState)
    {
        lock (this)
        {
            if (this.currentState == newState)
                return false;

            if (IsLogging())
            {
                AILogger.Info(this, "Setting AI state to " + newState);
                if (this.currentState == AIState.DIED && newState == AIState.FIGHT)
                {
                    System.Diagnostics.StackTrace stack = new System.Diagnostics.StackTrace();
                    foreach (System.Diagnostics.StackFrame elem in stack.GetFrames())
                        AILogger.Info(this, elem.ToString());
                }
            }
            this.currentState = newState;
            return true;
        }
    }

    public bool SetSubStateIfNot(AISubState newSubState)
    {
        lock (this)
        {
            if (this.currentSubState == newSubState)
            {
                if (IsLogging())
                {
                    AILogger.Info(this, "Can't change substate to " + newSubState + " from " + currentSubState);
                }
                return false;
            }
            if (IsLogging())
            {
                AILogger.Info(this, "Setting AI substate to " + newSubState);
            }
            this.currentSubState = newSubState;
            return true;
        }
    }

    public void OnGeneralEvent(AiEventType @event)
    {
        if (currentState.CanHandle(@event) && CanHandleEvent(@event))
        {
            if (IsLogging())
            {
                AILogger.Info(this, "General event " + @event);
            }
            HandleGeneralEvent(@event);
        }
    }

    public void OnCreatureEvent(AiEventType @event, Creature creature)
    {
        if (creature == null)
            throw new ArgumentNullException(nameof(creature), "Creature must not be null");
        if (currentState.CanHandle(@event) && CanHandleEvent(@event))
        {
            if (IsLogging())
            {
                AILogger.Info(this, "Creature event " + @event + ": " + creature.GetObjectTemplate().GetTemplateId());
            }
            try
            {
                int depth = DEPTH.Value;
                if (depth > 20)
                    throw new StackOverflowException(
                        "Aborted abnormal AI event recursion for " + owner + " with AiEventType." + @event + " and target: " + creature + ", most hated: "
                            + owner.GetAggroList().GetTarget(Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED));
                DEPTH.Value = depth + 1;
                HandleCreatureEvent(@event, creature);
            }
            finally
            {
                DEPTH.Value = 0;
            }
        }
    }

    public void OnCustomEvent(int eventId, params object[] args)
    {
        if (IsLogging())
        {
            AILogger.Info(this, "Custom event - id = " + eventId);
        }
        HandleCustomEvent(eventId, args);
    }

    /// <summary>Will be hidden for all AI's below NpcAI.</summary>
    public Creature GetOwner()
    {
        return owner;
    }

    public int GetObjectId()
    {
        return owner.GetObjectId();
    }

    public WorldPosition GetPosition()
    {
        return owner.GetPosition();
    }

    public VisibleObject GetTarget()
    {
        return owner.GetTarget();
    }

    public bool IsDead()
    {
        return owner.IsDead();
    }

    public bool SetThinking()
    {
        lock (this)
        {
            if (thinking)
                return false;
            return thinking = true;
        }
    }

    public void UnsetThinking()
    {
        lock (this)
        {
            thinking = false;
        }
    }

    public bool IsLogging()
    {
        return logging;
    }

    public virtual void SetLogging(bool logging)
    {
        this.logging = logging;
    }

    protected abstract void HandleActivate();
    protected abstract void HandleDeactivate();
    protected abstract void HandleBeforeSpawned();
    protected abstract void HandleSpawned();
    protected abstract void HandleDespawned();
    protected abstract void HandleDied();
    protected abstract void HandleMoveValidate();
    protected abstract void HandleMoveArrived();
    protected abstract void HandleAttackComplete();
    protected abstract void HandleFinishAttack();
    protected abstract void HandleTargetTooFar();
    protected abstract void HandleTargetGiveup();
    protected abstract void HandleNotAtHome();
    protected abstract void HandleBackHome();
    protected abstract void HandleDropRegistered();
    protected abstract void HandleAttack(Creature creature);
    protected abstract void CreatureNeedsHelp(Creature creature);
    protected abstract bool HandleCreatureNeedsSupport(Creature creature);
    protected abstract bool HandleCreatureNeedsSupportByGuard(Creature creature);
    protected abstract void HandleCreatureSee(Creature creature);

    /// <summary>
    /// Retail's <c>on_see_friend_killed_by_user</c>. Virtual rather than abstract: almost nothing
    /// reacts to it, and the 129 patterns that do nearly all answer the same way -- by leaving.
    /// </summary>
    /// <summary>Retail's <c>on_see_friend_attacked</c>.</summary>
    protected virtual void HandleFriendAttacked(Creature hurt)
    {
    }

    /// <summary>Retail's <c>on_friend_spelled</c>.</summary>
    protected virtual void HandleFriendSpelled(Creature hurt)
    {
    }

    protected virtual void HandleFriendKilled(Creature dead)
    {
    }

    /// <summary>Retail's <c>on_spelled</c>: a skill landed on this NPC.</summary>
    protected virtual void HandleSpelled(Creature caster)
    {
    }
    protected abstract void HandleCreatureNotSee(Creature creature);
    protected abstract void HandleCreatureMoved(Creature creature);
    protected abstract void HandleCreatureAggro(Creature creature);
    protected abstract void HandleTargetChanged(Creature creature);
    protected abstract void HandleFollowMe(Creature creature);
    protected abstract void HandleStopFollowMe(Creature creature);
    protected abstract void HandleDialogStart(Player player);
    protected abstract void HandleDialogFinish(Player player);
    protected abstract void HandleCustomEvent(int eventId, params object[] args);

    public abstract bool OnPatternShout(Aion.GameServer.Model.Templates.Npcshout.ShoutEventType @event, string pattern, int skillNumber);

    // AI interface members implemented by concrete subclasses (NpcAI etc.) — abstract here (Java: implicitly abstract via interface).
    public abstract void Think();
    public abstract bool CanThink();
    public abstract bool Ask(AIQuestion question);
    public abstract void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel);
    public abstract void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel);
    public abstract void OnEffectApplied(Effect effect);
    public abstract void OnEffectEnd(Effect effect);
    public abstract bool IsDestinationReached();

    protected virtual void HandleGeneralEvent(AiEventType @event)
    {
        if (IsLogging())
        {
            AILogger.Info(this, "Handle general event " + @event);
        }
        LogEvent(@event);

        switch (@event)
        {
            case AiEventType.MoveValidate:
                HandleMoveValidate();
                break;
            case AiEventType.MoveArrived:
                HandleMoveArrived();
                break;
            case AiEventType.Spawned:
                HandleSpawned();
                break;
            case AiEventType.BeforeSpawned:
                HandleBeforeSpawned();
                break;
            case AiEventType.Despawned:
                HandleDespawned();
                break;
            case AiEventType.Died:
                HandleDied();
                break;
            case AiEventType.AttackComplete:
                HandleAttackComplete();
                break;
            case AiEventType.AttackFinish:
                HandleFinishAttack();
                break;
            case AiEventType.TargetToofar:
                HandleTargetTooFar();
                break;
            case AiEventType.TargetGiveup:
                HandleTargetGiveup();
                break;
            case AiEventType.NotAtHome:
                HandleNotAtHome();
                break;
            case AiEventType.BackHome:
                HandleBackHome();
                break;
            case AiEventType.Activate:
                HandleActivate();
                break;
            case AiEventType.Deactivate:
                HandleDeactivate();
                break;
            case AiEventType.Freeze:
                Aion.GameServer.Ai.Handler.FreezeEventHandler.OnFreeze(this);
                break;
            case AiEventType.Unfreeze:
                Aion.GameServer.Ai.Handler.FreezeEventHandler.OnUnfreeze(this);
                break;
            case AiEventType.DropRegistered:
                HandleDropRegistered();
                break;
        }
    }

    protected void LogEvent(AiEventType @event)
    {
        if (Aion.GameServer.Configs.Main.AIConfig.EVENT_DEBUG)
        {
            if (eventLog == null)
            {
                lock (this)
                {
                    if (eventLog == null)
                    {
                        eventLog = new AIEventLog(10);
                    }
                }
            }
            eventLog.AddFirst(@event);
        }
    }

    private void HandleCreatureEvent(AiEventType @event, Creature creature)
    {
        switch (@event)
        {
            case AiEventType.Attack:
                HandleAttack(creature);
                LogEvent(@event);
                break;
            case AiEventType.CreatureNeedsSupport:
                if (!HandleCreatureNeedsSupport(creature))
                    HandleCreatureNeedsSupportByGuard(creature);
                LogEvent(@event);
                break;
            case AiEventType.CreatureNeedsHelp:
                CreatureNeedsHelp(creature);
                break;
            case AiEventType.CreatureSee:
                HandleCreatureSee(creature);
                break;
            case AiEventType.FriendKilled:
                HandleFriendKilled(creature);
                break;
            case AiEventType.FriendAttacked:
                HandleFriendAttacked(creature);
                break;
            case AiEventType.FriendSpelled:
                HandleFriendSpelled(creature);
                break;
            case AiEventType.Spelled:
                HandleSpelled(creature);
                break;
            case AiEventType.CreatureNotSee:
                HandleCreatureNotSee(creature);
                break;
            case AiEventType.CreatureMoved:
                HandleCreatureMoved(creature);
                break;
            case AiEventType.CreatureAggro:
                HandleCreatureAggro(creature);
                LogEvent(@event);
                break;
            case AiEventType.TargetChanged:
                HandleTargetChanged(creature);
                break;
            case AiEventType.FollowMe:
                HandleFollowMe(creature);
                LogEvent(@event);
                break;
            case AiEventType.StopFollowMe:
                HandleStopFollowMe(creature);
                LogEvent(@event);
                break;
            case AiEventType.DialogStart:
                HandleDialogStart((Player)creature);
                LogEvent(@event);
                break;
            case AiEventType.DialogFinish:
                HandleDialogFinish((Player)creature);
                LogEvent(@event);
                break;
        }
    }

    public abstract AttackIntention ChooseAttackIntention();

    public virtual bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        return false;
    }

    /// <summary>Spawn object in the same world and instance as AI's owner.</summary>
    protected VisibleObject Spawn(int npcId, float x, float y, float z, sbyte heading)
    {
        return Spawn(npcId, x, y, z, heading, 0);
    }

    /// <summary>Spawn object with staticId in the same world and instance as AI's owner.</summary>
    protected VisibleObject Spawn(int npcId, float x, float y, float z, sbyte heading, int staticId)
    {
        return Spawn(npcId, x, y, z, heading, staticId, null);
    }

    protected VisibleObject Spawn(int npcId, float x, float y, float z, sbyte heading, int staticId, string aiName)
    {
        Aion.GameServer.Model.Templates.Spawns.SpawnTemplate template = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(owner.GetWorldId(), npcId, x, y, z, (byte)heading, owner, aiName);
        template.SetStaticId(staticId);
        return Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, owner.GetInstanceId());
    }

    protected VisibleObject RndSpawnInRange(int npcId, float distance)
    {
        double angleRadians = Rnd.NextFloat(360f) * System.Math.PI / 180.0;
        WorldPosition p = GetPosition();
        float x = p.GetX() + (float)(System.Math.Cos(angleRadians) * distance);
        float y = p.GetY() + (float)(System.Math.Sin(angleRadians) * distance);
        Vector3f pos = GeoService.GetInstance().GetClosestCollision(owner, x, y, p.GetZ());
        return Spawn(npcId, pos.GetX(), pos.GetY(), pos.GetZ(), (sbyte)p.GetHeading());
    }

    protected VisibleObject RndSpawnInRange(int npcId, float minDistance, float maxDistance)
    {
        return RndSpawnInRange(npcId, Rnd.NextFloat(minDistance, maxDistance));
    }

    public virtual float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return damage;
    }

    public virtual float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage;
    }

    public virtual void ModifyOwnerStat(Stat2 stat)
    {
    }

    public virtual Aion.GameServer.Model.Templates.Items.ItemAttackType ModifyAttackType(Aion.GameServer.Model.Templates.Items.ItemAttackType type)
    {
        return type;
    }

    public virtual int ModifyAggroRange(int value)
    {
        return value;
    }

    public virtual int ModifyAggroAngle(int value)
    {
        return value;
    }

    public virtual Aion.GameServer.Model.Animations.AttackHandAnimation ModifyAttackHandAnimation(Aion.GameServer.Model.Animations.AttackHandAnimation attackHandAnimation)
    {
        return attackHandAnimation;
    }

    public virtual Aion.GameServer.Model.Animations.AttackTypeAnimation GetAttackTypeAnimation(Creature target)
    {
        return Aion.GameServer.Model.Animations.AttackTypeAnimation.MELEE;
    }

    public virtual int ModifyInitialSkillDelay(int delay)
    {
        return delay;
    }
}

/// <summary>
/// Java parity: generic typing of <see cref="AbstractAI"/> (Java <c>AbstractAI&lt;T extends Creature&gt;</c>).
/// Non-generic base + this generic shim (no C# wildcard generics); typed owner accessor.
/// </summary>
public abstract class AbstractAI<T> : AbstractAI where T : Creature
{
    protected AbstractAI(T owner) : base(owner)
    {
    }

    public new T GetOwner() => (T)base.GetOwner();
}

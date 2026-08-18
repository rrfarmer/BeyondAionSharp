namespace Aion.GameServer.Ai.Event;

/// <summary>
/// Event types dispatched to the AI system.
/// Java parity: ai/event/AiEventType.
/// </summary>
public enum AiEventType
{
    None,
    Activate,
    Deactivate,
    Freeze,
    Unfreeze,
    /// <summary>Creature is being attacked (internal).</summary>
    Attack,
    /// <summary>Creature's attack part is complete (internal).</summary>
    AttackComplete,
    /// <summary>Creature's stopping attack (internal).</summary>
    AttackFinish,
    /// <summary>Some neighbour creature is being attacked (broadcast).</summary>
    CreatureNeedsSupport,
    CreatureNeedsHelp,
    MoveValidate,
    MoveArrived,
    CreatureSee,
    CreatureNotSee,
    CreatureMoved,
    CreatureAggro,
    BeforeSpawned,
    Spawned,
    Despawned,
    Died,
    TargetToofar,
    TargetGiveup,
    TargetChanged,
    FollowMe,
    StopFollowMe,
    NotAtHome,
    BackHome,
    DialogStart,
    DialogFinish,
    DropRegistered,

    /// <summary>
    /// Retail's <c>on_see_friend_killed_by_user</c>: a friendly NPC in sight was killed by a player.
    /// </summary>
    /// <remarks>
    /// Has no aionemu counterpart, so it has no SCREAMING_SNAKE alias below -- nothing ported from
    /// Java calls it. Raised by <see cref="Aion.GameServer.Ai.FriendDeathNotice"/>; 129 retail
    /// patterns carry the handler. See docs/retail-ai-fidelity.md.
    /// </remarks>
    FriendKilled,

    // Java-name aliases (call sites use the Java SCREAMING_SNAKE names; same values, additive).
    NONE = None,
    ACTIVATE = Activate,
    DEACTIVATE = Deactivate,
    FREEZE = Freeze,
    UNFREEZE = Unfreeze,
    ATTACK = Attack,
    ATTACK_COMPLETE = AttackComplete,
    ATTACK_FINISH = AttackFinish,
    CREATURE_NEEDS_SUPPORT = CreatureNeedsSupport,
    CREATURE_NEEDS_HELP = CreatureNeedsHelp,
    MOVE_VALIDATE = MoveValidate,
    MOVE_ARRIVED = MoveArrived,
    CREATURE_SEE = CreatureSee,
    CREATURE_NOT_SEE = CreatureNotSee,
    CREATURE_MOVED = CreatureMoved,
    CREATURE_AGGRO = CreatureAggro,
    BEFORE_SPAWNED = BeforeSpawned,
    SPAWNED = Spawned,
    DESPAWNED = Despawned,
    DIED = Died,
    TARGET_TOOFAR = TargetToofar,
    TARGET_GIVEUP = TargetGiveup,
    TARGET_CHANGED = TargetChanged,
    FOLLOW_ME = FollowMe,
    STOP_FOLLOW_ME = StopFollowMe,
    NOT_AT_HOME = NotAtHome,
    BACK_HOME = BackHome,
    DIALOG_START = DialogStart,
    DIALOG_FINISH = DialogFinish,
    DROP_REGISTERED = DropRegistered,
}

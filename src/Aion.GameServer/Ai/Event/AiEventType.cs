namespace Aion.GameServer.Ai.Event;

/// <summary>
/// Event types dispatched to the AI system.
/// Java parity: ai/event/AIEventType.
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
}

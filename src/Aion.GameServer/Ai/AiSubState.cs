namespace Aion.GameServer.Ai;

/// <summary>
/// Fine-grained AI sub-activity within a state.
/// Java parity: ai/AISubState.
/// </summary>
public enum AISubState
{
    NONE,
    TALK,
    CAST,
    WALK_PATH,
    WALK_RANDOM,
    WALK_WAIT_GROUP,
    FREEZE,
    TARGET_LOST,
}

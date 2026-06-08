namespace Aion.GameServer.Ai;

/// <summary>
/// Fine-grained AI sub-activity within a state.
/// Java parity: ai/AISubState.
/// </summary>
public enum AiSubState
{
    None,
    Talk,
    Cast,
    WalkPath,
    WalkRandom,
    WalkWaitGroup,
    Freeze,
    TargetLost,
}

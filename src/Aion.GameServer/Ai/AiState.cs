using Aion.GameServer.Ai.Event;

namespace Aion.GameServer.Ai;

/// <summary>
/// High-level AI state, each gating which <see cref="AiEventType"/>s it will handle.
/// Java parity: ai/AIState.
/// </summary>
public enum AIState
{
    CREATED,
    DIED,
    DESPAWNED,
    IDLE,
    WALKING,
    FOLLOWING,
    RETURNING,
    FIGHT,
    FEAR,
    CONFUSE,
    FORCED_WALKING,
}

public static class AiStateExtensions
{
    // Java parity: per-constant EnumSet of handled events. States absent here use the
    // no-arg constructor = EnumSet.allOf(AiEventType) (handle every event).
    private static readonly Dictionary<AIState, HashSet<AiEventType>> HandledEvents = new()
    {
        [AIState.CREATED] = new() { AiEventType.BeforeSpawned, AiEventType.Spawned },
        [AIState.DIED] = new() { AiEventType.Despawned, AiEventType.DropRegistered },
        [AIState.DESPAWNED] = new() { AiEventType.BeforeSpawned, AiEventType.Spawned },
        [AIState.FORCED_WALKING] = new()
        {
            AiEventType.MoveArrived, AiEventType.MoveValidate, AiEventType.Despawned, AiEventType.Died,
        },
    };

    // Java parity: canHandle(AiEventType)
    public static bool CanHandle(this AIState state, AiEventType evt) =>
        !HandledEvents.TryGetValue(state, out var set) || set.Contains(evt);
}

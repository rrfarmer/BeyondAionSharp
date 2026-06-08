using Aion.GameServer.Ai.Event;

namespace Aion.GameServer.Ai;

/// <summary>
/// High-level AI state, each gating which <see cref="AiEventType"/>s it will handle.
/// Java parity: ai/AIState.
/// </summary>
public enum AiState
{
    Created,
    Died,
    Despawned,
    Idle,
    Walking,
    Following,
    Returning,
    Fight,
    Fear,
    Confuse,
    ForcedWalking,
}

public static class AiStateExtensions
{
    // Java parity: per-constant EnumSet of handled events. States absent here use the
    // no-arg constructor = EnumSet.allOf(AIEventType) (handle every event).
    private static readonly Dictionary<AiState, HashSet<AiEventType>> HandledEvents = new()
    {
        [AiState.Created] = new() { AiEventType.BeforeSpawned, AiEventType.Spawned },
        [AiState.Died] = new() { AiEventType.Despawned, AiEventType.DropRegistered },
        [AiState.Despawned] = new() { AiEventType.BeforeSpawned, AiEventType.Spawned },
        [AiState.ForcedWalking] = new()
        {
            AiEventType.MoveArrived, AiEventType.MoveValidate, AiEventType.Despawned, AiEventType.Died,
        },
    };

    // Java parity: canHandle(AIEventType)
    public static bool CanHandle(this AiState state, AiEventType evt) =>
        !HandledEvents.TryGetValue(state, out var set) || set.Contains(evt);
}

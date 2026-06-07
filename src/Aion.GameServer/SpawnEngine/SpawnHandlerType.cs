namespace Aion.GameServer.SpawnEngine;

/// <summary>
/// Spawn handler types for NPCs and world objects.
/// Java parity: spawnengine/SpawnHandlerType.
/// </summary>
public enum SpawnHandlerType
{
    // Java parity: ATTACKER
    Attacker,
    // Java parity: BOSS
    Boss,
    // Java parity: FLAG
    Flag,
    // Java parity: GUARDIAN
    Guardian,
    // Java parity: MERCHANT
    Merchant,
    // Java parity: OUTRIDER
    Outrider,
    // Java parity: OUTRIDER_ENHANCED
    OutriderEnhanced,
    // Java parity: RIFT
    Rift,
    // Java parity: SENTINEL
    Sentinel,
    // Java parity: STATIC
    Static,
}

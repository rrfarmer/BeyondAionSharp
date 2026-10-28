namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Calculation domain, not native class numbers or packet enum values.</summary>
internal enum RetailNpcNormalActorKind
{
    Unverified,
    Player,
    OrdinaryOwnerlessNpc,
    OwnedNpc,
    ScaledNpc
}

/// <summary>
/// Declared provenance and supported actor domain for the unrouted arithmetic unit.
/// This checks the declaration; it does not load profiles, verify live actors or enable combat.
/// An effective-input adapter and captured fixtures are still required before routing.
/// </summary>
internal sealed class RetailNpcNormalScope
{
    internal const string MainBinarySha256 =
        "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";

    internal RetailNpcNormalActorKind Attacker { get; }
    internal RetailNpcNormalActorKind Defender { get; }
    internal int AttackerNpcTemplateId { get; }
    internal int DefenderNpcTemplateId { get; }

    private RetailNpcNormalScope(RetailNpcNormalActorKind attacker, RetailNpcNormalActorKind defender,
        int attackerNpcTemplateId, int defenderNpcTemplateId)
    {
        Attacker = attacker;
        Defender = defender;
        AttackerNpcTemplateId = attackerNpcTemplateId;
        DefenderNpcTemplateId = defenderNpcTemplateId;
    }

    internal static RetailNpcNormalScope Create(string sourceMainSha256,
        RetailNpcNormalActorKind attacker, RetailNpcNormalActorKind defender,
        int attackerNpcTemplateId, int defenderNpcTemplateId)
    {
        if (!string.Equals(sourceMainSha256, MainBinarySha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported or unverified retail Main identity.", nameof(sourceMainSha256));
        RequireSupportedActor(attacker, nameof(attacker));
        RequireSupportedActor(defender, nameof(defender));
        if (attacker == RetailNpcNormalActorKind.Player && defender == RetailNpcNormalActorKind.Player)
            throw new ArgumentException("This arithmetic unit does not accept player-versus-player contexts.");
        RequireNpcIdentity(attacker, attackerNpcTemplateId, nameof(attackerNpcTemplateId));
        RequireNpcIdentity(defender, defenderNpcTemplateId, nameof(defenderNpcTemplateId));
        return new RetailNpcNormalScope(attacker, defender, attackerNpcTemplateId, defenderNpcTemplateId);
    }

    internal void RequireNpcAttacker()
    {
        if (Attacker != RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
            throw new ArgumentException("This stage requires an ordinary ownerless NPC attacker.");
    }

    private static void RequireSupportedActor(RetailNpcNormalActorKind actor, string name)
    {
        if (actor is not (RetailNpcNormalActorKind.Player or RetailNpcNormalActorKind.OrdinaryOwnerlessNpc))
            throw new ArgumentOutOfRangeException(name, actor, "Owned, scaled and unverified actors are outside this unit.");
    }

    private static void RequireNpcIdentity(RetailNpcNormalActorKind actor, int npcId, string name)
    {
        if (actor == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc ? npcId <= 0 : npcId != 0)
            throw new ArgumentOutOfRangeException(name, npcId, "Supply the matched NPC template ID, or zero for a player.");
    }
}

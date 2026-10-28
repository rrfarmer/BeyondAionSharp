using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline identity projection for the ordinary initial NPC/player
/// branch. It is not a complete enemy check or a gameplay admission receipt.</summary>
internal static class RetailNpcTribeActorSnapshot
{
    internal sealed record InitialIdentity(string NpcTribe, string PlayerTribe, int Relation, string SourceSha);

    // Source projection only: a native spawn override or later actor mutation
    // can replace the template value before an actual target check.
    internal static InitialIdentity? ResolveProfileSource(SkyTempleNpcProfile profile, string playerTribe,
        RetailNpcTribeMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrEmpty(playerTribe);
        ArgumentNullException.ThrowIfNull(matrix);
        if (playerTribe is not ("PC" or "PC_Dark")) return null;
        var raw = profile.Source.Source.GetRawRootField("tribe").RawValues;
        if (raw.Length != 1 || string.IsNullOrWhiteSpace(raw[0]) || matrix.Find(raw[0], playerTribe) is not int relation)
            return null;
        return new(raw[0], playerTribe, relation, profile.SourceRecordSha256);
    }

    // World::CreateNewTrapOrSkillAreaNPC (NPC server 0x14021a750) copies a
    // user owner's current tribe only when GetTribeConflict is true. Inputs
    // must be supplied from that particular creation; this is not a current
    // actor snapshot and does not establish the rest of the enemy check.
    internal static InitialIdentity? ResolveUserOwnedTrapCreationForOfflineFixture(
        SkyTempleNpcProfile profile, string playerTribe, RetailNpcTribeMatrix matrix,
        bool? nativeWorldTribeConflict, string? nativeOwnerCurrentTribe)
    {
        if (nativeWorldTribeConflict is null) return null;
        var source = ResolveProfileSource(profile, playerTribe, matrix);
        if (source is null || !nativeWorldTribeConflict.Value) return source;
        if (string.IsNullOrWhiteSpace(nativeOwnerCurrentTribe)
            || matrix.Find(nativeOwnerCurrentTribe, playerTribe) is not int relation) return null;
        return new(nativeOwnerCurrentTribe, playerTribe, relation, source.SourceSha);
    }

    internal static InitialIdentity? CaptureForOfflineFixture(Npc npc, Player player, RetailNpcTribeMatrix matrix,
        bool nativeSpawnOverrideKnownAbsent, bool nativePlayerSpecialStateKnownAbsent)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(matrix);
        // These two conditions are native inputs; no 4.8 state is used as a
        // substitute for evidence that the corresponding 5.8 input is absent.
        if (!nativeSpawnOverrideKnownAbsent || !nativePlayerSpecialStateKnownAbsent
            || !npc.IsSpawned() || npc.IsDead() || !player.IsSpawned() || player.IsDead()
            || !ReferenceEquals(npc.GetWorldMapInstance(), player.GetWorldMapInstance())
            || npc.IsTransformed() || player.GetTransformModel().GetTribe() is not null
            || !RetailNpcStatCatalog.TryGet(npc, out var profile)) return null;
        string? playerTribe = player.GetRace() switch
        {
            Race.ELYOS => "PC", Race.ASMODIANS => "PC_Dark", _ => null
        };
        var result = playerTribe is null ? null : ResolveProfileSource(profile, playerTribe, matrix);
        if (result is null) return null;
        // This verifies only this snapshot. Changes after capture and all other
        // native restrictions require a separate owner and revalidation.
        if (!RetailNpcStatCatalog.TryGet(npc, out var current) || !ReferenceEquals(profile, current)) return null;
        return result;
    }
}

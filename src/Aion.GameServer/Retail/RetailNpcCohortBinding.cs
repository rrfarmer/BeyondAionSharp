using System.Collections.Immutable;

namespace Aion.GameServer.Retail;

/// <summary>Reviewed admission metadata, separate from the unchanged extracted record.
/// A calculation being available does not grant membership in a live cohort.</summary>
internal sealed record RetailNpcCohortBinding(string Id, int MapId, string ManifestSha256,
    RetailNpcNormalMotion NormalMotion, ImmutableArray<RetailNpcSkillMotion> SkillMotions,
    int ExpectedHostLevel, string ExpectedHostAi, string HostTemplateSha256,
    ImmutableDictionary<int,ImmutableArray<byte>> SpellSources);

internal sealed record RetailNpcNormalMotion(ushort FirstHit, ushort? SecondHit, string EvidenceSha256,
    ImmutableArray<byte> Evidence)
{
    internal int VariantCount => SecondHit.HasValue ? 2 : 1;
}
internal sealed record RetailNpcSkillMotion(int Ordinal, int SkillId, int Level, ushort FirstHit,
    uint Recovery, uint Cast, uint Reuse, float Ammo, string EvidenceSha256);

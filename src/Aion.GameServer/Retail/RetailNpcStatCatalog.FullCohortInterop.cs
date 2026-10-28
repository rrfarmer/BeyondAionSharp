using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Retail;

internal static partial class RetailNpcStatCatalog
{
    static partial void ResolveFullProfile(Npc npc, ref SkyTempleNpcProfile? profile)
    {
        if (SkyTempleNpcProfileCatalog.TryGet(npc, out var full)) profile = full;
    }
    static partial void ValidateAdditionalHostIdentity(Member member)
    {
        if (SkyTempleNpcProfileCatalog.TryGet(member.MapId, member.Profile.NpcId, out var full)
            && full.SourceRecordSha256 != member.Profile.SourceRecordSha256)
            throw new InvalidDataException("Statistics and existing full package source records disagree");
    }
}

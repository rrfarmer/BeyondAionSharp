using System.Collections.Immutable;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Retail;

internal static partial class RetailNpcStatCatalog
{
    internal sealed record StatAuditBinding(int MapId, int NpcId, int SkillId, int Level, bool SourceBound);
    internal sealed record StatAuditSnapshot(ImmutableArray<Member> Members, ImmutableArray<int> Maps,
        ImmutableArray<StatAuditBinding> SkillBindings, ImmutableArray<RetailNpcStatBuffs.Source> BuffSources);

    /// <summary>Use the production base/normal/buff readers without optional damage,
    /// player-state or movement packages. Nothing is published and no actors are created.
    /// Source binding proves identity only; it does not certify primary skill damage.</summary>
    internal static StatAuditSnapshot ReadStatPackageForAudit(byte[] bytes, string hash, string directory)
    {
        var package = Parse(bytes, hash, directory, includeAdditional: false);
        var members = package.Profiles.Values.OrderBy(m => m.MapId).ThenBy(m => m.Profile.NpcId).ToImmutableArray();
        var bindings = members.SelectMany(m => DeclaredSkills(m.Profile).Select(s =>
            new StatAuditBinding(m.MapId, m.Profile.NpcId, s.Id, s.Level,
                package.BuffRootNames.TryGetValue(s.Id, out var name)
                && MatchesDeclaredSkillBinding(m.Profile, s.Id, s.Level, s.Id, name)))).ToImmutableArray();
        return new(members, package.Maps.Order().ToImmutableArray(), bindings,
            package.Buffs.Values.OrderBy(s => s.Id).ToImmutableArray());
    }
}

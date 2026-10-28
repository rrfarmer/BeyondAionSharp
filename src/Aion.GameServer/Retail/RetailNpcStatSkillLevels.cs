using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Retail;

/// <summary>
/// Projects only the numeric level of existing host NPC skill entries. The host
/// remains the owner of membership, ordering, conditions, chains and scheduling.
/// This list does not add source-only casts. Scripted casting helpers can share
/// the verified numeric resolver without changing which actions they request.
/// </summary>
internal static class RetailNpcStatSkillLevels
{
    internal sealed record LevelResolution(int? Level, string Status);
    internal sealed record Decision(int SkillId, int HostLevel, int? SourceLevel, string Status);
    private sealed record Projection(SkyTempleNpcProfile Profile, NpcSkillList Original,
        NpcSkillList Selected, IReadOnlyList<Decision> Decisions);
    private sealed class Cache
    {
        internal Projection? Current;
    }
    private static readonly ConditionalWeakTable<Npc, Cache> Caches = new();
    private static readonly ConditionalWeakTable<SkyTempleNpcProfile,Dictionary<int,LevelResolution>> DirectLevels=new();
    private sealed class Notice { internal int Emitted; }
    private static readonly ConditionalWeakTable<SkyTempleNpcProfile, Notice> Notices = new();
    private static readonly ILogger Log=AionLog.For(nameof(RetailNpcStatSkillLevels));

    internal static NpcSkillList Get(Npc npc, NpcSkillList original)
    {
        // A virtual call during Creature/Npc construction can precede assignment
        // of Npc's readonly list. Preserve its original null result in that window.
        if (original is null) return original!;
        // Resolve the live context before consulting the cache: creator/owner
        // assignment and retirement must immediately restore the original list.
        if (!RetailNpcStatCatalog.TryGet(npc, out var profile)) return original;
        return Selected(npc, original, profile).Selected;
    }

    internal static IReadOnlyList<Decision> GetDecisions(Npc npc, NpcSkillList original)
    {
        if (original is null) return Array.Empty<Decision>();
        if (!RetailNpcStatCatalog.TryGet(npc, out var profile)) return Array.Empty<Decision>();
        return Selected(npc, original, profile).Decisions;
    }

    internal static IReadOnlyList<Decision> GetDecisions(Npc npc) =>
        !RetailNpcStatCatalog.TryGet(npc, out _) ? Array.Empty<Decision>()
            : GetDecisions(npc, Aion.GameServer.Dataholders.DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(npc.GetNpcId()));

    internal static int? GetDirectLevel(Npc npc,int skillId)
    {
        // Eligibility precedes the immutable-profile cache, so ownership or
        // retirement immediately restores the caller's existing host fallback.
        if(!RetailNpcStatCatalog.TryGet(npc,out var profile))return null;
        var cache=DirectLevels.GetValue(profile,_=>new());
        lock(cache)
        {
            if(!cache.TryGetValue(skillId,out var resolution))
            {
                resolution=Resolve(profile.Source.Source.GetRawRootChildren("skills"),profile.Source.Source.SkillBindings,skillId,
                    (id,level)=>RetailNpcStatCatalog.TryGetSkill(profile,id,level,out var source)?source.Name:null);
                cache.Add(skillId,resolution);
                Log.LogInformation("RETAIL_STAT_DIRECT_SKILL_LEVEL npc={Npc} skill={Skill} level={Level} status={Status} source={Source} generation={Generation}",
                    profile.NpcId,skillId,resolution.Level,resolution.Status,profile.SourceRecordSha256,profile.Generation);
            }
            return resolution.Level;
        }
    }

    private static Projection Selected(Npc npc, NpcSkillList original, SkyTempleNpcProfile profile)
    {
        var cache = Caches.GetValue(npc, _ => new Cache());
        lock (cache)
        {
            if (cache.Current is { } current && ReferenceEquals(current.Profile, profile)
                && ReferenceEquals(current.Original, original)) return current;
            var entries = new List<NpcSkillTemplate>(original.Size());
            var decisions = new List<Decision>(original.Size());
            bool changed = false;
            for (int i = 0; i < original.Size(); i++)
            {
                NpcSkillEntry entry = original.GetSkillOnPosition(i);
                NpcSkillTemplate template = entry.GetTemplate();
                var resolution = Resolve(profile.Source.Source.GetRawRootChildren("skills"),
                    profile.Source.Source.SkillBindings, entry.GetSkillId(),
                    (id, level) => RetailNpcStatCatalog.TryGetSkill(profile, id, level, out var source)
                        ? source.Name : null);
                int hostLevel = entry.GetSkillLevel();
                if (resolution.Level is { } level && level != hostLevel)
                {
                    entries.Add(template.CloneWithSkillLevel(level));
                    decisions.Add(new(entry.GetSkillId(), hostLevel, level, "source-level-applied"));
                    changed = true;
                }
                else
                {
                    entries.Add(template);
                    decisions.Add(new(entry.GetSkillId(), hostLevel, resolution.Level,
                        resolution.Level.HasValue ? "source-level-agrees" : resolution.Status));
                }
            }
            // NpcSkillList sorts stably by host priority, which is already the
            // original ordering. Shared DataManager templates are never mutated.
            if(decisions.Any(d=>d.Status!="source-level-agrees")
                &&Interlocked.Exchange(ref Notices.GetValue(profile,_=>new Notice()).Emitted,1)==0)
                Log.LogInformation("RETAIL_STAT_SKILL_LEVELS npc={Npc} source={Source} applied={Applied} unresolved={Unresolved} decisions={Decisions}",
                    profile.NpcId,profile.SourceRecordSha256,decisions.Count(d=>d.Status=="source-level-applied"),
                    decisions.Count(d=>!d.SourceLevel.HasValue),JsonSerializer.Serialize(decisions));
            return cache.Current = new(profile, original, changed ? new NpcSkillList(entries) : original,
                decisions.AsReadOnly());
        }
    }

    /// <summary>Source binding/ordinal/name validation precedes numeric projection.
    /// The callback independently validates the published skill-source identity.</summary>
    internal static LevelResolution Resolve(IReadOnlyList<JsonElement> groups, JsonElement bindings,
        int skillId, Func<int, int, string?> validatedSourceName)
    {
        if (groups.Count != 1) return new(null, "missing-or-ambiguous-source-skills-group");
        if (bindings.ValueKind != JsonValueKind.Array) return new(null, "missing-source-bindings");
        var rows = RetailNpcSourceProfile.RawChildren(groups[0]).Where(x => x[0].GetString() == "data").ToArray();
        var matched = bindings.EnumerateArray().Where(b => Integer(b, "supported48_id", out int id) && id == skillId).ToArray();
        if (matched.Length == 0) return new(null, "missing-source-binding");
        var levels = new HashSet<int>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ordinals = new HashSet<int>();
        foreach (var binding in matched)
        {
            if (Text(binding, "status") != "dual_join_agrees_static_only"
                || !Integer(binding, "source_group_index", out int group) || group != 0
                || !Integer(binding, "source_entry_index", out int ordinal) || ordinal < 0 || ordinal >= rows.Length)
                return new(null, "unverified-source-binding");
            if (!ordinals.Add(ordinal)) return new(null, "duplicate-source-binding");
            string? name = LeafText(rows[ordinal], "skill_name");
            if (string.IsNullOrEmpty(name) || name != Text(binding, "name")) return new(null, "source-name-mismatch");
            string? rawLevel = LeafText(rows[ordinal], "skill_level");
            if (rawLevel is null || !int.TryParse(rawLevel, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out int level)) return new(null, "missing-or-invalid-source-level");
            if (level is < 0 or > short.MaxValue) return new(null, "unsupported-source-level-range");
            levels.Add(level); names.Add(RetailNpcStatCatalog.NativeSkillNameKey(name));
        }
        if (names.Count != 1) return new(null, "ambiguous-source-name");
        if (levels.Count != 1) return new(null, "ambiguous-source-level");
        int selected = levels.Single();
        string selectedName = names.Single();
        if (!RetailNpcStatCatalog.SameNativeSkillName(validatedSourceName(skillId, selected), selectedName)) return new(null, "unpublished-or-mismatched-skill-source");
        // A second source row with this same native skill name must also have its
        // independent binding. Never silently choose the first ordinal/level.
        for (int i = 0; i < rows.Length; i++)
            if (RetailNpcStatCatalog.SameNativeSkillName(LeafText(rows[i], "skill_name"), selectedName) && !ordinals.Contains(i))
                return new(null, "incomplete-source-binding");
        return new(selected, "verified-source-level");
    }

    private static bool Integer(JsonElement node, string property, out int value)
    {
        value = 0;
        return node.ValueKind == JsonValueKind.Object && node.TryGetProperty(property, out var field)
            && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out value);
    }
    private static string? Text(JsonElement node, string property) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(property, out var field)
            && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    private static string? LeafText(JsonElement row, string tag)
    {
        var leaves = RetailNpcSourceProfile.RawChildren(row).Where(x => x[0].GetString() == tag).ToArray();
        return leaves.Length == 1 ? RetailNpcSourceProfile.RawText(leaves[0]) : null;
    }
}

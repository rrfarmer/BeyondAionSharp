using System.Collections.Frozen;
using System.Text.Json;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Retail;

/// <summary>Independently selected HP/attack/defense corrections. Membership never
/// publishes a navigation, AI, skill-list, timing, return or regeneration package.</summary>
internal static partial class RetailNpcStatCatalog
{
    private sealed partial record Package
    {
        internal FrozenDictionary<int, RetailNpcStatSkillCombat.Source> Skills { get; init; } = FrozenDictionary<int, RetailNpcStatSkillCombat.Source>.Empty;
        internal FrozenDictionary<int, RetailNpcStatPeriodicCombat.Source> Periodic { get; init; } = FrozenDictionary<int, RetailNpcStatPeriodicCombat.Source>.Empty;
        internal FrozenDictionary<int,RetailNpcStatHealCombat.Source> Heals { get; init; } = FrozenDictionary<int, RetailNpcStatHealCombat.Source>.Empty;
        internal int? MagicBoostCap { get; init; }
        internal RetailNpcStatEquipmentResponses? EquipmentResponses { get; init; }
        internal RetailNpcStatStatusSources? StatusSources { get; init; }
        internal RetailNpcCurseArmorSource? CurseArmorSource { get; init; }
        internal RetailNpcCurseOffhandSource? CurseOffhandSource { get; init; }
        internal RetailNpcCurseWeaponHpSource? CurseWeaponHpSource { get; init; }
        internal RetailNpcCurseOtherEquipmentHpSource? CurseOtherEquipmentHpSource { get; init; }
        internal RetailNpcCurseAuthorizationHpSource? CurseAuthorizationHpSource { get; init; }
        internal RetailNpcCurseItemBonusSource? CurseItemBonusSource { get; init; }
        internal RetailNpcStatSkillTimingSources? SkillTiming { get; init; }
        internal RetailNpcCurseClassPermitSource? CurseClassPermitSource { get; init; }
        internal RetailNpcCurseManastoneSource? CurseManastoneSource { get; init; }
        internal RetailNpcVampirePlayerInputs.Sources? VampirePlayerSources { get; init; }
        internal FrozenDictionary<int,RetailNpcVampirePrimaryProjection.Candidate> VampireProjections { get; init; } = FrozenDictionary<int, RetailNpcVampirePrimaryProjection.Candidate>.Empty;
        internal RetailNpcPlayerShieldCatalog? PlayerShieldSources { get; init; }
    }
    internal static int? MagicBoostCap => Volatile.Read(ref active)?.MagicBoostCap;
    internal static RetailNpcStatEquipmentResponses? EquipmentResponses=>Volatile.Read(ref active)?.EquipmentResponses;
    internal static RetailNpcStatStatusSources? StatusSources=>Volatile.Read(ref active)?.StatusSources;
    internal static RetailNpcCurseArmorSource? CurseArmorSource=>Volatile.Read(ref active)?.CurseArmorSource;
    internal static RetailNpcCurseOffhandSource? CurseOffhandSource=>Volatile.Read(ref active)?.CurseOffhandSource;
    internal static RetailNpcCurseWeaponHpSource? CurseWeaponHpSource=>Volatile.Read(ref active)?.CurseWeaponHpSource;
    internal static RetailNpcCurseOtherEquipmentHpSource? CurseOtherEquipmentHpSource=>Volatile.Read(ref active)?.CurseOtherEquipmentHpSource;
    internal static RetailNpcCurseAuthorizationHpSource? CurseAuthorizationHpSource=>Volatile.Read(ref active)?.CurseAuthorizationHpSource;
    internal static RetailNpcCurseItemBonusSource? CurseItemBonusSource=>Volatile.Read(ref active)?.CurseItemBonusSource;
    internal static RetailNpcCurseManastoneSource? CurseManastoneSource=>Volatile.Read(ref active)?.CurseManastoneSource;
    internal static RetailNpcVampirePlayerInputs.Sources? VampirePlayerSources=>Volatile.Read(ref active)?.VampirePlayerSources;
    internal static RetailNpcPlayerShieldCatalog? PlayerShieldSources=>Volatile.Read(ref active)?.PlayerShieldSources;
    internal static RetailNpcPlayerShieldSource? PlayerShieldSource=>PlayerShieldSources?.Find(1165);
    internal static RetailNpcCurseClassPermitSource? CurseClassPermitSource=>Volatile.Read(ref active)?.CurseClassPermitSource;
    internal static bool TryGetSkillTiming(Npc npc,int id,int level,out RetailNpcStatSkillTimingSources.Entry entry)
    {
        entry=null!;
        var package=Volatile.Read(ref active);
        if(package?.SkillTiming is not { } timing ||!TryGet(npc,out var profile)
            ||!package.Profiles.TryGetValue((npc.GetWorldId(),npc.GetNpcId()),out var member)
            ||!ReferenceEquals(member.Profile,profile) ||!TryGetSkill(package,profile,id,level,out _)
            ||!timing.TryGet(npc.GetWorldId(),npc.GetNpcId(),id,level,out var selected)
            ||!ReferenceEquals(Volatile.Read(ref active),package))return false;
        entry=selected;return true;
    }

    // A projection supplies only the primary. Selected single-target casts
    // must also bind timing, player inputs and the dependent curse; retrieving
    // this source alone does not admit a cast or bypass those checks.
    internal static bool TryGetReviewedVampireProjection(Npc npc,int id,int level,
        out RetailNpcVampirePrimaryProjection.Candidate candidate)
    {
        candidate=null!;var package=Volatile.Read(ref active);
        if(package?.VampirePlayerSources is null ||!TryGet(npc,out var profile)
            ||!package.Profiles.TryGetValue((npc.GetWorldId(),npc.GetNpcId()),out var member)
            ||!ReferenceEquals(member.Profile,profile)||!TryGetSkill(package,profile,id,level,out var original)
            ||original.Supported||!package.VampireProjections.TryGetValue(id,out var selected)
            ||selected.Id!=id||selected.FullSourceSha256!=original.Sha256
            ||!ReferenceEquals(Volatile.Read(ref active),package))return false;
        candidate=selected;return true;
    }

    internal static bool TryGetSkill(SkyTempleNpcProfile profile, int id, int level,
        out RetailNpcStatSkillCombat.Source skill)
        => TryGetSkill(Volatile.Read(ref active),profile,id,level,out skill);

    private static bool TryGetSkill(Package? package,SkyTempleNpcProfile profile,int id,int level,
        out RetailNpcStatSkillCombat.Source skill)
    {
        skill = null!;
        if (!profile.StatisticsOnly || package is null || !package.Skills.TryGetValue(id, out var source)
            || !MatchesSelectedSkillBinding(profile,id,level,source)) return false;
        skill=source;return true;
    }

    internal static bool MatchesSelectedSkillBinding(SkyTempleNpcProfile profile,int id,int level,
        RetailNpcStatSkillCombat.Source source)
        => MatchesDeclaredSkillBinding(profile, id, level, source.Id, source.Name);

    internal sealed record AuditSkillBinding(int MapId,int NpcId,int SkillId,int Level,bool SourceBound);
    internal sealed record AuditSnapshot(Member[] Members,int[] Maps,AuditSkillBinding[] SkillBindings,
        RetailNpcStatSkillCombat.Source[] PrimarySources,RetailNpcStatBuffs.Source[] BuffSources);

    /// <summary>Runs the production package parser and pure binding checks without
    /// publishing active state, bootstrapping a server or constructing any actors.</summary>
    internal static AuditSnapshot ReadForAudit(byte[] bytes,string hash,string directory)
    {
        var package=Parse(bytes,hash,directory);
        var members=package.Profiles.Values.OrderBy(x=>x.MapId).ThenBy(x=>x.Profile.NpcId).ToArray();
        var bindings=members.SelectMany(m=>DeclaredSkills(m.Profile).Select(s=>
            new AuditSkillBinding(m.MapId,m.Profile.NpcId,s.Id,s.Level,TryGetSkill(package,m.Profile,s.Id,s.Level,out _)))).ToArray();
        return new(members,package.Maps.Order().ToArray(),bindings,
            package.Skills.Values.OrderBy(x=>x.Id).ToArray(),package.Buffs.Values.OrderBy(x=>x.Id).ToArray());
    }

    internal static bool TryGetPeriodic(SkyTempleNpcProfile profile,int id,int level,out RetailNpcStatPeriodicCombat.Source source)
    {
        source=null!;
        return TryGetSkill(profile,id,level,out _)&&Volatile.Read(ref active) is { } package&&package.Periodic.TryGetValue(id,out source!);
    }

    internal static bool TryGetHeal(SkyTempleNpcProfile profile,int id,int level,out RetailNpcStatHealCombat.Source source)
    {
        source=null!;
        return TryGetSkill(profile,id,level,out _)&&Volatile.Read(ref active) is { } package&&package.Heals.TryGetValue(id,out source!);
    }

    static partial void ReadAdditionalPackage(JsonElement root, string expected, string directory, ref Package package)
    {
        var skills = new Dictionary<int, RetailNpcStatSkillCombat.Source>();
        var vampireProjections=new Dictionary<int,RetailNpcVampirePrimaryProjection.Candidate>();
        var periodic = new Dictionary<int,RetailNpcStatPeriodicCombat.Source>();
        var heals = new Dictionary<int,RetailNpcStatHealCombat.Source>();
        var spellConditions=root.TryGetProperty("spell_condition_context",out var spellConditionContext)
            ?RetailNpcStatSpellConditions.Parse(ReadAsset(spellConditionContext,directory),spellConditionContext.GetProperty("sha256").GetString()!):null;
        RetailNpcPlayerShieldCatalog? playerShield=null;
        if(root.TryGetProperty("player_shield_context",out var playerShieldContext))
        {
            if(playerShieldContext.ValueKind!=JsonValueKind.Object)
                throw new InvalidDataException("Invalid player shield context");
            if(playerShieldContext.TryGetProperty("records",out var selectedFamily)
                && (selectedFamily.ValueKind!=JsonValueKind.Array
                    ||playerShieldContext.EnumerateObject().Count()!=1))
                throw new InvalidDataException("Ambiguous player shield family context");
            var records=playerShieldContext.TryGetProperty("records",out var family)
                ?family.EnumerateArray().ToArray():[playerShieldContext];
            if(records.Length is <1 or >12)throw new InvalidDataException("Invalid player shield context size");
            if(selectedFamily.ValueKind==JsonValueKind.Array && records.Any(x=>
                x.ValueKind!=JsonValueKind.Object ||x.EnumerateObject().Count()!=2
                ||x.EnumerateObject().Count(p=>p.Name=="path")!=1
                ||x.EnumerateObject().Count(p=>p.Name=="sha256")!=1))
                throw new InvalidDataException("Ambiguous player shield family record");
            playerShield=new(records.Select(x=>RetailNpcPlayerShieldSource.Parse(ReadAsset(x,directory),x.GetProperty("sha256").GetString()!)));
        }
        int? magicBoostCap = root.TryGetProperty("skill_context", out var skillContext)
            ? RetailNpcStatSkillCombat.ParseBoostCapContext(ReadAsset(skillContext, directory), skillContext.GetProperty("sha256").GetString()!) : null;
        var equipmentResponses=root.TryGetProperty("equipment_response_context",out var equipmentContext)
            ?RetailNpcStatEquipmentResponses.Parse(ReadAsset(equipmentContext,directory),equipmentContext.GetProperty("sha256").GetString()!):null;
        var statusSources=root.TryGetProperty("status_context",out var statusContext)
            ?RetailNpcStatStatusSources.Parse(ReadAsset(statusContext,directory),statusContext.GetProperty("sha256").GetString()!,directory,ReadAsset):null;
        var curseArmor=root.TryGetProperty("curse_armor_context",out var curseArmorContext)
            ?RetailNpcCurseArmorSource.Parse(ReadAsset(curseArmorContext,directory),curseArmorContext.GetProperty("sha256").GetString()!):null;
        var curseOffhand=root.TryGetProperty("curse_offhand_context",out var curseOffhandContext)
            ?RetailNpcCurseOffhandSource.Parse(ReadAsset(curseOffhandContext,directory),curseOffhandContext.GetProperty("sha256").GetString()!):null;
        var curseWeaponHp=root.TryGetProperty("curse_weapon_hp_context",out var curseWeaponHpContext)
            ?RetailNpcCurseWeaponHpSource.Parse(ReadAsset(curseWeaponHpContext,directory),curseWeaponHpContext.GetProperty("sha256").GetString()!):null;
        var curseOtherHp=root.TryGetProperty("curse_other_equipment_hp_context",out var curseOtherHpContext)
            ?RetailNpcCurseOtherEquipmentHpSource.Parse(ReadAsset(curseOtherHpContext,directory),curseOtherHpContext.GetProperty("sha256").GetString()!):null;
        var curseAuthorizationHp=root.TryGetProperty("curse_authorization_hp_context",out var curseAuthorizationHpContext)
            ?RetailNpcCurseAuthorizationHpSource.Parse(ReadAsset(curseAuthorizationHpContext,directory),curseAuthorizationHpContext.GetProperty("sha256").GetString()!):null;
        var curseItemBonus=root.TryGetProperty("curse_item_bonus_context",out var curseItemBonusContext)
            ?RetailNpcCurseItemBonusSource.Parse(ReadAsset(curseItemBonusContext,directory),curseItemBonusContext.GetProperty("sha256").GetString()!):null;
        var curseManastone=root.TryGetProperty("curse_manastone_context",out var curseManastoneContext)
            ?RetailNpcCurseManastoneSource.Parse(ReadAsset(curseManastoneContext,directory),curseManastoneContext.GetProperty("sha256").GetString()!):null;
        var vampirePlayerSources=root.TryGetProperty("vampire_player_input_context",out var vampireContext)
            ?LoadVampirePlayerSources(vampireContext,directory):null;
        var curseClassPermit=root.TryGetProperty("curse_class_permit_context",out var curseClassPermitContext)
            ?RetailNpcCurseClassPermitSource.Parse(ReadAsset(curseClassPermitContext,directory),curseClassPermitContext.GetProperty("sha256").GetString()!):null;
        if (root.TryGetProperty("skill_sources", out var skillSources))
        foreach (var asset in skillSources.EnumerateArray())
        {
            byte[] raw=ReadAsset(asset,directory);string pin=asset.GetProperty("sha256").GetString()!;
            var source = RetailNpcStatSkillCombat.ParseSource(raw,pin,spellConditions);
            if (!skills.TryAdd(source.Id, source)) throw new InvalidDataException("Duplicate statistics skill source: " + source.Id);
            if(vampirePlayerSources is not null && RetailNpcVampirePrimaryProjection.IsReviewedSource(source.Id))
            {
                var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(source.Id,raw);
                if(source.Supported||candidate.FullSourceSha256!=source.Sha256
                    ||!vampireProjections.TryAdd(source.Id,candidate))
                    throw new InvalidDataException("Reviewed vampire source identity or gate differs");
            }
            var overTime=RetailNpcStatPeriodicCombat.Parse(raw,pin);
            if(overTime.Id!=source.Id||overTime.Name!=source.Name)throw new InvalidDataException("Periodic source identity differs");
            if(!overTime.Parts.IsEmpty)periodic.Add(overTime.Id,overTime);
            var heal=RetailNpcStatHealCombat.Parse(raw,pin);
            if(heal.Id!=source.Id||heal.Name!=source.Name)throw new InvalidDataException("Heal source identity differs");
            if(!heal.Parts.IsEmpty)heals.Add(heal.Id,heal);
        }
        var result = package.Profiles;
        RetailNpcStatSkillTimingSources? skillTiming=null;
        if(root.TryGetProperty("skill_timing_context",out var timingContext))
        {
            var specs=root.GetProperty("skill_sources").EnumerateArray().ToDictionary(x=>x.GetProperty("id").GetInt32());
            skillTiming=RetailNpcStatSkillTimingSources.Parse(ReadAsset(timingContext,directory),
                timingContext.GetProperty("sha256").GetString()!,result,skills,
                id=>specs.TryGetValue(id,out var spec)?ReadAsset(spec,directory):throw new InvalidDataException("Timing skill source missing"));
        }
        package = package with
        {
            Skills = skills.ToFrozenDictionary(),
            Periodic = periodic.ToFrozenDictionary(),
            Heals = heals.ToFrozenDictionary(),
            MagicBoostCap = magicBoostCap,
            EquipmentResponses = equipmentResponses,
            StatusSources = statusSources,
            CurseArmorSource = curseArmor,
            CurseOffhandSource = curseOffhand,
            CurseWeaponHpSource = curseWeaponHp,
            CurseOtherEquipmentHpSource = curseOtherHp,
            CurseAuthorizationHpSource = curseAuthorizationHp,
            CurseItemBonusSource = curseItemBonus,
            SkillTiming = skillTiming,
            CurseClassPermitSource = curseClassPermit,
            CurseManastoneSource = curseManastone,
            VampirePlayerSources = vampirePlayerSources,
            VampireProjections = vampireProjections.ToFrozenDictionary(),
            PlayerShieldSources = playerShield,
        };
    }

    // Each asset and its referenced source records are hash-checked by the
    // existing readers. An incomplete configured bundle is an error, not a
    // reason to silently use generic player inputs.
    internal static RetailNpcVampirePlayerInputs.Sources LoadVampirePlayerSources(JsonElement context,string directory)
    {
        JsonElement absolute=context.GetProperty("absolute"),items=context.GetProperty("items"),
            direct=context.GetProperty("direct"),buffs=context.GetProperty("buffs");
        string Parent(JsonElement spec)=>Path.GetDirectoryName(Path.GetFullPath(spec.GetProperty("path").GetString()!,directory))!;
        string Hash(JsonElement spec)=>spec.GetProperty("sha256").GetString()!;
        return new(
            RetailNpcPlayerCurseAbsoluteCapture.Catalog.LoadForOfflineFixture(ReadAsset(absolute,directory),Hash(absolute),Parent(absolute)),
            RetailNpcPlayerCurseItemCapture.Catalog.LoadForOfflineFixture(ReadAsset(items,directory),Hash(items),Parent(items)),
            RetailNpcPlayerCurseItemCapture.DirectCatalog.LoadForOfflineFixture(ReadAsset(direct,directory),Hash(direct)),
            RetailNpcPlayerResistanceCapture.Catalog.LoadForOfflineFixture(ReadAsset(buffs,directory),Hash(buffs),Parent(buffs)));
    }


}

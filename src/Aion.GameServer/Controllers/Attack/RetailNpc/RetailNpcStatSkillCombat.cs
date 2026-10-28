using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound primary damage for the statistics-only adapter. Existing AI,
/// selected skills, cast/impact timers, secondary effects and HP owners remain in charge.
/// Native recipe: phase3/pilot-magical-instant and pilot-physical-instant evidence;
/// broader exact source census: stats-only-20261025-01/combat-worker/skill-census.json.
/// A source-family match does not imply complete native skill/lifecycle equivalence.</summary>
internal static class RetailNpcStatSkillCombat
{
    private static readonly ILogger Log = AionLog.For(nameof(RetailNpcStatSkillCombat));
    private static readonly ConditionalWeakTable<Npc, HashSet<(int Skill, string Reason)>> CompatibilityWarnings = new();
    internal enum Kind { Unsupported, SpellInstant, PhysicalInstant, SpellDrain }
    internal sealed record Source(int Id, string Name, string Sha256, Kind Family,
        int Percent, SkillElement Element, bool ApplyBoost, bool ApplyCritical,
        int RandomType, int CriticalPercent, string? UnsupportedReason,
        int LevelRate = 0, bool UsesWeaponRange = true, bool SelfOnlyPrimary = false,
        bool BackOnly = false, int AdditionalFlat = 0, bool StumbleOnly = false, bool AreaConditional = false,
        bool NoResist = false, int AccuracyFlat = 0, int AdditionalStatusMask = 0,
        int BaseHitLevelRate = 0, int BaseHitConstant = 0, int ForceThreshold = 0,
        SkillElement PhysicalR10MetadataElement = SkillElement.NONE, int RandomMask = 1,
        RetailNpcStatSpellBonus.Program? SpellBonus = null, RetailNpcStatAerialCombat.CloseProgram? AerialClose = null,
        RetailNpcStatPhysicalBonus.Program? PhysicalBonus = null,RetailNpcStatSpellDrain.Program? Drain = null)
    {
        internal bool Supported => Family != Kind.Unsupported && UnsupportedReason is null;
        internal int ValueAtSkillLevel(int level) => LevelValue(level,LevelRate,Percent);
        internal int BaseHitAtSkillLevel(int level) => LevelValue(level,BaseHitLevelRate,BaseHitConstant);
        private static int LevelValue(int level,int rate,int constant)
        {
            if (level is < 0 or > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(level));
            // Native wrapper sign-extends the int16 level before IMUL/ADD32.
            return unchecked((short)level * rate + constant);
        }
    }

    /// <summary>Checks the complete selected record before accepting a bounded,
    /// already proved constructor/parser shape. Unsupported families remain visible
    /// compatibility data; malformed identity, XML or changed bytes cannot be admitted.</summary>
    internal static Source ParseSource(ReadOnlySpan<byte> completeUtf16LeRecord, string expectedSha256,
        RetailNpcStatSpellConditions? spellConditions=null)
    {
        var record = RetailNpcSkillRecord.Read(completeUtf16LeRecord, expectedSha256);
        var root = record.Root; int id = record.Id; string name = record.Name;
        Source Unsupported(string reason) => new(id, name, expectedSha256, Kind.Unsupported, 0,
            SkillElement.NONE, false, false, 0, 0, reason);
        try
        {
            string? Get(string tag)
            {
                var rows = root.Elements(tag).ToArray();
                if (rows.Length == 0) return null;
                if (rows.Length != 1 || rows[0].HasElements || rows[0].HasAttributes)
                    throw new RecipeBoundary("Repeated/non-leaf field: " + tag);
                return rows[0].Value;
            }
            int Number(string tag, int constructor = 0)
            {
                string? value = Get(tag);
                if (value is null) return constructor;
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed))
                    throw new RecipeBoundary("Noncanonical integer: " + tag);
                return parsed;
            }
            void Need(bool condition, string reason) { if (!condition) throw new RecipeBoundary(reason); }
            foreach (var group in root.Elements().Where(x => x.Name.LocalName.StartsWith("effect1_", StringComparison.Ordinal)).GroupBy(x => x.Name))
                Need(group.Count() == 1, "Repeated primary effect field: " + group.Key);
            string type = Get("effect1_type") ?? "";
            bool selfOnly=string.Equals(Get("first_target"),"Me",StringComparison.OrdinalIgnoreCase)
                &&string.Equals(Get("target_range"),"OnlyOne",StringComparison.OrdinalIgnoreCase)
                &&string.Equals(Get("effect1_target_type"),"Target_Only",StringComparison.OrdinalIgnoreCase);
            if (type is not ("SpellATK_Instant" or "SkillATK_Instant" or "SpellATKDrain_Instant"))
                return Unsupported("Unimplemented primary damage family: " + type);
            Need(Get("exclusive_attribute") is null && Get("damage_attenuation") is null
                && Get("is_familiar_skill") is null, "Exclusive, attenuation or familiar source context");
            int noResist=Number("effect1_noresist"),accuracyFlat=Number("effect1_acc_mod2");
            Need(noResist is 0 or 1 && Number("effect1_acc_mod1") == 0
                && (accuracyFlat==0 || type=="SkillATK_Instant" || type is "SpellATK_Instant" or "SpellATKDrain_Instant" && noResist==1)
                && Number("effect1_critical_prob_mod1") == 0
                && Number("effect1_critical_add_dmg_mod1") == 0 && Number("effect1_critical_add_dmg_mod2") == 0,
                "Unimplemented primary accuracy, critical-add or no-resist recipe");
            Need(root.Elements().Where(x => x.Name.LocalName.StartsWith("effect1_", StringComparison.Ordinal))
                .All(x => AllowedPrimaryFields.Contains(x.Name.LocalName)), "Unreviewed primary field");
            // Native Skill::Set851/852 stores these separate HoP operands;
            // SkillPhyAttack calculates HP damage before invoking its hate callback.
            // Validate their integer shape without treating them as damage inputs
            // or replacing the existing hate/AI owner (offline native review02).
            _=Number("effect1_hop_a");_=Number("effect1_hop_b");
            Need(Number("effect1_reserved17") == 0 && Number("effect1_reserved18") is 0 or 100,
                "Unreviewed Begin chance");
            int percent = Number("effect1_reserved2"), critical = Number("effect1_critical_prob_mod2", 100);
            Need(percent is >= 0 and <= 100000 && critical is >= 0 and <= 100000, "Unbounded percentage/critical recipe");
            if(type=="SpellATKDrain_Instant")
            {
                int rate=Number("effect1_reserved1"),mode=Number("effect1_reserved6");
                int hpRate=Number("effect1_reserved14"),hpPercent=Number("effect1_reserved15");
                string? range=Get("target_range");bool drainArea=range=="Area";
                // Drain uses R3/R4 for additional damage, unlike ordinary
                // SpellATK_Instant's R8/R9. Drain CheckParameter passes the
                // raw R1 pointer to FUN_140bc2020, which copies its +8/+12
                // operands to the additional-damage pair. R8/R9 are stored
                // separately; the drain primary passes a fixed additional-hit
                // chance to magical damage, and the effect manager owns the
                // sibling precondition. The reviewed vampire records use
                // R8=0/R9=20 with no primary additional damage. Their curse is
                // still independently gated below.
                int extraRate=Number("effect1_reserved3"),extraConstant=Number("effect1_reserved4");
                int effectRate=Number("effect1_reserved8"),effectChance=Number("effect1_reserved9");
                Need(rate is >=0 and <=100000 && mode is 0 or 1 && Number("effect1_reserved5") is 0 or 1
                    && effectRate==0 && (effectChance is 0 or 100 || effectChance==20 && extraRate==0 && extraConstant==0)
                    &&Number("effect1_reserved13")==0 && Number("effect1_reserved16")==0
                    &&Number("effect1_reserved19")==0&&Number("effect1_reserved20")==0&&Number("effect1_reserved21")==0
                    &&hpRate==0&&hpPercent is >=0 and <=100000, "Unreviewed spell drain operands");
                Need(Get("first_target")=="Target" && (range=="OnlyOne"||drainArea)
                    && Get("effect1_target_type")=="Target_Only" && Number("target_maxcount") is >=1 and <=150,
                    "Unreviewed spell drain target or sibling context");
                var siblings=root.Elements().Where(x=>x.Name.LocalName is var tag&&tag.Length>11
                    &&tag.StartsWith("effect",StringComparison.Ordinal)&&tag.EndsWith("_type",StringComparison.Ordinal)
                    &&tag.Substring(6,tag.Length-11).All(char.IsAsciiDigit)&&tag!="effect1_type"&&x.Value!="0").ToArray();
                RetailNpcStatSpellDrain.MpProgram? mp=null;
                RetailNpcStatSpellDrain.StunProgram? stun=null;
                RetailNpcStatSpellDrain.PeriodicSpellProgram? periodicSpell=null;
                RetailNpcStatSpellDrain.BlindProgram? blind=null;
                RetailNpcStatSpellDrain.ParalyzeProgram? paralyze=null;
                if(siblings.Length!=0 && Get("effect2_type")=="Stun")
                {
                    string[] allowed=["type","noresist","remain1","remain2","effectid","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved1","reserved2",
                        "reserved10","reserved11","reserved15","reserved16","critical_prob_mod2","hop_type","hop_a","hop_b"];
                    Need(siblings.Length==1 && siblings[0].Name.LocalName=="effect2_type"
                        && root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal))
                            .All(x=>allowed.Contains(x.Name.LocalName[8..]))
                        && Number("effect2_noresist") is 0 or 1 && Number("effect2_remain1")==0
                        && Number("effect2_remain2")==3000 && Number("effect2_effectid")==20000
                        && Get("effect2_target_type")=="Target_Only" && Get("effect2_cond_preeffect")=="e1"
                        && Number("effect2_cond_preeffect_prob1")==0 && Number("effect2_cond_preeffect_prob2")==100
                        && Number("effect2_reserved1")==0 && Number("effect2_reserved2")==0
                        && Get("effect2_reserved10")=="Earth" && Number("effect2_reserved11")==100
                        && Number("effect2_reserved15")==0 && Number("effect2_reserved16")==1
                        && Number("effect2_critical_prob_mod2",100)==100 && Number("apply_magical_critical")==1
                        && string.Equals(Get("effect2_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        && Number("effect2_hop_a")==60 && Number("effect2_hop_b")==60,
                        "Unreviewed spell drain target or sibling context");
                    stun=new(Number("effect2_noresist")==1);
                }
                else if(siblings.Length!=0 && Get("effect2_type")=="Blind")
                {
                    string[] allowed=["type","noresist","remain1","remain2","effectid","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved1","reserved2",
                        "reserved10","reserved11","critical_prob_mod2","hop_type","hop_a","hop_b"];
                    Need(siblings.Length==1 && siblings[0].Name.LocalName=="effect2_type"
                        && root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal))
                            .All(x=>allowed.Contains(x.Name.LocalName[8..]))
                        && Number("effect2_noresist")==1 && Number("effect2_remain1")==60
                        && Number("effect2_remain2")==3000 && Number("effect2_effectid")==20107
                        && Get("effect2_target_type")=="Target_Only" && Get("effect2_cond_preeffect")=="e1"
                        && Number("effect2_cond_preeffect_prob1")==0 && Number("effect2_cond_preeffect_prob2")==100
                        && Number("effect2_reserved1")==0 && Number("effect2_reserved2")==70
                        && Get("effect2_reserved10")=="Air" && Number("effect2_reserved11")==100
                        && Number("effect2_critical_prob_mod2",100)==100
                        && string.Equals(Get("effect2_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        && Number("effect2_hop_a")==60 && Number("effect2_hop_b")==60,
                        "Unreviewed spell drain blind sibling context");
                    blind=new(70);
                }
                else if(siblings.Length!=0 && Get("effect2_type")=="SpellATK")
                {
                    string[] allowed=["type","remain1","remain2","effectid","checktime","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved8","reserved9",
                        "reserved10","reserved11","reserved12","critical_prob_mod2","hop_type","hop_a","hop_b"];
                    Need(siblings.Length==1 && siblings[0].Name.LocalName=="effect2_type"
                        && root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal))
                            .All(x=>allowed.Contains(x.Name.LocalName[8..]))
                        && Number("effect2_remain1")==0 && Number("effect2_remain2")==2000
                        && Number("effect2_checktime")==1000 && Number("effect2_effectid")==10190501
                        && Get("effect2_target_type")=="Target_Only" && Get("effect2_cond_preeffect")=="e1"
                        && Number("effect2_cond_preeffect_prob1")==0 && Number("effect2_cond_preeffect_prob2")==100
                        && Number("effect2_reserved8")==0 && Number("effect2_reserved9") is >0 and <=100000
                        && Get("effect2_reserved10")=="Air" && Number("effect2_reserved11")==100
                        && Number("effect2_reserved12")==100 && Number("effect2_critical_prob_mod2",100)==100
                        && Number("apply_magical_skill_boost_bonus")==1 && Number("apply_magical_critical")==1
                        && string.Equals(Get("effect2_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        && Number("effect2_hop_a")==60 && Number("effect2_hop_b")==60,
                        "Unreviewed spell drain periodic sibling context");
                    periodicSpell=new(Number("effect2_reserved9"));
                }
                else if(siblings.Length==2 && Get("effect2_type")=="MPAttack" && Get("effect3_type")=="Paralyze")
                {
                    string[] mpFields=["type","noresist","remain1","remain2","effectid","checktime","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved6","reserved8","reserved9",
                        "reserved10","reserved11","reserved12","critical_prob_mod2","hop_type","hop_a","hop_b"];
                    string[] statusFields=["type","noresist","remain1","remain2","effectid","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved10","reserved11","reserved12",
                        "critical_prob_mod2","hop_type","hop_a","hop_b"];
                    Need(siblings.Select(x=>x.Name.LocalName).SequenceEqual(new[]{"effect2_type","effect3_type"})
                        &&root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal)).All(x=>mpFields.Contains(x.Name.LocalName[8..]))
                        &&root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect3_",StringComparison.Ordinal)).All(x=>statusFields.Contains(x.Name.LocalName[8..]))
                        &&Number("effect2_noresist")==1 && Number("effect2_remain1")==0 && Number("effect2_remain2")==3000
                        &&Number("effect2_checktime")==3000 && Number("effect2_effectid")==10182392
                        &&Get("effect2_target_type")=="Target_Only" && Get("effect2_cond_preeffect")=="e1"
                        &&Number("effect2_cond_preeffect_prob1")==0 && Number("effect2_cond_preeffect_prob2")==50
                        &&Number("effect2_reserved6")==0 && Number("effect2_reserved8")==0 && Number("effect2_reserved9")==5
                        &&Get("effect2_reserved10")=="Fire" && Number("effect2_reserved11")==100 && Number("effect2_reserved12")==100
                        &&Number("effect2_critical_prob_mod2",100)==100 && string.Equals(Get("effect2_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        &&Number("effect2_hop_a")==60 && Number("effect2_hop_b")==60
                        &&Number("effect3_noresist")==0 && Number("effect3_remain1")==0 && Number("effect3_remain2")==3000
                        &&Number("effect3_effectid")==20002 && Get("effect3_target_type")=="Target_Only"
                        &&Get("effect3_cond_preeffect")=="e2" && Number("effect3_cond_preeffect_prob1")==0 && Number("effect3_cond_preeffect_prob2")==50
                        &&Get("effect3_reserved10")=="Fire" && Number("effect3_reserved11")==100 && Number("effect3_reserved12")==100
                        &&Number("effect3_critical_prob_mod2",100)==100 && string.Equals(Get("effect3_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        &&Number("effect3_hop_a")==50 && Number("effect3_hop_b")==50 && Number("apply_magical_critical")==1,
                        "Unreviewed spell drain mana/paralyze sibling context");
                    mp=new(5,3000,3000,10182392,SkillElement.FIRE,100,50);paralyze=new();
                }
                else if(siblings.Length!=0)
                {
                    // Flat MPAttack with one preeffect and one interval: the retail
                    // CheckResist caches R8*level+R9; the host owner agrees when
                    // R8/R6 are zero and the complete target/host shape matches.
                    string[] allowed=["type","noresist","remain1","remain2","effectid","checktime","target_type",
                        "cond_preeffect","cond_preeffect_prob1","cond_preeffect_prob2","reserved6","reserved8","reserved9",
                        "reserved10","reserved11","reserved12","critical_prob_mod2","hop_type","hop_a","hop_b"];
                    Need(siblings.Length==1 && siblings[0].Name.LocalName=="effect2_type" && Get("effect2_type")=="MPAttack"
                        && root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal))
                            .All(x=>allowed.Contains(x.Name.LocalName[8..]))
                        && Number("effect2_noresist")==1 && Number("effect2_remain1")==0
                        && Number("effect2_remain2")==1100 && Number("effect2_checktime")==1000 && Number("effect2_effectid")>0
                        && Get("effect2_target_type")=="Target_Only" && Get("effect2_cond_preeffect")=="e1"
                        && Number("effect2_cond_preeffect_prob1")==0 && Number("effect2_cond_preeffect_prob2")==100
                        && Number("effect2_reserved6")==0 && Number("effect2_reserved8")==0
                        && Number("effect2_reserved9") is >0 and <=100000 && Number("effect2_reserved11") is 0 or 100
                        && Number("effect2_reserved12") is 0 or 100 && Number("effect2_critical_prob_mod2",100) is 0 or 100
                        && string.Equals(Get("effect2_hop_type"),"SkillLv",StringComparison.OrdinalIgnoreCase)
                        && Number("effect2_hop_a")==60 && Number("effect2_hop_b")==60,
                        "Unreviewed spell drain target or sibling context");
                    var mpElement=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect2_reserved10")??"") switch
                    {"water"=>SkillElement.WATER,"air"=>SkillElement.WIND,"earth"=>SkillElement.EARTH,"fire"=>SkillElement.FIRE,
                        _=>throw new RecipeBoundary("Unreviewed MP drain element")};
                    mp=new(Number("effect2_reserved9"),Number("effect2_checktime"),Number("effect2_remain2"),
                        Number("effect2_effectid"),mpElement,Number("effect2_critical_prob_mod2",100));
                }
                var pair=RetailNpcStatSpellBonus.Construct(Number("effect1_reserved3"),Number("effect1_reserved4"));
                RetailNpcStatSpellBonus.Program? bonus=null;
                if(pair is (0,0))
                    Need(Get("effect1_reserved7") is null or "0", "Unreviewed spell drain unused condition context");
                if(pair is not (0,0))
                {
                    Need(pair.Rate<=100000 && pair.Constant is >=-100000 and <=100000,
                        "Unreviewed spell drain conditional damage bounds");
                    Need(RetailNpcStatSpellBonus.TrySpellCondition(Get("effect1_reserved7"),spellConditions,out uint classes,out int? races,out int statuses,allowParalyze:true),
                        "Unreviewed spell drain additional condition");
                    Need(statuses is 0 or RetailNpcStatTargetConditions.ParalyzeMask && siblings.Length==0,
                        "Unreviewed spell drain conditional sibling or status context");
                    bonus=new(pair.Rate,pair.Constant,Number("effect1_reserved5")==1,classes,races,
                        races.HasValue?RetailNpcStatSpellConditions.ContextSha256:null,statuses);
                }
                var element=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect1_reserved10")??"") switch
                {"water"=>SkillElement.WATER,"air"=>SkillElement.WIND,"earth"=>SkillElement.EARTH,"fire"=>SkillElement.FIRE,
                    _=>throw new RecipeBoundary("Unsupported spell drain element")};
                _=Number("effect1_reserved11");_=Number("effect1_reserved12");
                // Native Skill constructor clears both flags; absent differs from
                // an unknown or malformed value and means explicitly disabled.
                Need(Get("apply_magical_skill_boost_bonus") is null or "0" or "1"&&Get("apply_magical_critical") is null or "0" or "1"
                    &&Get("sub_type") is "Attack" or "Debuff", "Unreviewed spell drain flags");
                return new(id,name,expectedSha256,Kind.SpellDrain,percent,element,Number("apply_magical_skill_boost_bonus")!=0,
                    Number("apply_magical_critical")!=0,0,critical,null,LevelRate:rate,UsesWeaponRange:mode==1,
                    NoResist:noResist==1,AccuracyFlat:accuracyFlat,SpellBonus:bonus,Drain:new(hpPercent,drainArea,mp,stun,periodicSpell,blind,paralyze));
            }
            if (type == "SpellATK_Instant")
            {
                int levelRate = Number("effect1_reserved1"), mode = Number("effect1_reserved6");
                int spellRandom=Number("effect1_reserved3");
                Need(spellRandom is >=0 and <=10,"Unreviewed spell random table");
                // InitParameter supplies BasicDmg(1); the parser writes the
                // named mask using _wcsicmp. Existing explicit raw0 also leaves
                // the constructor mask unchanged; no unknown-name fallback.
                int randomMask=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect1_reserved4")??"0") switch
                {"0" or "basicdmg"=>1,"adddmg"=>2,"all"=>3,_=>throw new RecipeBoundary("Unreviewed spell random mask")};
                Need(levelRate is >= 0 and <= 100000 && mode is 0 or 1
                    && Number("effect1_reserved5") == 0 && Number("effect1_reserved7") is 0 or 1
                    && Number("effect1_reserved19") == 0
                    && Number("effect1_reserved20") == 0 && Number("effect1_reserved21") == 0,
                    "Outside proved NPC absolute/percentage spell construction");
                var bonusPair=RetailNpcStatSpellBonus.Construct(Number("effect1_reserved8"),Number("effect1_reserved9"));
                RetailNpcStatSpellBonus.Program? bonus=null;
                if(bonusPair is not (0,0))
                {
                    Need(RetailNpcStatSpellBonus.TrySpellCondition(Get("effect1_reserved16"),spellConditions,out uint classMask,out int? raceMask,out int statusMask),
                        "Unreviewed spell additional target condition");
                    if(statusMask!=0)
                        Need(Number("effect1_reserved14")==0&&Number("effect1_reserved15")==0
                            &&!root.Elements().Any(x=>x.Name.LocalName is var tag&&tag.Length>11
                                &&tag.StartsWith("effect",StringComparison.Ordinal)&&tag.EndsWith("_type",StringComparison.Ordinal)
                                &&tag.Substring(6,tag.Length-11).All(char.IsAsciiDigit)&&tag!="effect1_type"&&x.Value!="0"),
                            "Unreviewed spell status emission or sibling context");
                    bonus=new(bonusPair.Rate,bonusPair.Constant,Number("effect1_reserved7")==1,classMask,
                        raceMask,raceMask.HasValue?RetailNpcStatSpellConditions.ContextSha256:null,statusMask);
                }
                string element = Get("effect1_reserved10") ?? "";
                // Native XML_ParseElementalType140da5450 uses _wcsicmp. Keep
                // the four proved damage channels; do not trim or invent aliases.
                SkillElement selected = RetailNpcStatCatalog.NativeSkillNameKey(element) switch
                { "water" => SkillElement.WATER, "air" => SkillElement.WIND, "earth" => SkillElement.EARTH,
                    "fire" => SkillElement.FIRE, _ => throw new RecipeBoundary("Unsupported source element: " + element) };
                // Base ParseParameter stores R11 at raw+300. Unlike the physical
                // parser, SpellATK_Instant never constructs a consumer from it:
                // Init/Parse/Check/CheckResist/Begin and SkillMagicalAttack were
                // byte-rechecked. Absence/zero is not an unverified force value.
                _=Number("effect1_reserved11");
                // Skill constructor clears both optional root flags before XML parsing.
                Need(Get("apply_magical_skill_boost_bonus") is null or "0" or "1" && Get("apply_magical_critical") is null or "0" or "1",
                    "Unreviewed source boost/critical participation");
                int boost = Number("apply_magical_skill_boost_bonus"), crit = Number("apply_magical_critical");
                Need(boost is 0 or 1 && crit is 0 or 1, "Invalid source boost/critical participation");
                Need(Get("sub_type") is "Attack" or "Debuff", "Unreviewed source subtype");
                return new(id, name, expectedSha256, Kind.SpellInstant, percent, selected, boost != 0,
                    crit != 0, spellRandom, critical, null, levelRate, mode == 1,selfOnly,
                    NoResist:noResist==1,AccuracyFlat:accuracyFlat,RandomMask:randomMask,SpellBonus:bonus);
            }
            // Physical ParseParameter uses the base numeric CRT conversion for
            // R10, not XML_ParseElementalType. The six authored element words
            // consume no decimal digits and construct zero additional damage.
            // Retain the literal's meaning ONLY to reconcile a legacy host
            // template that incorrectly assigned it as an elemental channel.
            var physicalMetadata=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect1_reserved10")??"") switch
            {"fire"=>SkillElement.FIRE,"water"=>SkillElement.WATER,"air"=>SkillElement.WIND,
                "earth"=>SkillElement.EARTH,"light"=>SkillElement.LIGHT,"dark"=>SkillElement.DARK,_=>SkillElement.NONE};
            var physicalPair=RetailNpcStatPhysicalBonus.Construct(Number("effect1_reserved9"),
                physicalMetadata==SkillElement.NONE?Number("effect1_reserved10"):0);
            int additional=physicalPair.Constant;
            bool hasAdditional=physicalPair is not (0,0);
            // Physical R7 is stored as a byte, but neither SkillPhyAttack nor
            // its damage calculator reads it. Both authored0/1 use flat R9/10;
            // the spell R7 percentage interpretation does not apply here.
            Need(Number("effect1_reserved7") is 0 or 1,"Unreviewed physical additional metadata");
            bool back=hasAdditional && Get("effect1_reserved16")=="_Back";
            bool stumble=hasAdditional
                && string.Equals(Get("effect1_reserved16"),"stumble",StringComparison.OrdinalIgnoreCase);
            int periodicStatusMask=Get("effect1_reserved16")?.ToLowerInvariant() switch
            {"poison"=>RetailNpcStatTargetConditions.PoisonMask,"bleed"=>RetailNpcStatTargetConditions.BleedMask,
                "openaerial"=>RetailNpcStatTargetConditions.OpenAerialMask,_=>0};
            bool periodicStatus=hasAdditional&&periodicStatusMask!=0;
            if(!periodicStatus)periodicStatusMask=0;
            RetailNpcStatPhysicalBonus.Program? physicalBonus=null;
            if(hasAdditional)
            {
                uint classes=0;int? races=null;
                bool playerPredicate=!(back||stumble||periodicStatus);
                if(playerPredicate)
                    Need(RetailNpcStatSpellBonus.TryCondition(Get("effect1_reserved16"),spellConditions,out classes,out races),
                        "Unreviewed physical additional target condition");
                physicalBonus=new(physicalPair.Rate,physicalPair.Constant,playerPredicate,classes,races,
                    races.HasValue?RetailNpcStatSpellConditions.ContextSha256:null);
            }
            bool aerial=periodicStatusMask==RetailNpcStatTargetConditions.OpenAerialMask;
            RetailNpcStatAerialCombat.CloseProgram? aerialClose=null;
            if(aerial && Get("effect2_type") is not (null or "0"))
            {
                Need(root.Elements().Where(x=>x.Name.LocalName.StartsWith("effect2_",StringComparison.Ordinal))
                    .All(x=>RetailNpcStatAerialCombat.CloseFields.Contains(x.Name.LocalName)),"Unreviewed aerial secondary field");
                Need(Get("effect2_type")=="CloseAerial" && Number("effect2_noresist")==1 && Number("effect2_remain1")==0
                    &&Get("effect2_target_type")=="Target_Only" &&Get("effect2_cond_preeffect")=="e1"
                    &&Number("effect2_cond_preeffect_prob1")==0&&Number("effect2_cond_preeffect_prob2")==100
                    &&Number("effect2_cond_status_prob1")==0&&Get("effect2_hop_type")=="SkillLv",
                    "Unreviewed aerial secondary construction");
                bool status=string.Equals(Get("effect2_cond_status"),"OpenAerial",StringComparison.OrdinalIgnoreCase);
                Need(status?Number("effect2_cond_status_prob2")==100:
                    Get("effect2_cond_status") is null&&Number("effect2_cond_status_prob2")==0,"Unreviewed aerial secondary status chance");
                var closeElement=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect2_reserved10")??"") switch
                    {"water"=>SkillElement.WATER,"fire"=>SkillElement.FIRE,"air"=>SkillElement.WIND,"earth"=>SkillElement.EARTH,
                        _=>throw new RecipeBoundary("Unreviewed aerial secondary element")};
                aerialClose=new(closeElement,status,Number("effect2_hop_a"),Number("effect2_hop_b"));
            }
            bool area=hasAdditional && !back && Get("target_range")=="Area";
            int physicalLevelRate=Number("effect1_reserved1");
            int forceThreshold=Number("effect1_reserved11");
            int baseHitLevelRate=Number("effect1_reserved3"),baseHitConstant=Number("effect1_reserved4");
            Need(baseHitLevelRate is >=0 and <=100000 && baseHitConstant is >=0 and <=100000,
                "Unbounded physical baseHit pair");
            // Native hitMult R1/R2 construction and the signed16 level IMUL/ADD
            // precede the per-target calculation for every physical primary.
            // Range and additional-state selectors do not select another formula.
            Need(physicalLevelRate is >=0 and <=100000
                && forceThreshold>=0 && Number("effect1_reserved19") == 0,
                "Conditional additional, force, condition or hand-state physical recipe");
            int random = Number("effect1_reserved8");
            int physicalRandomMask=RetailNpcStatCatalog.NativeSkillNameKey(Get("effect1_reserved12")??"0") switch
            {"0" or "basicdmg"=>1,"adddmg"=>2,"all"=>3,_=>throw new RecipeBoundary("Unreviewed physical random mask")};
            Need(random is >= 0 and <= 10,"Unreviewed physical random table");
            if(hasAdditional)
            {
                // Native per-target condition48/mask1. Area dispatch
                // separately invokes this program for each target; R19=0 keeps
                // sharing divisor1. Count is a collector limit, not a divisor.
                // _Back retains its existing single-target scope. No ID whitelist.
                _=Number("effect1_reserved5"); // Canonical operand only; this is not named accuracy.
                // First-target Me selects the center of an AREA collector.
                // The supplied normal caller still invokes this same physical
                // program separately for each collected actor; R19=0 keeps divisor1.
                // This adapter leaves that collector and its geometry unchanged.
                Need((Get("first_target")=="Target" || area&&string.Equals(Get("first_target"),"Me",StringComparison.OrdinalIgnoreCase))
                    && Number("effect1_reserved6")==100 && Number("effect1_reserved13")==0
                    && Number("effect1_reserved20")==0 && Number("effect1_reserved21")==0
                    && (area ? Number("target_maxcount") is >=1 and <=120 && string.Equals(Get("sub_type"),"Attack",StringComparison.OrdinalIgnoreCase)
                        : Get("target_range")=="OnlyOne" && Number("target_maxcount")==1)
                    && Get("effect1_target_type")=="Target_Only" && Get("effect1_reserved14") is null or "0"
                    && Get("effect1_reserved15") is null or "0"
                    && !root.Elements().Any(x=>x.Name.LocalName is var tag && tag.Length>11
                        &&tag.StartsWith("effect",StringComparison.Ordinal)&&tag.EndsWith("_type",StringComparison.Ordinal)
                        &&tag.Substring(6,tag.Length-11).All(char.IsAsciiDigit)&&tag!="effect1_type"
                        &&!(tag=="effect2_type"&&aerialClose is not null)&&x.Value!="0"),
                    "Unreviewed conditional target, child, sibling or random context");
            }
            // R14/15 later status emission and sibling effects retain their existing
            // 4.8 owners; with additional==0, R16 cannot alter primary numeric damage.
            return new(id, name, expectedSha256, Kind.PhysicalInstant, percent, SkillElement.NONE,
                false, true, random, critical, null,LevelRate:physicalLevelRate,SelfOnlyPrimary:selfOnly,
                BackOnly:back,AdditionalFlat:additional,StumbleOnly:stumble,AreaConditional:area,
                NoResist:noResist==1,AccuracyFlat:accuracyFlat,AdditionalStatusMask:periodicStatusMask,
                BaseHitLevelRate:baseHitLevelRate,BaseHitConstant:baseHitConstant,ForceThreshold:forceThreshold,
                PhysicalR10MetadataElement:physicalMetadata,RandomMask:physicalRandomMask,AerialClose:aerialClose,PhysicalBonus:physicalBonus);
        }
        catch (RecipeBoundary e) { return Unsupported(e.Message); }
    }
    private static readonly HashSet<string> AllowedPrimaryFields = new(StringComparer.Ordinal)
    {
        "effect1_type", "effect1_target_type", "effect1_G1", "effect1_G2", "effect1_hop_type", "effect1_hop_a", "effect1_hop_b",
        "effect1_noresist", "effect1_acc_mod1", "effect1_acc_mod2", "effect1_critical_prob_mod1", "effect1_critical_prob_mod2",
        "effect1_critical_add_dmg_mod1", "effect1_critical_add_dmg_mod2",
        "effect1_reserved1", "effect1_reserved2", "effect1_reserved3", "effect1_reserved4", "effect1_reserved5",
        "effect1_reserved6", "effect1_reserved7", "effect1_reserved8", "effect1_reserved9", "effect1_reserved10",
        "effect1_reserved11", "effect1_reserved12", "effect1_reserved13", "effect1_reserved14", "effect1_reserved15",
        "effect1_reserved16", "effect1_reserved17", "effect1_reserved18", "effect1_reserved19", "effect1_reserved20", "effect1_reserved21"
    };
    private sealed class RecipeBoundary(string message) : Exception(message);

    internal static int SpellBase(int sample, int percent, int boostDelta, uint defenderRatio, bool applyBoost) =>
        SkyTempleNpcSkillCombat.MagicBase(sample, percent, applyBoost ? boostDelta : 0, defenderRatio);

    // Physical wrapper140bb8d90 supplies named acc_mod1/2 separately from R5.
    // Helper14036d0f0 adds it after the current/one-time bonus, then clamps.
    // Selected recipes currently have acc_mod1=0; nonzero level rates stay guarded.
    internal static int PhysicalAccuracy(int current,int oneTimeBonus,int sourceFlat)=>
        Math.Clamp(unchecked(current+oneTimeBonus+sourceFlat),0,9999);

    internal static bool PhysicalHostMatches(Source source,EffectTemplate template)=>source.Supported
        &&source.Family==Kind.PhysicalInstant&&template.GetType()==typeof(SkillAttackInstantEffect)&&template.GetPosition()==1
        &&(template.GetElement()==SkillElement.NONE||source.PhysicalR10MetadataElement!=SkillElement.NONE
            &&template.GetElement()==source.PhysicalR10MetadataElement);

    // Source/default reconstruction for the supplied ordinary server, not a
    // runtime-global observation. The context is independent of country selection.
    internal const string OrdinaryBoostCapContextSha256 = "9cb03cf864293e481f7a8aff76fc6329855cc3866579bc4e964bbe3aea0b688e";
    internal static int ParseBoostCapContext(ReadOnlySpan<byte> bytes, string expectedSha256)
    {
        if (bytes.Length is <= 0 or > 32768 || expectedSha256 != OrdinaryBoostCapContextSha256
            || Convert.ToHexStringLower(SHA256.HashData(bytes)) != OrdinaryBoostCapContextSha256)
            throw new InvalidDataException("Unreviewed statistics skill boost-cap context");
        using var document = JsonDocument.Parse(bytes.ToArray());
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1
            || root.GetProperty("kind").GetString() != "supplied-ordinary-server-special-type0-v1"
            || root.GetProperty("main_sha256").GetString() != RetailNpcNormalScope.MainBinarySha256
            || root.GetProperty("special_server_type").GetInt32() != 0
            || root.GetProperty("magical_boost_cap").GetInt32() != 3400)
            throw new InvalidDataException("Statistics skill cap source context differs");
        return 3400;
    }

    /// <summary>Complete NPC/R6 boost base with an explicitly selected native cap.
    /// Clamp before and after ratio normalization; integer division by1000 precedes
    /// binary32 conversion. The old finite pilot API remains unchanged.</summary>
    internal static int StatsSpellBase(int sample, int percent, int boostDelta, uint defenderRatio,
        bool applyBoost, int selectedCap)
    {
        if (sample is < 0 or > 65535 || percent is < 0 or > 100000 || defenderRatio is < 1000 or > 65000
            || selectedCap is not (2900 or 3400)) throw new ArgumentOutOfRangeException(nameof(sample));
        int normalized = applyBoost ? NormalizeSpellBoost(boostDelta,defenderRatio,selectedCap) : 0;
        int quotient = normalized / 1000;
        float coefficient = F.Add(F.Div(percent, 100f), 1f);
        return Math.Max(0, F.Cvtt(F.Add(F.Mul(sample, F.Mul(coefficient, F.Add(quotient, 1f))), 0f)));
    }

    /// <summary>Native NPC R6=0 uses absolute level*R1+R2 and continuous boost,
    /// with neutral Knowledge. It consumes no weapon-range draw.</summary>
    internal static int AbsoluteSpellBase(int sourceValue, int boostDelta, uint defenderRatio, bool applyBoost, int selectedCap)
    {
        if (sourceValue is < 0 or > 100000 || defenderRatio is < 1000 or > 65000
            || selectedCap is not (2900 or 3400)) throw new ArgumentOutOfRangeException(nameof(sourceValue));
        int normalized = applyBoost ? NormalizeSpellBoost(boostDelta,defenderRatio,selectedCap) : 0;
        float coefficient = F.Add(F.Div(normalized, 1000f), 1f);
        return Math.Max(0, F.Cvtt(F.Add(F.Mul(sourceValue, coefficient), 0f)));
    }

    // Shared native input stage; both the PC continuous branch and the NPC
    // absolute/range branches clamp before and after target-ratio normalization.
    internal static int NormalizeSpellBoost(int boostDelta,uint defenderRatio,int selectedCap)
    {
        if(defenderRatio is < 1000 or > 65000 || selectedCap is not (2900 or 3400))
            throw new ArgumentOutOfRangeException(nameof(defenderRatio));
        int bounded=Math.Clamp(boostDelta,0,selectedCap);
        return Math.Clamp(F.Cvtt(F.Div(F.FromInt(bounded),F.Div(defenderRatio,1000f))),0,selectedCap);
    }

    // PC type1 always takes the absolute/continuous branch. Its integer base
    // and post-Knowledge scalar conversion precede BOOST_SPELL_ATTACK/defense;
    // NPC R6 weapon sampling and integer boost quotients do not apply to a PC.
    internal static int PlayerSpellBase(float baseValue,int knowledge,int normalizedBoost)
    {
        if(knowledge is < 0 or > 999 || normalizedBoost is < 0 or > 3400)
            throw new ArgumentOutOfRangeException(nameof(knowledge));
        if(!float.IsFinite(baseValue)||baseValue!=MathF.Truncate(baseValue))
            throw new ArgumentException("The native PC primary consumer requires an absolute integer base",nameof(baseValue));
        float knowledgeFactor=F.Add(F.Div(F.FromInt(knowledge-100),100f),1f);
        float factor=F.Add(knowledgeFactor,F.Div(F.FromInt(normalizedBoost),1000f));
        return Math.Max(0,F.Cvtt(F.Add(F.Mul(F.FromInt(F.Cvtt(baseValue)),factor),0f)));
    }

    internal static float PhysicalBase(int sample, int percent, SkyTempleNpcPhysicalSkills.Modifiers modifiers,
        int randomType, int randomWord, bool dual = false, int baseHit = 0)
    {
        if (sample is < 0 or > 65535 || randomType is < 0 or > 10
            || randomWord is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(sample));
        float coefficient = F.Add(modifiers.Rate, 1f);
        if(dual)coefficient=F.Add(coefficient,modifiers.Rate);
        coefficient=F.Add(coefficient,F.Mul(percent,BitConverter.UInt32BitsToSingle(0x3c23d70a)));
        int flat=dual?unchecked(modifiers.Flat+modifiers.Flat):modifiers.Flat;
        // Native baseHit R3/R4 joins the current flat modifier in signed32
        // before their single float conversion. Only the modifier has two hands.
        flat=unchecked(flat+baseHit);
        float amount = F.Add(F.Mul(sample, coefficient), flat);
        if (randomType != 0)
        {
            // Selected skill_randomdamage.xml (SHA256 1901847166c70e03258072a3
            // 1737d3e5d43ec7bf0eb39634acfabb2e5d53eacb) has twenty five-roll
            // buckets per row. The native parser accepts exactly0..10.
            // Randomize the basic damage before conditional flat additions.
            int factor = RandomFactor(randomType,randomWord);
            amount = F.Mul(amount, F.Div(factor, 100f));
        }
        return amount;
    }

    internal static int RandomFactor(int randomType,int randomWord)=>randomType switch
    {
        1=>randomWord<35?50:randomWord<65?100:150,
        2=>randomWord<70?60:200,
        3=>randomWord<35?90:randomWord<65?100:110,
        6=>randomWord<70?100:200,
        4 or 5 or 7 or 8 or 9 or 10=>100,
        _=>throw new ArgumentOutOfRangeException(nameof(randomType))
    };

    // CalcMagicalSkillAttackDamage first CVTT/clamps its base, then randomizes
    // that integer and CVTTs again before BOOST_SPELL_ATTACK/armor. With the
    // separate bonus program, AddDmg(2) consumes no BASIC table draw.
    internal static int SpellRandomBasic(int amount,int randomType,int mask,Func<int,int,int> draw)
    {
        if(amount<0||randomType is <0 or >10||mask is <1 or >3)throw new ArgumentOutOfRangeException(nameof(amount));
        if(randomType==0||(mask&1)==0)return amount;
        int word=draw(0,99);
        if(word is <0 or >99)throw new InvalidOperationException("Spell random sample outside native endpoints");
        return F.Cvtt(F.Mul(F.Div(RandomFactor(randomType,word),100f),F.FromInt(amount)));
    }

    internal static float StatisticsPhysicalBase(int sample, int percent, RetailNpcStatNormalInputs.Modifiers modifiers,
        int randomType, int randomWord, bool dual = false, int baseHit = 0) => PhysicalBase(sample, percent,
            new SkyTempleNpcPhysicalSkills.Modifiers(F.Div(F.FromInt(modifiers.Percent), 100f), modifiers.Flat), randomType, randomWord,dual,baseHit);

    internal static float BackAdditional(float amount,int additional,bool behind,System.Func<int,int,int> draw)
        => ConditionalAdditional(amount,additional,behind,draw);

    internal static float ConditionalAdditional(float amount,int additional,bool applies,System.Func<int,int,int> draw)
        => ConditionalAdditional(amount,additional,applies,draw,out _);

    internal static float ConditionalAdditional(float amount,int additional,bool applies,System.Func<int,int,int> draw,out int? originalDraw)
    {
        var result=RetailNpcStatPhysicalBonus.Apply(amount,additional,applies,0,1,draw);
        originalDraw=result.ChanceDraw;
        return result.Damage;
    }

    internal readonly record struct WeaponSample(int Minimum,int Maximum,int Sample);

    /// <summary>Ordinary NPC current914=0: physical skills double the split
    /// endpoints BEFORE one draw; R6 spells use only the split main hand.
    /// Full producer/caller proof: stats-only-20261025-01/dual-skill-worker/.
    /// This emits one primary sample, never two normal-attack hand records.</summary>
    internal static WeaponSample DrawSkillWeapon(int minimum,int maximum,bool dual,bool physical,
        System.Func<int,int,int> draw)
    {
        if(minimum is < 0 or > ushort.MaxValue || maximum is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(minimum));
        if(dual)
        {
            var main=RetailNpcDualNormalPlan.Split(minimum,maximum,1).Main;
            minimum=physical?main.Minimum*2:main.Minimum;
            maximum=physical?main.Maximum*2:main.Maximum;
        }
        int sample=draw(minimum,maximum);
        if(sample<Math.Min(minimum,maximum)||sample>Math.Max(minimum,maximum))
            throw new InvalidOperationException("Skill weapon sample outside native endpoints");
        return new(minimum,maximum,sample);
    }

    /// <summary>CalcMagicalSkillAttackDamage mask1 consumes BoostSpellAttackEffect
    /// actor11c4/11c8, not ordinary magicalAttack. ADD32(percent,100), CVT, DIV,
    /// MUL, ADD independent flat, CVTT; no flat/rate cross-term.</summary>
    internal static int ApplySpellAttackModifiers(int amount, RetailNpcStatNormalInputs.Modifiers modifiers)
    {
        float coefficient = F.Div(F.FromInt(unchecked(modifiers.Percent + 100)), 100f);
        return F.Cvtt(F.Add(F.Mul(coefficient, F.FromInt(amount)), F.FromInt(modifiers.Flat)));
    }
    internal static bool TryCalculate(Effect effect, EffectTemplate template, SpellStatus? spellStatus, out bool success)
    {
        success = false;
        if (template.GetPosition() != 1 || template is not DamageEffect || effect.GetEffector() is not Npc npc
            || !RetailNpcStatCatalog.TryGet(npc, out var profile)) return false;
        bool Compatibility(string reason)
        {
            var warnings = CompatibilityWarnings.GetValue(npc, _ => []);
            lock (warnings)
                if (warnings.Add((effect.GetSkillId(), reason)))
                    Log.LogWarning("RETAIL_STAT_SKILL_COMPAT npc={Npc} skill={Skill} reason={Reason}; existing 4.8 skill calculation retained",
                        profile.NpcId, effect.GetSkillId(), reason);
            return false;
        }
        if (effect.GetEffected() is not Player target) return Compatibility("non-player-target");
        if (!RetailNpcStatCatalog.TryGetSkill(profile, effect.GetSkillId(), effect.GetSkillLevel(), out var source))
            return Compatibility("missing-exact-source-binding");
        if (!source.Supported) return Compatibility(source.UnsupportedReason ?? "unimplemented-source-family");
        if(source.SpellBonus?.PlayerRaceMask is not null && target.GetRace() is not (Race.ELYOS or Race.ASMODIANS))
            return Compatibility("unreviewed-spell-condition-player-race-before-calculation");
        if(source.PhysicalBonus?.PlayerRaceMask is not null && target.GetRace() is not (Race.ELYOS or Race.ASMODIANS))
            return Compatibility("unreviewed-physical-condition-player-race-before-calculation");
        if(source.SpellBonus?.StatusMask is >0 && effect.GetEffectTemplates().Count()!=1)
            return Compatibility("spell-status-source-host-sibling-drift-before-calculation");
        if(source.Family==Kind.SpellDrain && source.SpellBonus is { } drainBonus
            && ((long)effect.GetSkillLevel()*drainBonus.Rate+drainBonus.Constant is <0 or >100000))
            return Compatibility("unreviewed-drain-conditional-damage-level");
        if(!NoResistRecordSupported(source,effect.GetAttackStatus()))
            return Compatibility("unreviewed-noresist-preforced-record-before-calculation");
        if(!CriticalOwnerSupported(source,npc.GetObserveController()))
            return Compatibility("unreviewed-critical-observer-owner-before-consumption");
        if(source.BackOnly && (effect.GetSkillTemplate().GetProperties()?.GetTargetType()!=Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE
            ||target.GetHeading()>=120 ||new[]{npc.GetX(),npc.GetY(),target.GetX(),target.GetY()}.Any(x=>!float.IsFinite(x)||Math.Abs(x)>1000000)))
            return Compatibility("conditional-current-geometry-or-host-target-context");
        if((source.StumbleOnly||source.AdditionalStatusMask!=0||source.PhysicalBonus is not null) && effect.GetSkillTemplate().GetProperties()?.GetTargetType()!=
            (source.AreaConditional ? Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.AREA
                : Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE))
            return Compatibility("conditional-status-host-target-context");
        if(!RetailNpcStatAerialCombat.Preflight(effect,source,out string aerialReason))return Compatibility(aerialReason);
        // The verified ordinary ownerless NPC preparation is one physical
        // normal/modifier100/hit1. Do not invent current hand state for future
        // mixed/magical profiles. Absolute spells do not consume these fields.
        if (profile.Source.Integers["dual"].ParsedValue is not (null or 0)
            && (source.Family is not (Kind.SpellInstant or Kind.SpellDrain) || source.UsesWeaponRange)
            && (profile.Attacks.Length!=1||!profile.Attacks[0].Type.Equals("physical",StringComparison.OrdinalIgnoreCase)
                ||profile.Attacks[0].Modifier!=100||profile.Source.Integers["hit_count"].ParsedValue!=1))
            return Compatibility("source-dual-current-hand-preparation-unverified");
        if (effect.GetSkillLevel() is < 0 or > short.MaxValue
            || source.ValueAtSkillLevel(effect.GetSkillLevel()) is < 0 or > 100000
            || source.BaseHitAtSkillLevel(effect.GetSkillLevel()) is < 0 or > 100000)
            return Compatibility("source-level-adjusted-scalar-outside-reviewed-bound");
        // Numeric source overrides are separate from HP/defense profile availability.
        // Source identity is already checked by the catalog; unsupported scalar branches
        // remain on their explicitly recorded 4.8 compatibility skill path.
        RetailNpcStatEquipmentResponses.Handle? equipmentResponse=null;
        if (RetailNpcStatEquipmentResponses.Required(profile))
        {
            if(!RetailNpcStatEquipmentResponses.TryPrepare(profile,target,out equipmentResponse,out string responseReason))return Compatibility(responseReason);
        }
        if (source.Family is Kind.SpellInstant or Kind.SpellDrain
            && (source.Family==Kind.SpellInstant ? template.GetType()==typeof(SpellAttackInstantEffect)
                : template.GetType()==typeof(SpellAtkDrainInstantEffect))
            && template.GetElement() == source.Element)
        {
            if(source.Drain is { } drain)
            {
                var drainTemplates=effect.GetEffectTemplates();
                bool hasSecondary=drain.Mp is not null || drain.Stun is not null || drain.PeriodicSpell is not null || drain.Blind is not null || drain.Paralyze is not null;
                if(hasSecondary&&!RetailNpcStatSpellDrain.SecondaryContextMatches(effect))
                    return Compatibility("unreviewed-drain-secondary-forced-or-first-target-context");
                bool hostShape;
                if(!hasSecondary)hostShape=drainTemplates.Count==1;
                else
                {
                    var second=drainTemplates.Where(x=>x.Position==2).ToArray();
                    var third=drainTemplates.Where(x=>x.Position==3).ToArray();
                    hostShape=drain.Paralyze is not null
                        ? drainTemplates.Count==3 && second.Length==1 && third.Length==1 && drain.Mp is not null
                            && drain.Stun is null && drain.PeriodicSpell is null && drain.Blind is null
                            && RetailNpcStatSpellDrain.MpHostMatches(drain.Mp,second[0]) && RetailNpcStatSpellDrain.ParalyzeHostMatches(third[0])
                        : drainTemplates.Count==2 && second.Length==1 && (drain.Mp is not null
                        ? drain.Stun is null && drain.PeriodicSpell is null && drain.Blind is null && RetailNpcStatSpellDrain.MpHostMatches(drain.Mp,second[0])
                        : drain.Stun is not null ? drain.PeriodicSpell is null && drain.Blind is null && RetailNpcStatSpellDrain.StunHostMatches(drain.Stun,second[0])
                        : drain.PeriodicSpell is not null ? drain.Blind is null && RetailNpcStatSpellDrain.PeriodicSpellHostMatches(drain.PeriodicSpell,second[0])
                        : drain.Blind is not null && RetailNpcStatSpellDrain.BlindHostMatches(drain.Blind,second[0]));
                }
                if(!hostShape || effect.GetSkill() is not { } skill
                    ||skill.GetHitTime() is <0 or >ushort.MaxValue
                    || effect.GetSkillTemplate().GetProperties()?.GetTargetType()!=
                        (drain.Area?Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.AREA:Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE))
                    return Compatibility("unreviewed-drain-host-target-or-hit-delay");
                if(drain.PeriodicSpell is { } periodic)
                {
                    if(!RetailNpcStatCatalog.TryGetPeriodic(profile,effect.GetSkillId(),effect.GetSkillLevel(),out var periodicSource)
                        ||periodicSource.Sha256!=source.Sha256)
                        return Compatibility("missing-bound-drain-periodic-source");
                    var part=periodicSource.Parts.SingleOrDefault(x=>x.Position==2);
                    if(part is null || !part.Supported || part.Family!=RetailNpcPeriodicSpellParameters.Family.SpellAttack
                        ||part.Value!=periodic.Value || part.Delta!=0 || part.Interval!=1000 || part.Element!=SkillElement.WIND
                        ||!part.ApplyBoost || !part.ApplyMagicalDefense || npc.GetObserveController().HasSkillCriticalObserver
                        ||RetailNpcStatEquipmentResponses.Required(profile))
                        return Compatibility("unreviewed-drain-periodic-current-owner");
                }
                if(drain.Blind is not null && target.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND))
                    return Compatibility("unreviewed-drain-blind-overlap");
                if(drain.Paralyze is not null && (effect.GetSkillTemplate().GetPvpDuration()!=0
                    || (effect.GetDuration()!=0 && effect.GetDuration()!=4000) || target.GetTransformModel().IsActive()))
                    return Compatibility("unreviewed-drain-paralyze-duration-or-transformation");
                if(drain.Paralyze is not null && target.GetEffectController().IsInAnyAbnormalState(AbnormalState.PARALYZE))
                    return Compatibility("unreviewed-drain-paralyze-overlap");
                if(!target.GetObserveController().CanCalculateRetailPlayerDrain(target))
                    return Compatibility("unreviewed-drain-protection-owner");
            }
            // Both proved loaded-cap branches agree below 2900. Reject before
            // resist RNG or any effect charge when the unknown selector matters.
            int? selectedCap = RetailNpcStatCatalog.MagicBoostCap;
            if (source.ApplyBoost && selectedCap is null && npc.GetGameStats().GetMBoost().GetCurrent()
                - target.GetGameStats().GetMBResist().GetCurrent() >= 2900)
                return Compatibility("missing-source-bound-native-boost-cap-context");
            // At ratio1000, both cap branches coincide below2900. Null context
            // therefore remains useful for the earlier low-boost packages only.
            RetailNpcStatNormalInputs.Modifiers spellModifiers;
            try { spellModifiers = RetailNpcStatNormalInputs.CurrentModifiers(npc, StatEnum.BOOST_SPELL_ATTACK); }
            catch (InvalidOperationException e)
            { return Compatibility("unimplemented-current-spell-boost-operator: " + e.Message); }
            bool handled=CalculateSpell(effect, template, spellStatus, npc, target, profile, source, selectedCap ?? 2900, spellModifiers,equipmentResponse, out success,out var drainProtection);
            if(handled&&success&&source.Drain is { } program)RetailNpcStatSpellDrain.Prepare(effect,template,program,drainProtection);
            return handled;
        }
        if (PhysicalHostMatches(source,template))
        {
            RetailNpcStatNormalInputs.Modifiers modifiers;
            try { modifiers = RetailNpcStatNormalInputs.CurrentModifiers(npc, true); }
            catch (InvalidOperationException e)
            { return Compatibility("unimplemented-current-physical-operator: " + e.Message); }
            if(!source.NoResist&&(source.ForceThreshold!=0||RetailNpcStatCatalog.StatusSources is not null))
            {
                // Ordinary managed requests map to native RunInfo58=0. A host
                // ForceType does not establish an arbitrary native signed threshold.
                if(effect.IsForcedEffect())return Compatibility("unmapped-current-force-context");
                if(RetailNpcStatCatalog.StatusSources is null)return Compatibility("missing-retail-status-source-context");
                if(!RetailNpcStatPhysicalStatuses.Preflight(npc,target,out string statusReason))return Compatibility(statusReason);
            }
            // All source/owner/host preflights passed. Keep this particular
            // effect's miss classification physical even when the shared4.8
            // template contains the erroneous element word. Do not mutate it.
            RetailNpcStatAerialCombat.Prepare(effect,source);
            effect.MarkRetailNpcPhysicalPrimary();
            return CalculatePhysical(effect, template, spellStatus, npc, target, profile, source, modifiers,equipmentResponse, out success);
        }
        return Compatibility("host-primary-family-or-element-differs");
    }


    /// <summary>Exercise the pinned 17860 primary with actual host actors.
    /// The dependent StatDown is not applied by this fixture, and the full
    /// source remains rejected by the production catalog.</summary>
    internal static bool TryPrepare17860PrimaryForOfflineFixture(Effect effect,
        RetailNpc17860PrimaryProjection.Candidate candidate,Func<int,int,int> draw,out bool success)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(draw);success=false;
        if(effect.RetailHasEnded ||effect.GetSuccessEffects().Count!=0
            ||effect.GetEffector() is not Npc npc ||effect.GetEffected() is not Player target
            ||!npc.IsSpawned() ||npc.IsDead() ||!target.IsSpawned() ||target.IsDead()
            ||!ReferenceEquals(npc.GetWorldMapInstance(),target.GetWorldMapInstance())
            ||!RetailNpcStatCatalog.TryGet(npc,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,17860,effect.GetSkillLevel(),out var original)
            ||effect.GetSkillId()!=17860 ||original.Supported
            ||original.Sha256!="52953fccf5f044eb35e7ab1fdebb06af5ff5948ccd67dacecf6aacb855367ee2"
            ||candidate.Primary.Id!=17860 ||candidate.Primary.Drain is not {HpPercent:200} drain
            ||candidate.Primary.SpellBonus is not {Rate:0,Constant:250,Percent:true,StatusMask:RetailNpcStatTargetConditions.ParalyzeMask}
            ||effect.GetEffectTemplates() is not {Count:2} templates
            ||templates[0] is not SpellAtkDrainInstantEffect primary ||primary.Position!=1
            ||templates[1] is not StatdownEffect ||primary.Element!=candidate.Primary.Element
            ||primary.hpPercent!=200 ||primary.mpPercent!=0
            ||effect.GetSkill() is not { } skill ||skill.GetHitTime() is <0 or >ushort.MaxValue
            ||!RetailNpcStatSpellDrain.SecondaryContextMatches(effect)
            ||target.GetObserveController().HasDrainProtectionObserver
            ||npc.GetObserveController().HasSkillCriticalObserver
            ||RetailNpcStatEquipmentResponses.Required(profile))return false;
        var selected=candidate.Primary with {Sha256=original.Sha256};
        var modifiers=RetailNpcStatNormalInputs.CurrentModifiers(npc,StatEnum.BOOST_SPELL_ATTACK);
        bool handled=CalculateSpell(effect,primary,null,npc,target,profile,selected,
            RetailNpcStatCatalog.MagicBoostCap??2900,modifiers,null,out success,out _,draw);
        if(handled && success)RetailNpcStatSpellDrain.Prepare(effect,primary,drain);
        return handled;
    }

    /// <summary>Prepare a selected vampire primary and its dependent curse.
    /// The single-target production bridge supplies source-bound player inputs;
    /// explicit test plans and deterministic rolls remain fixture-only.</summary>
    internal static bool TryPrepareVampireForOfflineFixture(Effect effect,
        RetailNpcVampirePrimaryProjection.Candidate candidate,RetailNpcCurseResourceOwner.Plan? resources,
        Func<int,int,int> draw,out bool success,Func<Player,Effect,RetailNpcCurseResourceOwner.Plan?>? fixturePlan=null,
        Func<Player,RetailNpcPlayerResistanceSnapshot.Receipt?>? fixtureResistance=null,Func<Player,Effect,bool>? fixturePreflight=null)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(draw);
        if((resources is null)==(fixturePlan is null))throw new ArgumentException("Supply exactly one offline resource plan or factory");
        success=false;
        if(effect.RetailHasEnded ||effect.GetSuccessEffects().Count!=0
            ||effect.GetEffector() is not Npc npc ||effect.GetEffected() is not Player target
            ||!npc.IsSpawned() ||npc.IsDead() ||!target.IsSpawned() ||target.IsDead()
            ||!ReferenceEquals(npc.GetWorldMapInstance(),target.GetWorldMapInstance())
            ||!RetailNpcStatCatalog.TryGet(npc,out var profile)
            ||!RetailNpcStatCatalog.TryGetSkill(profile,effect.GetSkillId(),effect.GetSkillLevel(),out var original)
            ||original.Supported ||original.Sha256!=candidate.FullSourceSha256 ||candidate.Id!=effect.GetSkillId()
            ||candidate.Primary.Id!=candidate.Id ||candidate.Primary.Drain is not { } drain
            ||effect.GetEffectTemplates() is not {Count:2} templates
            ||templates[0] is not SpellAtkDrainInstantEffect primary ||primary.Position!=1
            ||templates[1] is not CurseEffect curse ||!RetailNpcStatSpellDrain.CurseHostMatches(curse)
            ||primary.Element!=candidate.Primary.Element ||primary.hpPercent!=drain.HpPercent
            ||primary.mpPercent!=0 ||!RetailNpcStatSpellDrain.SecondaryContextMatches(effect)
            ||!target.GetObserveController().CanCalculateRetailPlayerDrain(target)
            ||npc.GetObserveController().HasSkillCriticalObserver
            ||RetailNpcStatEquipmentResponses.Required(profile)
            ||fixturePreflight is not null && !fixturePreflight(target,effect))return false;
        var selected=candidate.Primary with {Sha256=candidate.FullSourceSha256,
            Drain=drain with {Curse=new(candidate.FullSourceSha256,resources,draw,fixturePlan,
                FixtureResistanceReceipt:fixtureResistance,AdmissionCheck:fixturePreflight)}};
        var modifiers=RetailNpcStatNormalInputs.CurrentModifiers(npc,StatEnum.BOOST_SPELL_ATTACK);
        bool handled=CalculateSpell(effect,primary,null,npc,target,profile,selected,
            RetailNpcStatCatalog.MagicBoostCap??2900,modifiers,null,out success,out var protection,draw);
        if(handled && success)
        {
            if(protection is null)throw new InvalidOperationException("Prepared vampire primary has no protection receipt");
            RetailNpcStatSpellDrain.Prepare(effect,primary,selected.Drain!,protection);
        }
        return handled;
    }

    private static bool CalculateSpell(Effect effect, EffectTemplate template, SpellStatus? spellStatus,
        Npc npc, Player target, SkyTempleNpcProfile profile, Source selected, int selectedCap,
        RetailNpcStatNormalInputs.Modifiers spellModifiers,RetailNpcStatEquipmentResponses.Handle? equipmentResponse, out bool success,
        out Aion.GameServer.Controllers.ObserveController.PlayerDrainProtectionReceipt? drainProtection,
        Func<int,int,int>? fixtureDraw=null)
    {
        int Roll(int first,int second)
        {
            int value=fixtureDraw is null?Draw(first,second):fixtureDraw(first,second);
            if(value<Math.Min(first,second)||value>Math.Max(first,second))
                throw new InvalidOperationException("Fixture spell roll is outside the requested range");
            return value;
        }
        using var ranges=new RangeTraceScope(selected.NoResist||selected.RandomType!=0||selected.SpellBonus is not null);
        success = false;drainProtection=null;
        var scope = SkyTempleCombatInputs.Scope(profile.NpcId);
        var context = RetailNpcNormalStatusMath.Context.Create(scope, RetailNpcNormalScope.MainBinarySha256,
            "statistics-only current 4.8 player inputs and source-bound primary spell", false);
        var boost = OneTimeBoostSkillAttackEffect.GetActiveBoost(npc, template);
        if(!selected.NoResist)
        {
            int accuracy = npc.GetGameStats().GetMAccuracy().GetCurrent();
            if (boost is not null) accuracy = boost.CalculateMagicalAccuracy(npc.GetGameStats().GetMAccuracy(), 0);
            int threshold = RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context, new(
                target.GetGameStats().GetMResist().GetCurrent(), accuracy, (uint)profile.StatRatio,
                profile.Level, target.GetLevel(), SkyTempleCombatInputs.Pose(target)));
            if (target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST) || Roll(1, 1000) <= threshold)
            {
                effect.ResetMagicalCritical(); effect.SetAttackStatus(AttackStatus.RESIST);
                effect.SetEffectResult(EffectResult.RESIST); return true;
            }
        }
        bool dual=profile.Source.Integers["dual"].ParsedValue is not (null or 0);
        WeaponSample? weapon=selected.UsesWeaponRange?DrawSkillWeapon(profile.MinDamage,profile.MaxDamage,dual,false,Roll):null;
        int sample=weapon?.Sample??0;
        int boostDelta = selected.ApplyBoost
            ? npc.GetGameStats().GetMBoost().GetCurrent() - target.GetGameStats().GetMBResist().GetCurrent() : 0;
        int value = selected.ValueAtSkillLevel(effect.GetSkillLevel());
        int amount = selected.UsesWeaponRange
            ? StatsSpellBase(sample, value, boostDelta, 1000, selected.ApplyBoost, selectedCap)
            : AbsoluteSpellBase(value, boostDelta, 1000, selected.ApplyBoost, selectedCap);
        int baseDamageBeforeRandom=amount;int? randomBasicDraw=null;
        amount=SpellRandomBasic(amount,selected.RandomType,selected.RandomMask,(lo,hi)=>
        {int word=Roll(lo,hi);randomBasicDraw=word;return word;});
        int baseDamage = amount;
        int bonusValue=selected.SpellBonus?.ValueAtLevel(effect.GetSkillLevel())??0;
        int targetBonusStatusMask=target.GetEffectController().RetailNpcTargetConditionMask;
        bool bonusApplies=selected.SpellBonus?.AppliesTo(target.GetPlayerClass(),target.GetRace(),targetBonusStatusMask)??false;
        var bonus=RetailNpcStatSpellBonus.Apply(amount,bonusValue,selected.SpellBonus?.Percent??false,
            bonusApplies,selected.RandomType,selected.RandomMask,Roll);
        amount=bonus.Damage;
        int afterAdditional=amount;
        amount = ApplySpellAttackModifiers(amount, spellModifiers);
        int modifiedDamage = amount;
        if (boost is not null)
        {
            amount = F.Cvtt(boost.CalculateDamage(amount));
            // For drains, consume only after the final protection check admits
            // the hit. A new shield can invalidate the earlier preflight.
            if (selected.Family != Kind.SpellDrain) boost.ConsumeCharge();
        }
        int boostedDamage = amount;
        amount = Math.Max(1, amount);
        int mdef = SkyTempleCombatInputs.Moved(target, StatEnum.MAGICAL_DEFEND, target.GetGameStats().GetMDef().GetCurrent());
        amount = Math.Max(0, unchecked(amount - F.Cvtt(F.Div(F.FromInt(mdef), 10f))));
        int afterMdef = amount;
        int elemental = SkyTempleCombatInputs.ElementalDefense(target, selected.Element);
        int normalized = F.Cvtt(F.Div(F.FromInt(elemental), F.Div(profile.StatRatio, 1000f)));
        int extra = profile.Level > 50 && target.GetLevel() > 50 ? (Math.Min(profile.Level, target.GetLevel()) - 50) * 10 : 0;
        int targetLevel = target.GetLevel();
        int elementFloor = SkyTempleCombatInputs.ElementalFloor(targetLevel);
        float multiplier = F.Sub(1f, F.Div(Math.Max(elementFloor, normalized), F.Add(extra, 1300f)));
        amount = F.Cvtt(F.Mul(amount, multiplier));
        int afterElement = amount;
        bool critical = selected.ApplyCritical && SelectCritical(npc, target, profile, false, selected, effect,Roll);
        // Main's disabled primary branch leaves the magical cache unset. A
        // following periodic effect still owns its independent critical check.
        if(selected.ApplyCritical)effect.SetCalculatedMagicalCritical(1, critical);
        int criticalReduction = 0;
        if (critical)
        {
            int reduction = F.Cvtt(F.Div(SkyTempleCombatInputs.CriticalReduction(target, false), F.Div(profile.StatRatio, 1000f)));
            criticalReduction = reduction;
            amount = F.Cvtt(F.Mul(amount, Math.Max(1f, F.Sub(1f, F.Div(reduction, 1000f)))));
        }
        int afterCritical = amount;
        var pve = SkyTempleCombatInputs.Pve(npc, target, false);
        amount = Math.Max(1, F.Cvtt(F.Mul(amount, RetailNpcNormalDamageStages.CalculatePveRatio(scope, pve.Attack, pve.Defense))));
        int afterPve = amount;
        if(equipmentResponse is not null)amount=equipmentResponse.Spell(amount);
        if(selected.Family!=Kind.SpellDrain)_ = Roll(1, 100); // Drain Begin has no chance draw.
        var attackStatus=critical ? AttackStatus.CRITICAL : AttackStatus.NORMALHIT;
        if (selected.Family == Kind.SpellDrain)
        {
            if (!target.GetObserveController().TryCalculatePlayerRetailNpcDrain(effect,target,amount,attackStatus,out drainProtection))
            {
                if (selected.ApplyCritical) effect.SetCalculatedMagicalCritical(1,false);
                effect.ResetMagicalCritical();
                Log.LogWarning("RETAIL_NPC_DRAIN_ABORT npc={Npc} skill={Skill} reason=protection-changed-during-calculation",
                    profile.NpcId,selected.Id);
                return true;
            }
            boost?.ConsumeCharge();
            effect.AddSuccessEffect(template); if (spellStatus.HasValue) effect.SetSpellStatus(spellStatus.Value);
        }
        else
        {
            effect.AddSuccessEffect(template); if (spellStatus.HasValue) effect.SetSpellStatus(spellStatus.Value);
            AttackUtil.CalculateEffectResult(effect,target,amount,attackStatus,HitType.MAHIT,false,1,true);
        }
        LogPrimaryInputs(new { version=1,family="spell",npc=profile.NpcId,source=npc.GetObjectId(),target=target.GetObjectId(),
            world=npc.GetWorldId(),skill=selected.Id,skillLevel=effect.GetSkillLevel(),generation=profile.Generation,
            profileHash=profile.SourceRecordSha256,skillHash=selected.Sha256,sourceLevel=profile.Level,targetLevel,
            noResist=selected.NoResist,sourceAccuracyFlat=selected.AccuracyFlat,rangeRequests=ranges.Requests,
            randomType=selected.RandomType,randomMask=selected.RandomMask,randomBasicDraw,baseDamageBeforeRandom,
            bonusProgram=selected.SpellBonus,bonusValue,bonusApplies,targetBonusStatusMask,bonusChanceDraw=bonus.ChanceDraw,
            bonusRandomDraw=bonus.RandomDraw,bonusAddedDamage=bonus.AddedDamage,afterAdditional,
            profileRatio=profile.StatRatio,dual,usesWeaponRange=selected.UsesWeaponRange,sample,rangeMin=weapon?.Minimum,rangeMax=weapon?.Maximum,
            value,applyBoost=selected.ApplyBoost,boostDelta,boostCap=selectedCap,modifierPercent=spellModifiers.Percent,modifierFlat=spellModifiers.Flat,
            baseDamage,modifiedDamage,oneTimeBoost=boost is not null,boostedDamage,mdef,afterMdef,element=selected.Element.ToString(),
            elemental,normalized,extra,elementFloor,multiplierBits=BitConverter.SingleToUInt32Bits(multiplier),afterElement,
            critical,criticalReduction,afterCritical,pveAttack=pve.Attack,pveDefense=pve.Defense,afterPve,
            equipmentResponse=equipmentResponse is not null,requestedDamage=amount,protectedDamage=effect.GetReserveds(1).GetValue() });
        Log.LogInformation("RETAIL_STAT_SKILL npc={Npc} skill={Skill} level={Level} family={Family} mode={BaseMode} value={Value} sample={Sample} element={Element} damage={Damage} dual={Dual} rangeMin={RangeMin} rangeMax={RangeMax} source={Source}",
            profile.NpcId, selected.Id, effect.GetSkillLevel(), selected.Family,
            selected.UsesWeaponRange ? "source-range" : "absolute", value, sample, selected.Element, amount,dual,weapon?.Minimum,weapon?.Maximum,selected.Sha256);
        success = true;
        return true;
    }

    private static bool CalculatePhysical(Effect effect, EffectTemplate template, SpellStatus? spellStatus,
        Npc npc, Player target, SkyTempleNpcProfile profile, Source selected,
        RetailNpcStatNormalInputs.Modifiers modifiers,RetailNpcStatEquipmentResponses.Handle? equipmentResponse, out bool success)
    {
        bool nativeStatuses=!selected.NoResist&&RetailNpcStatCatalog.StatusSources is not null;
        int forceThreshold=selected.ForceThreshold;
        using var ranges=new RangeTraceScope(selected.NoResist||nativeStatuses||forceThreshold!=0
            ||selected.RandomType!=0||selected.PhysicalBonus is not null);
        success = false;
        var scope = SkyTempleCombatInputs.Scope(profile.NpcId);
        var context = RetailNpcNormalStatusMath.Context.Create(scope, RetailNpcNormalScope.MainBinarySha256,
            "statistics-only current 4.8 player inputs and source-bound primary physical skill", false);
        var boost = OneTimeBoostSkillAttackEffect.GetActiveBoost(npc, template);
        int accuracy = npc.GetGameStats().GetMainHandPAccuracy().GetCurrent();
        int accuracyBonus=boost is not null&&!selected.NoResist&&forceThreshold==0
            ?boost.CalculatePhysicalAccuracyBonus(npc.GetGameStats().GetMainHandPAccuracy()):0;
        accuracy=PhysicalAccuracy(accuracy,accuracyBonus,selected.AccuracyFlat);
        RetailNpcStatPhysicalStatuses.Resolution? precheck=null;
        bool sourceBlindActive=false;int sourceBlindChance=0;int? sourceBlindDraw=null,naturalDodgeDraw=null;
        void RecordPrecheck(bool hit)
        {
            if(!Log.IsEnabled(LogLevel.Information))return;
            Log.LogInformation("RETAIL_STAT_PHYSICAL_PRECHECK {Payload}",JsonSerializer.Serialize(new
            {version=1,npc=profile.NpcId,source=npc.GetObjectId(),target=target.GetObjectId(),world=npc.GetWorldId(),
                skill=selected.Id,skillLevel=effect.GetSkillLevel(),generation=profile.Generation,skillHash=selected.Sha256,
                currentForce=0,sourceForce=selected.ForceThreshold,forceThreshold,noResist=selected.NoResist,nativeStatuses,
                sourceBlindActive,sourceBlindChance,sourceBlindDraw,naturalDodgeDraw,hit,precheck,rangeRequests=ranges.Requests}));
        }
        bool Dodge()
        {
            RecordPrecheck(false);effect.ResetMagicalCritical();effect.SetAttackStatus(AttackStatus.DODGE);
            effect.SetEffectResult(EffectResult.DODGE);return true;
        }
        if(nativeStatuses)
        {
            (sourceBlindActive,sourceBlindChance)=RetailNpcStatPhysicalStatuses.For(npc).Blind;
            if(sourceBlindActive)
            {sourceBlindDraw=Draw(1,100);if(sourceBlindDraw<=sourceBlindChance)return Dodge();}
            precheck=RetailNpcStatPhysicalStatuses.For(target).Resolve(forceThreshold);
            if(!precheck.Hit)return Dodge();
        }
        bool active = !selected.NoResist && forceThreshold==0 && SkyTempleCombatInputs.Pose(target) != 5
            && !npc.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE);
        if(active&&!nativeStatuses&&(npc.GetObserveController().CheckAttackerStatus(AttackStatus.DODGE)
            ||target.GetObserveController().CheckAttackStatus(AttackStatus.DODGE)))return Dodge();
        if(active)
        {
            naturalDodgeDraw=Draw(1,1000);
            if(naturalDodgeDraw<=Chance(context,profile,target,accuracy,RetailNpcNormalStatusMath.ChanceStage.Dodge,
                StatEnum.EVASION,target.GetGameStats().GetEvasion().GetCurrent()))return Dodge();
        }
        if(nativeStatuses||selected.NoResist||forceThreshold!=0)RecordPrecheck(true);
        _ = Draw(1, 100);
        bool dual=profile.Source.Integers["dual"].ParsedValue is not (null or 0);
        var weapon=DrawSkillWeapon(profile.MinDamage,profile.MaxDamage,dual,true,Draw);
        int sample=weapon.Sample;
        // Native random type0 skips the second draw. Type4 still draws, even
        // though its complete selected source table consists of twenty100s.
        int baseRandomType=(selected.RandomMask&1)!=0?selected.RandomType:0;
        int random = baseRandomType == 0 ? 0 : Draw(0, 99);
        int effectivePercent=selected.ValueAtSkillLevel(effect.GetSkillLevel());
        int baseHit=selected.BaseHitAtSkillLevel(effect.GetSkillLevel());
        float amount = StatisticsPhysicalBase(sample, effectivePercent, modifiers, baseRandomType, random,dual,baseHit);
        float beforeAdditional=amount;
        int heading=target.GetHeading();float sx=npc.GetX(),sy=npc.GetY(),tx=target.GetX(),ty=target.GetY();
        bool behind=selected.BackOnly&&SkyTempleNpcPhysicalSkills.Behind(heading,sx,sy,tx,ty);
        bool stumbled=selected.StumbleOnly&&target.GetEffectController().RetailNpcStumbleActive;
        int targetAdditionalStatusMask=target.GetEffectController().RetailNpcTargetConditionMask;
        bool statusAdditional=(selected.AdditionalStatusMask&targetAdditionalStatusMask)!=0;
        int additionalValue=selected.PhysicalBonus?.ValueAtLevel(effect.GetSkillLevel())??selected.AdditionalFlat;
        bool playerAdditional=selected.PhysicalBonus?.AppliesTo(target.GetPlayerClass(),target.GetRace())??false;
        var additional=RetailNpcStatPhysicalBonus.Apply(amount,additionalValue,
            behind||stumbled||statusAdditional||playerAdditional,selected.RandomType,selected.RandomMask,Draw);
        int? additionalDraw=additional.ChanceDraw,additionalRandomDraw=additional.RandomDraw;
        amount=additional.Damage;
        float afterAdditional=amount;
        if (boost is not null) { amount = boost.CalculateDamage(amount); boost.ConsumeCharge(); }
        float afterBoost = amount;
        amount = F.Cvtt(amount); // selected None attenuation accepts/returns int32
        int attenuatedDamage = (int)amount;
        amount = SkyTempleCombatInputs.Moved(npc, StatEnum.PHYSICAL_ATTACK, F.Cvtt(amount));
        int movementDamage = (int)amount;
        int targetLevel = target.GetLevel();
        uint defense = SkyTempleCombatInputs.Unsigned(SkyTempleCombatInputs.Moved(target,
            StatEnum.PHYSICAL_DEFENSE, target.GetGameStats().GetPDef().GetCurrent()), "Player PDef");
        amount = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope, Math.Max(1, amount), profile.Level,
            targetLevel, false, defense, 0, 1000).Damage;
        float afterDefense = amount;
        AttackStatus status = AttackStatus.NORMALHIT;
        int blockRemainingPercent = 100, blockMax = 0;
        bool forcedDefenseApplied=false;
        if(precheck?.CachedDefense==2&&target.GetEquipment().IsShieldEquipped())
        {
            status=AttackStatus.BLOCK;forcedDefenseApplied=true;
            blockRemainingPercent=target.GetGameStats().GetReverseStat(StatEnum.DAMAGE_REDUCE,100).GetCurrent();
            blockMax=target.GetEquipment().GetEquippedShield().GetItemTemplate().GetWeaponStats().GetReduceMax();
            amount=SkyTempleNpcCombat.BlockDamage(amount,blockRemainingPercent,blockMax);
        }
        else if(precheck?.CachedDefense==1)
        {status=AttackStatus.PARRY;forcedDefenseApplied=true;amount=F.Mul(amount,0.6f);}
        if (active&&!forcedDefenseApplied)
        {
            bool blocked = false;
            if (target.GetEquipment().IsShieldEquipped())
            {
                blocked = !nativeStatuses&&target.GetObserveController().CheckAttackStatus(AttackStatus.BLOCK)
                    || Draw(1, 1000) <= Chance(context, profile, target, accuracy,
                        RetailNpcNormalStatusMath.ChanceStage.Block, StatEnum.BLOCK, target.GetGameStats().GetBlock().GetCurrent());
                if (blocked)
                {
                    status = AttackStatus.BLOCK;
                    blockRemainingPercent = target.GetGameStats().GetReverseStat(StatEnum.DAMAGE_REDUCE, 100).GetCurrent();
                    blockMax = target.GetEquipment().GetEquippedShield().GetItemTemplate().GetWeaponStats().GetReduceMax();
                    amount = SkyTempleNpcCombat.BlockDamage(amount, blockRemainingPercent, blockMax);
                }
            }
            if (!blocked && (!nativeStatuses&&target.GetObserveController().CheckAttackStatus(AttackStatus.PARRY)
                || Draw(1, 1000) <= Chance(context, profile, target, accuracy,
                    RetailNpcNormalStatusMath.ChanceStage.Parry, StatEnum.PARRY, target.GetGameStats().GetParry().GetCurrent())))
            { status = AttackStatus.PARRY; amount = F.Mul(amount, .6f); }
        }
        float afterActiveDefense = amount;
        AttackStatus activeDefenseStatus = status;
        bool critical = SelectCritical(npc, target, profile, true, selected, effect);
        int criticalReduction = 0;
        if (critical)
        {
            int reduction = F.Cvtt(F.Div(SkyTempleCombatInputs.CriticalReduction(target, true), F.Div(profile.StatRatio, 1000f)));
            criticalReduction = reduction;
            amount = F.Mul(amount, Math.Max(1f, F.Sub(1.5f, F.Div(reduction, 1000f))));
            status = AttackStatusExtensions.GetCriticalStatusFor(status);
        }
        float afterCritical = amount;
        // Main physical Check owns hit-state/accuracy, not RunContext+50/+54.
        // A following Poison/Bleed owns its independent magical critical cache.
        // Keep the physical status in the AttackResult without seeding that cache.
        var pve = SkyTempleCombatInputs.Pve(npc, target, true);
        int damage = Math.Max(1, F.Cvtt(F.Mul(amount, RetailNpcNormalDamageStages.CalculatePveRatio(scope, pve.Attack, pve.Defense))));
        int afterPve = damage;
        if(equipmentResponse is not null)damage=equipmentResponse.Physical(damage);
        effect.AddSuccessEffect(template); if (spellStatus.HasValue) effect.SetSpellStatus(spellStatus.Value);
        AttackUtil.CalculateEffectResult(effect, target, damage, status, HitType.PHHIT, false, 1, true);
        LogPrimaryInputs(new { version=1,family="physical",magicalCriticalCachePresent=effect.TryGetCalculatedMagicalCritical(out _),npc=profile.NpcId,source=npc.GetObjectId(),target=target.GetObjectId(),
            world=npc.GetWorldId(),skill=selected.Id,skillLevel=effect.GetSkillLevel(),generation=profile.Generation,
            profileHash=profile.SourceRecordSha256,skillHash=selected.Sha256,sourceLevel=profile.Level,targetLevel,profileRatio=profile.StatRatio,
            noResist=selected.NoResist,sourceAccuracyFlat=selected.AccuracyFlat,rangeRequests=ranges.Requests,
            dual,sample,rangeMin=weapon.Minimum,rangeMax=weapon.Maximum,percent=effectivePercent,
            basePercent=selected.Percent,levelRate=selected.LevelRate,areaConditional=selected.AreaConditional,
            baseHitLevelRate=selected.BaseHitLevelRate,baseHitConstant=selected.BaseHitConstant,baseHit,
            sourceForce=selected.ForceThreshold,currentForce=0,forceThreshold,nativeStatuses,
            sourceBlindActive,sourceBlindChance,sourceBlindDraw,naturalDodgeDraw,cachedDefense=precheck?.CachedDefense??0,
            actualTargetCount=effect.GetSkill()?.GetEffectedList().Count??1,randomType=selected.RandomType,random,
            modifierPercent=modifiers.Percent,modifierFlat=modifiers.Flat,backOnly=selected.BackOnly,behind,heading,sx,sy,tx,ty,
            stumbleOnly=selected.StumbleOnly,stumbled,additional=additionalValue,additionalDraw,additionalRandomDraw,
            randomMask=selected.RandomMask,baseRandomType,playerAdditional,physicalBonus=selected.PhysicalBonus,
            additionalStatusMask=selected.AdditionalStatusMask,targetAdditionalStatusMask,statusAdditional,
            beforeAdditionalBits=BitConverter.SingleToUInt32Bits(beforeAdditional),afterAdditionalBits=BitConverter.SingleToUInt32Bits(afterAdditional),
            oneTimeBoost=boost is not null,afterBoostBits=BitConverter.SingleToUInt32Bits(afterBoost),attenuatedDamage,movementDamage,defense,
            afterDefenseBits=BitConverter.SingleToUInt32Bits(afterDefense),activeDefenseStatus=activeDefenseStatus.ToString(),blockRemainingPercent,blockMax,
            afterActiveDefenseBits=BitConverter.SingleToUInt32Bits(afterActiveDefense),critical,criticalReduction,
            afterCriticalBits=BitConverter.SingleToUInt32Bits(afterCritical),pveAttack=pve.Attack,pveDefense=pve.Defense,afterPve,
            equipmentResponse=equipmentResponse is not null,requestedDamage=damage,protectedDamage=effect.GetReserveds(1).GetValue() });
        Log.LogInformation("RETAIL_STAT_SKILL npc={Npc} skill={Skill} level={Level} family={Family} sample={Sample} percent={Percent} element={Element} damage={Damage} dual={Dual} rangeMin={RangeMin} rangeMax={RangeMax} source={Source} backOnly={BackOnly} behind={Behind} additional={Additional} random={Random} beforeAdditionalBits={BeforeBits} afterAdditionalBits={AfterBits} heading={Heading} sx={Sx} sy={Sy} tx={Tx} ty={Ty} modifierPercent={ModifierPercent} modifierFlat={ModifierFlat}",
            profile.NpcId, selected.Id, effect.GetSkillLevel(), selected.Family, sample, effectivePercent, selected.Element, damage,dual,weapon.Minimum,weapon.Maximum,selected.Sha256,
            selected.BackOnly,behind,additionalValue,random,BitConverter.SingleToUInt32Bits(beforeAdditional),BitConverter.SingleToUInt32Bits(afterAdditional),heading,sx,sy,tx,ty,modifiers.Percent,modifiers.Flat);
        success = true;
        return true;
    }

    // Log only values already consumed by the calculation. In particular, never
    // re-query status/shield observers, boosts or random draws for diagnostics.
    private static void LogPrimaryInputs<T>(T inputs)
    {
        if (Log.IsEnabled(LogLevel.Information))
            Log.LogInformation("RETAIL_STAT_PRIMARY_INPUT {Payload}", JsonSerializer.Serialize(inputs));
    }

    private static int Chance(RetailNpcNormalStatusMath.Context context, SkyTempleNpcProfile profile, Player target,
        int accuracy, RetailNpcNormalStatusMath.ChanceStage stage, StatEnum stat, int value) =>
        RetailNpcNormalStatusMath.CalculatePhysicalThreshold(context, stage,
            new(SkyTempleCombatInputs.Moved(target, stat, value), accuracy, 0, (uint)profile.StatRatio));

    internal static bool CriticalOwnerSupported(Source source,Aion.GameServer.Controllers.ObserveController observers)
    {
        bool newlyAdmitted=source.NoResist&&(source.Family==Kind.PhysicalInstant||source.ApplyCritical)
            ||source.Family==Kind.SpellInstant&&source.ApplyCritical&&source.CriticalPercent!=100
            ||source.Family==Kind.SpellDrain&&source.ApplyCritical
            ||source.Family==Kind.PhysicalInstant&&source.ForceThreshold!=0
            ||source.Family==Kind.PhysicalInstant
                &&(source.CriticalPercent is not (0 or 100)||source.CriticalPercent==0&&(source.BackOnly||source.StumbleOnly||source.AdditionalStatusMask!=0||source.PhysicalBonus is not null));
        return !newlyAdmitted||!observers.HasSkillCriticalObserver;
    }

    internal static bool NoResistRecordSupported(Source source,AttackStatus status)
        =>!source.NoResist||status==AttackStatus.NORMALHIT;

    internal readonly record struct RangeRequest(int First,int Second,int Value);
    private sealed class RangeTrace { internal readonly List<RangeRequest> Requests=[]; }
    [ThreadStatic] private static RangeTrace? activeRanges;
    internal readonly struct RangeTraceScope : IDisposable
    {
        private readonly RangeTrace? previous,current;
        internal RangeTraceScope(bool enabled)
        {
            previous=activeRanges;current=enabled?new RangeTrace():null;activeRanges=current;
        }
        internal IReadOnlyList<RangeRequest>? Requests=>current?.Requests;
        public void Dispose()=>activeRanges=previous;
    }
    internal static int Draw(int first,int second)
    {
        int value=SkyTempleNpcCombat.Draw(first,second);
        activeRanges?.Requests.Add(new(first,second,value));
        return value;
    }

    internal static int CriticalThreshold(int scaledCritical, int resistance, byte pose, uint defenderRatio)
    {
        if(pose>15 || defenderRatio is <1000 or >65000)throw new ArgumentOutOfRangeException(nameof(defenderRatio));
        int delta=unchecked(scaledCritical+(pose==5?500:0)-resistance);
        return Math.Min(500,F.Cvtt(F.Div(F.FromInt(delta),F.Div(F.FromInt((int)defenderRatio),1000f))));
    }

    internal static (float Factor,float Scaled,int Value) CriticalInput(int rawCritical,int percent)
    {
        // Skill::Set stores the integer coefficient /100. CheckResist adds
        // its zero level rate, then the calculator converts the scaled stat
        // BEFORE adding pose and subtracting resistance. Zero still rolls.
        float factor=F.Div(F.FromInt(percent),100f);
        float scaled=F.Mul(F.FromInt(rawCritical),factor);
        return (factor,scaled,F.Cvtt(scaled));
    }

    private static bool SelectCritical(Npc npc, Player target, SkyTempleNpcProfile profile, bool physical,
        Source selected, Effect effect,Func<int,int,int>? fixtureDraw=null)
    {
        int draw = fixtureDraw is null?Draw(1, 1000):fixtureDraw(1,1000);
        int rawCritical = physical ? npc.GetGameStats().GetMainHandPCritical().GetCurrent() : npc.GetGameStats().GetMCritical().GetCurrent();
        var input=CriticalInput(rawCritical,selected.CriticalPercent);
        float factor=input.Factor,scaled=input.Scaled;
        int critical = input.Value; // Native converts before integer pose/resistance arithmetic.
        int scaledCritical=critical;
        var forced = npc.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL, true);
        string forceKind=forced.IsResult()?(forced.IsPercent()?"legacy-percent-unverified":"absolute-owner-unverified"):"none";
        int? forceValue=forced.IsResult()?forced.GetValue():null;
        bool Complete(bool result,int decisionThreshold,string comparisonMode,int? criticalResistance,byte? rawPose)
        {
            Log.LogInformation("RETAIL_STAT_CRITICAL_INPUT {Inputs}",JsonSerializer.Serialize(new
            {
                version=1,npc=profile.NpcId,source=npc.GetObjectId(),target=target.GetObjectId(),world=npc.GetWorldId(),
                skill=effect.GetSkillId(),skillLevel=effect.GetSkillLevel(),generation=profile.Generation,
                profileHash=profile.SourceRecordSha256,skillHash=selected.Sha256,physical,
                sourcePercent=selected.CriticalPercent,rawCritical,factorBits=BitConverter.SingleToUInt32Bits(factor),
                scaledBits=BitConverter.SingleToUInt32Bits(scaled),scaledCritical,modifiedCritical=critical,
                forceKind,forceValue,resistance=criticalResistance,pose=rawPose,defenderRatio=SkyTempleCombatInputs.PlayerStatRatio,
                threshold=decisionThreshold,comparison=comparisonMode,draw,critical=result
            }));
            return result;
        }
        if (forced.IsResult() && !forced.IsPercent())
            return Complete(draw<=forced.GetValue(),forced.GetValue(),"le",null,null);
        // Existing managed percent-force behavior remains visibly unverified;
        // the native threshold owner/lifetime requires separate translation.
        if (forced.IsResult()) critical = F.Cvtt(F.Mul(critical, F.Add(1f, F.Div(forced.GetValue(), 100f))));
        int reduction = physical ? target.GetGameStats().GetPCR().GetCurrent() : target.GetGameStats().GetMCR().GetCurrent();
        byte pose=SkyTempleCombatInputs.Pose(target);
        int threshold=CriticalThreshold(critical,reduction,pose,SkyTempleCombatInputs.PlayerStatRatio);
        return Complete(draw<threshold,threshold,"lt",reduction,pose);
    }

}

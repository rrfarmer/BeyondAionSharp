using Aion.GameServer.Model;
using Aion.GameServer.Retail;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>SpellATK_Instant R7/8/9 and proved R16 player-target predicates.
/// Native evidence: stats-only-20261025-01/spell-bonus-stat-implementation-01.
/// The condition's first half belongs to secondary status emission; this class
/// implements only its second half, consumed by primary bonus damage.</summary>
internal static class RetailNpcStatSpellBonus
{
    internal sealed record Program(int Rate, int Constant, bool Percent, uint ClassMask,
        int? PlayerRaceMask=null, string? ConditionContextSha256=null,int StatusMask=0)
    {
        internal int ValueAtLevel(int level)
        {
            if (level is < 0 or > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(level));
            return Math.Max(0, unchecked((short)level * Rate + Constant));
        }
        internal bool AppliesTo(PlayerClass targetClass, Race targetRace=Race.NONE,int targetStatusMask=0)
        {
            if(PlayerRaceMask.HasValue && targetRace is not (Race.ELYOS or Race.ASMODIANS))
                throw new ArgumentOutOfRangeException(nameof(targetRace));
            // An authored NPC-race predicate projects to mask0 for a player;
            // that is a FALSE condition, distinct from an absent predicate.
            return ClassMask==0&&PlayerRaceMask is null&&StatusMask==0
                ||(ClassMask&(1u<<targetClass.GetClassId()))!=0
                ||PlayerRaceMask.HasValue&&(PlayerRaceMask.Value&(targetRace==Race.ELYOS?1:2))!=0
                ||(StatusMask&targetStatusMask)!=0;
        }
    }

    // CheckParameter140bc0390 clears BOTH constructed operands when R8 or the
    // wrapped R8+R9 is negative. A negative R9 alone is valid (e.g.86*level-86).
    internal static (int Rate, int Constant) Construct(int rate, int constant) =>
        rate < 0 || unchecked(rate + constant) < 0 ? (0, 0) : (rate, constant);

    internal static bool TryClassCondition(string? text, out uint mask)
    {
        mask = 0;
        if (text is null) return true;
        if (text.Length > 255) return false; // Native temporary buffer is256 wchar_t.
        foreach (string token in text.Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            string key = RetailNpcStatCatalog.NativeSkillNameKey(token);
            string name;
            if (key.StartsWith("_class_", StringComparison.Ordinal)) name = key[7..];
            else if (key.StartsWith("_dmg_class_", StringComparison.Ordinal)) name = key[11..];
            else return false;
            // Exact built-in table1410ea0f0 and the native elementalist alias.
            // In particular native priest=10 (C# CLERIC), cleric=9 (C# PRIEST).
            int id = name switch
            {
                "warrior"=>0, "fighter"=>1, "knight"=>2, "scout"=>3,
                "assassin"=>4, "ranger"=>5, "mage"=>6, "wizard"=>7,
                "elementallist" or "elementalist"=>8, "cleric"=>9,
                "priest"=>10, "chanter"=>11, "engineer"=>12, "rider"=>13,
                "gunner"=>14, "artist"=>15, "bard"=>16, _=>-1
            };
            if (id < 0) return false;
            mask |= 1u << id;
        }
        return true;
    }

    internal static bool TryCondition(string? text, RetailNpcStatSpellConditions? context,
        out uint classMask, out int? playerRaceMask)
    {
        classMask=0;playerRaceMask=null;
        if(context is null)return TryClassCondition(text,out classMask);
        if(text is null)return true;
        if(text.Length>255)return false;
        foreach(string token in text.Split([',',' ','\t'],StringSplitOptions.RemoveEmptyEntries))
        {
            if(TryClassCondition(token,out uint classes)){classMask|=classes;continue;}
            string key=RetailNpcStatCatalog.NativeSkillNameKey(token);
            string race;
            if(key.StartsWith("_race_",StringComparison.Ordinal))race=key[6..];
            else if(key.StartsWith("_dmg_race_",StringComparison.Ordinal))race=key[10..];
            else return false;
            if(!context.TryPlayerMask(race,out int mask))return false;
            playerRaceMask=(playerRaceMask??0)|mask;
        }
        return true;
    }

    // Race-only NPC predicates use the 64-bit native race-order mask, not the
    // two-bit player projection. Other predicate families need their own
    // reviewed NPC ownership and are deliberately rejected here.
    internal static bool TryNpcRaceCondition(string? text,RetailNpcStatSpellConditions? context,out ulong mask)
    {
        mask=0;
        if(context is null || string.IsNullOrEmpty(text) || text.Length>255)return false;
        bool any=false;
        foreach(string token in text.Split([',',' ','\t'],StringSplitOptions.RemoveEmptyEntries))
        {
            string key=RetailNpcStatCatalog.NativeSkillNameKey(token);
            string race;
            if(key.StartsWith("_race_",StringComparison.Ordinal))race=key[6..];
            else if(key.StartsWith("_dmg_race_",StringComparison.Ordinal))race=key[10..];
            else {mask=0;return false;}
            if(!context.TryNativeRace(race,out var native)){mask=0;return false;}
            mask|=1UL<<native.Order;any=true;
        }
        return any;
    }

    // Returning false means the target's race could not be established; that
    // is distinct from a known race that does not match the predicate.
    internal static bool TryNpcRaceMatch(ulong mask,RetailNpcStatSpellConditions context,
        SkyTempleNpcProfile target,out bool matches)
    {
        matches=false;
        if(!context.TryNpcRace(target,out var race))return false;
        matches=(mask&(1UL<<race.Order))!=0;
        return true;
    }

    internal readonly record struct Result(int Damage, int? ChanceDraw, int? RandomDraw, int? AddedDamage);

    // R16 condition48's second half ORs native class/race/status matches.
    // Only statuses with a separately proved event/lifetime read side enter
    // this projection. Never use the legacy4.8 abnormal union as that mask.
    internal static bool TrySpellCondition(string? text,RetailNpcStatSpellConditions? context,
        out uint classes,out int? races,out int statusMask,bool allowParalyze=false)
    {
        classes=0;races=null;statusMask=0;
        if(text is null)return true;
        if(text.Length>255)return false;
        foreach(string token in text.Split([',',' ','\t'],StringSplitOptions.RemoveEmptyEntries))
        {
            string key=RetailNpcStatCatalog.NativeSkillNameKey(token);
            int status=key switch
            {
                "poison" or "_dmg_poison"=>RetailNpcStatTargetConditions.PoisonMask,
                "bleed" or "_dmg_bleed"=>RetailNpcStatTargetConditions.BleedMask,
                "openaerial" or "_dmg_openaerial"=>RetailNpcStatTargetConditions.OpenAerialMask,
                "paralyze" when allowParalyze=>RetailNpcStatTargetConditions.ParalyzeMask,
                _=>0
            };
            if(status!=0){statusMask|=status;continue;}
            if(!TryCondition(token,context,out uint c,out int? r))return false;
            classes|=c;if(r.HasValue)races=(races??0)|r.Value;
        }
        return true;
    }

    // CalcMagicalSkillAttackDamage14036f850: base has already been quantized
    // and independently randomized. The constant100 chance still draws; a false
    // predicate or zero level-value skips both draws. Randomize the FLOAT bonus
    // before CVTT, then ADD32 to the integer base, before spell modifiers/armor.
    internal static Result Apply(int amount, int value, bool percent, bool applies,
        int randomType, int randomMask, Func<int,int,int> draw)
    {
        if (amount < 0 || value < 0 || randomType is < 0 or > 10 || randomMask is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!applies || value == 0) return new(amount, null, null, null);
        int chance = draw(1, 100);
        if (chance is < 1 or > 100) throw new InvalidOperationException("Spell bonus chance outside1..100");
        float bonus = F.Add(F.Mul(F.Div(percent ? value : 0, 100f), F.FromInt(amount)),
            F.FromInt(percent ? 0 : value));
        int? random = null;
        if (randomType != 0 && (randomMask & 2) != 0)
        {
            random = draw(0, 99);
            if (random is < 0 or > 99) throw new InvalidOperationException("Spell bonus random outside0..99");
            bonus = F.Mul(bonus, F.Div(RetailNpcStatSkillCombat.RandomFactor(randomType, random.Value), 100f));
        }
        int added = F.Cvtt(bonus);
        return new(unchecked(amount + added), chance, random, added);
    }
}

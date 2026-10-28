using Aion.GameServer.Model;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Physical R9/10 construction and second-half player predicates.
/// Unlike spells, physical R7 does not select percentage damage. The flat value
/// enters the existing float additional stage before truncation and defenses.</summary>
internal static class RetailNpcStatPhysicalBonus
{
    // Native CheckParameter140bbcb70 validates wrapped R9+R10 at level1.
    internal static (int Rate,int Constant) Construct(int rate,int constant) =>
        rate<0||unchecked(rate+constant)<0?(0,0):(rate,constant);

    internal readonly record struct Result(float Damage,int? ChanceDraw,int? RandomDraw);

    // Native physical branch14036de40..36def8 keeps FLOAT addition until the
    // caller's later truncation. AddDmg/All randomize the bonus independently;
    // unlike the spell branch, there is no bonus CVTT/ADD32 here.
    internal static Result Apply(float amount,int additional,bool applies,int randomType,int mask,Func<int,int,int> draw)
    {
        if(!float.IsFinite(amount)||additional<0||randomType is <0 or >10||mask is <1 or >3)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if(!applies||additional==0)return new(amount,null,null);
        int chance=draw(1,100);
        if(chance is <1 or >100)throw new InvalidOperationException("Physical additional chance outside1..100");
        float bonus=F.Add(F.Mul(amount,0f),F.FromInt(additional));
        int? random=null;
        if(randomType!=0&&(mask&2)!=0)
        {
            random=draw(0,99);
            if(random is <0 or >99)throw new InvalidOperationException("Physical additional random outside0..99");
            bonus=F.Mul(bonus,F.Div(RetailNpcStatSkillCombat.RandomFactor(randomType,random.Value),100f));
        }
        return new(F.Add(amount,bonus),chance,random);
    }

    internal sealed record Program(int Rate,int Constant,bool UsesPlayerPredicate,uint ClassMask,
        int? PlayerRaceMask=null,string? ConditionContextSha256=null)
    {
        internal int ValueAtLevel(int level)
        {
            if(level is <0 or >short.MaxValue)throw new ArgumentOutOfRangeException(nameof(level));
            return Math.Max(0,unchecked((short)level*Rate+Constant));
        }

        internal bool AppliesTo(PlayerClass targetClass,Race targetRace)
        {
            if(!UsesPlayerPredicate)return false; // Geometry/status is evaluated separately.
            if(PlayerRaceMask.HasValue&&targetRace is not (Race.ELYOS or Race.ASMODIANS))
                throw new ArgumentOutOfRangeException(nameof(targetRace));
            return ClassMask==0&&PlayerRaceMask is null
                ||(ClassMask&(1u<<targetClass.GetClassId()))!=0
                ||PlayerRaceMask.HasValue&&(PlayerRaceMask.Value&(targetRace==Race.ELYOS?1:2))!=0;
        }
    }
}

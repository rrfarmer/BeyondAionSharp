using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Native pre-critical defense stages for an ordinary NPC spell hitting
/// another ordinary ownerless NPC. Inputs already represent the current actors;
/// source selection, RNG, protection, HP and healing are separate owners.</summary>
internal static class RetailNpcStatNpcSpellDefense
{
    internal readonly record struct Inputs(int Damage,int SourceLevel,int TargetLevel,uint SourceRatio,
        int MagicalDefense,int MagicalDefenseMovePercent,int ElementalDefense,int ElementalMoveFlat);
    internal readonly record struct Result(int AfterLevel,int AfterMagicalDefense,int NormalizedElement,int AfterElement);

    internal static Result Calculate(RetailNpcNormalScope scope,Inputs input)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.RequireNpcAttacker();
        if(scope.Defender!=RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
            throw new ArgumentException("This defense stage requires an ordinary ownerless NPC target",nameof(scope));
        if(input.Damage<0 || input.SourceLevel is <0 or >ushort.MaxValue || input.TargetLevel is <0 or >ushort.MaxValue
            || input.SourceRatio is <1000 or >65000 || input.MagicalDefense<0
            || input.ElementalDefense is <short.MinValue or >short.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(input));

        // FUN_14036b890: a higher-level ordinary NPC target reduces incoming
        // damage by ten percent per level above attacker+2, floored at zero.
        int excess=input.TargetLevel-input.SourceLevel-2;
        float damage=F.FromInt(input.Damage);
        if(excess>0)
        {
            float factor=F.Sub(1f,F.Mul(F.FromInt(excess),0.1f));
            damage=factor<0f ? 0f : F.Mul(damage,factor);
        }
        int afterLevel=Math.Max(1,F.Cvtt(damage));
        // CalcMagicalSkillAttackDamage applies the target pose percentage to
        // the current magical defense before the final division and truncation.
        float defense=F.Add(F.Mul(F.Div(F.FromInt(input.MagicalDefenseMovePercent),100f),
            F.FromInt(input.MagicalDefense)),F.FromInt(input.MagicalDefense));
        int afterDefense=Math.Max(0,unchecked(afterLevel-F.Cvtt(F.Div(defense,10f))));
        int element=unchecked(input.ElementalDefense+input.ElementalMoveFlat);
        int normalized=F.Cvtt(F.Div(F.FromInt(element),F.Div(F.FromInt((int)input.SourceRatio),1000f)));
        int bounded=Math.Max(-1300,normalized); // NPC target branch, not the player level-based floor.
        int afterElement=F.Cvtt(F.Mul(F.Sub(1f,F.Div(F.FromInt(bounded),1300f)),F.FromInt(afterDefense)));
        return new(afterLevel,afterDefense,normalized,afterElement);
    }
}

using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using ChangeFunc=Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Offline, optimistic witness for an existing 4.8 17860 effect and
/// its two host stat functions. It does not replace or own those functions,
/// reserve the target, or establish retail target/lifecycle admission.</summary>
internal static class RetailNpcPlayerDefenseDrainEffectWitness
{
    private const string Stack="BNEL_SPELLDRAINMPATTSDDEFTA10_CH1";
    private static readonly StatEnum[] Elements=[StatEnum.WATER_RESISTANCE,StatEnum.WIND_RESISTANCE,
        StatEnum.EARTH_RESISTANCE,StatEnum.FIRE_RESISTANCE,StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE];

    internal sealed class Receipt(Player target,Effect effect,StatdownEffect template,CreatureGameStats stats,EffectController controller,
        StatFunctionProxy physical,StatFunctionProxy magic,long stamp,byte[] source)
    {
        internal bool GameplayReady=>false;
        internal bool IsCurrentForOfflineFixture(Player player)
            =>ReferenceEquals(player,target)&&ReferenceEquals(player.GetGameStats(),stats)
                &&ReferenceEquals(player.GetEffectController(),controller)
                &&stats.RetailStatMutationUnchanged(stamp)
                &&Matches(player,effect,template,stats,out var currentPhysical,out var currentMagic)
                &&ReferenceEquals(currentPhysical,physical)&&ReferenceEquals(currentMagic,magic)
                &&stats.RetailStatMutationUnchanged(stamp);

        // Input is an explicitly supplied retail vector excluding this effect;
        // the observed host functions are not themselves retail corrections.
        internal bool TryProjectForOfflineFixture(Player player,RetailNpcPlayerDefenseDrainLifecycleProjection.Vector other,
            out RetailNpcPlayerDefenseDrainLifecycleProjection.Vector projected)
        {
            projected=default;if(!IsCurrentForOfflineFixture(player))return false;
            var value=RetailNpcPlayerDefenseDrainLifecycleProjection.ApplyForOfflineFixture(source,other,
                RetailNpcPlayerDefenseDrainLifecycleProjection.Invocation.Begin);
            if(!IsCurrentForOfflineFixture(player))return false;
            projected=value;return true;
        }
    }

    internal static bool TryCaptureForOfflineFixture(Player target,Effect effect,ReadOnlySpan<byte> source,out Receipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(effect);
        receipt=null;_ = RetailNpcPlayerDefenseDrainProjection.Read17860PercentForOfflineFixture(source);
        if(effect.EffectInPos(2) is not StatdownEffect template)return false;
        var stats=target.GetGameStats();var controller=target.GetEffectController();
        if(stats is null||!stats.TryCaptureRetailStatMutation(out long stamp)
            ||!Matches(target,effect,template,stats,out var physical,out var magic)
            ||!ReferenceEquals(target.GetEffectController(),controller)
            ||!stats.RetailStatMutationUnchanged(stamp))return false;
        receipt=new(target,effect,template,stats,controller,physical!,magic!,stamp,source.ToArray());return true;
    }

    private static bool Matches(Player target,Effect effect,StatdownEffect template,CreatureGameStats stats,
        out StatFunctionProxy? physical,out StatFunctionProxy? magic)
    {
        physical=null;magic=null;
        if(effect.RetailHasEnded||effect.GetSkillId()!=17860||effect.GetStack()!=Stack
            ||!ReferenceEquals(effect.GetEffected(),target)||!ReferenceEquals(target.GetGameStats(),stats)
            ||target.GetEffectController() is not { } controller||!ReferenceEquals(controller.GetOwner(),target)
            ||!ReferenceEquals(controller.GetAbnormalEffect(Stack),effect)
            ||effect.EffectInPos(1) is not SpellAtkDrainInstantEffect first
            ||!ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(1),first)
            ||!ReferenceEquals(effect.EffectInPos(2),template)
            ||!ReferenceEquals(effect.GetSkillTemplate().GetEffectTemplate(2),template)||!HostShape(template))return false;
        bool Find(StatEnum stat,out StatFunctionProxy? proxy)
        {
            proxy=null;var owned=stats.GetStatsSorted(stat).Where(x=>ReferenceEquals(x.GetOwner(),effect)).ToArray();
            if(owned is not [StatFunctionProxy candidate]||candidate.GetProxiedFunction().GetType()!=typeof(StatRateFunction)
                ||candidate.GetName()!=stat||candidate.GetValue()!=-10||!candidate.IsBonus()||candidate.HasConditions())return false;
            proxy=candidate;return true;
        }
        if(!Find(StatEnum.PHYSICAL_DEFENSE,out physical)||!Find(StatEnum.MAGICAL_RESIST,out magic))return false;
        return Elements.All(stat=>!stats.GetStatsSorted(stat).Any(x=>ReferenceEquals(x.GetOwner(),effect)));
    }

    private static bool HostShape(StatdownEffect effect)
        =>effect.GetType()==typeof(StatdownEffect)&&effect.Position==2&&effect.NoResist
            &&effect.Duration1==0&&effect.Duration2==60100&&effect.Effectid==10178603
            &&effect.Element==SkillElement.WATER&&effect.PreEffects is [1]
            &&effect.Hoptype==HopType.SKILLLV&&effect.HopA==60&&effect.HopB==60
            &&effect.change is [var p,var m]&&p.Stat==StatEnum.PHYSICAL_DEFENSE&&m.Stat==StatEnum.MAGICAL_RESIST
            &&p.Func==ChangeFunc.PERCENT&&m.Func==ChangeFunc.PERCENT&&p.Value==-10&&m.Value==-10
            &&p.Delta==0&&m.Delta==0&&p.Conditions is null&&m.Conditions is null;
}

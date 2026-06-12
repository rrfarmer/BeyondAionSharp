using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/PlayerStatFunctions (ATracer). static{} block→static ctor; package-private function classes→internal classes in same file; instanceof→is; ArrayUtils.contains→LINQ Contains; stream.max(comparator).get→MaxBy; base `stat` field→Stat. IStatFunction/StatFunctionProxy/Stat2/Player.GetGameStats/GetEquipment red-tolerated.</summary>
public class PlayerStatFunctions
{
    private static readonly List<IStatFunction> FUNCTIONS = new();

    static PlayerStatFunctions()
    {
        FUNCTIONS.Add(new PhysicalAttackFunction());
        FUNCTIONS.Add(new MagicalAttackFunction());
        FUNCTIONS.Add(new AttackSpeedFunction());
        FUNCTIONS.Add(new BoostCastingTimeFunction());
        FUNCTIONS.Add(new PvPAttackRatioFunction());
        FUNCTIONS.Add(new PDefFunction());
        FUNCTIONS.Add(new MaxHpFunction());
        FUNCTIONS.Add(new MaxMpFunction());
        FUNCTIONS.Add(new BlockFunction());
        FUNCTIONS.Add(new ParryFunction());
        FUNCTIONS.Add(new EvasionFunction());
        FUNCTIONS.Add(new PhysicalCriticalFunction());
        FUNCTIONS.Add(new PhysicalAccuracyFunction());
        FUNCTIONS.Add(new PvEAttackRatioFunction());
        FUNCTIONS.Add(new PvEDefendRatioFunction());
    }

    public static List<IStatFunction> GetFunctions()
    {
        return FUNCTIONS;
    }

    public static void AddPredefinedStatFunctions(Player player)
    {
        player.GetGameStats().AddEffectOnly(null, FUNCTIONS);
    }
}

internal class PhysicalAttackFunction : StatFunction
{
    public PhysicalAttackFunction()
    {
        Stat = StatEnum.PHYSICAL_ATTACK;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
        {
            int power = stat.GetOwner().GetGameStats().GetPower().GetCurrent();
            if (player.GetEquipment().GetMainHandWeapon() == null)
            {
                stat.SetBaseRate(1 + ((power - 100) * player.GetPlayerClass().GetNoWeaponPowerMultiplier()) / 10000f);
            }
            else
            {
                if (calculationTypes.Contains(CalculationType.SKILL) && calculationTypes.Contains(CalculationType.DUAL_WIELD))
                {
                    if (power > 100)
                        power = Rnd.Get(100, power);
                    else
                        power = Rnd.Get(power, 100);
                }
                stat.SetBaseRate(power * 0.01f);
            }
        }
    }

    public override int GetPriority()
    {
        return 30;
    }
}

internal class MaxHpFunction : StatFunction
{
    public MaxHpFunction()
    {
        Stat = StatEnum.MAXHP;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetHealthDependentAdditionalHp());
    }

    public override int GetPriority()
    {
        return 30;
    }
}

internal class MaxMpFunction : StatFunction
{
    public MaxMpFunction()
    {
        Stat = StatEnum.MAXMP;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetWillDependentAdditionalMp());
    }

    public override int GetPriority()
    {
        return 30;
    }
}

internal class MagicalAttackFunction : StatFunction
{
    public MagicalAttackFunction()
    {
        Stat = StatEnum.MAGICAL_ATTACK;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        float knowledge = stat.GetOwner().GetGameStats().GetKnowledge().GetCurrent();
        stat.SetBaseRate(knowledge * 0.01f);
    }

    public override int GetPriority()
    {
        return 30;
    }
}

internal class PDefFunction : StatFunction
{
    public PDefFunction()
    {
        Stat = StatEnum.PHYSICAL_DEFENSE;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner().IsInFlyingState())
            stat.SetBonus(stat.GetBonus() - (stat.GetBase() / 2));
    }

    public override int GetPriority()
    {
        return 60;
    }
}

internal class BlockFunction : StatFunction
{
    public BlockFunction()
    {
        Stat = StatEnum.BLOCK;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetAgilityDependentAdditionalBaseBlock());
    }
}

internal class ParryFunction : StatFunction
{
    public ParryFunction()
    {
        Stat = StatEnum.PARRY;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetAgilityDependentAdditionalBaseParry());
    }
}

internal class EvasionFunction : StatFunction
{
    public EvasionFunction()
    {
        Stat = StatEnum.EVASION;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetAgilityDependentAdditionalBaseEvasion());
    }
}

internal class PhysicalCriticalFunction : StatFunction
{
    public PhysicalCriticalFunction()
    {
        Stat = StatEnum.PHYSICAL_CRITICAL;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetAccuracyDependentAdditionalBasePhysicalCritical());
    }
}

internal class PhysicalAccuracyFunction : StatFunction
{
    public PhysicalAccuracyFunction()
    {
        Stat = StatEnum.PHYSICAL_ACCURACY;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (stat.GetOwner() is Player player)
            stat.AddToBase(player.GetGameStats().GetAccuracyDependentAdditionalBasePhysicalAccuracy());
    }
}

internal class AttackSpeedFunction : DuplicateStatFunction
{
    public AttackSpeedFunction()
    {
        Stat = StatEnum.ATTACK_SPEED;
    }
}

internal class BoostCastingTimeFunction : DuplicateStatFunction
{
    public BoostCastingTimeFunction()
    {
        Stat = StatEnum.BOOST_CASTING_TIME;
    }
}

internal class PvEAttackRatioFunction : StatFunction
{
    public PvEAttackRatioFunction()
    {
        Stat = StatEnum.PVE_ATTACK_RATIO;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        WorldMapTemplate template = DataManager.WORLD_MAPS_DATA.GetTemplate(stat.GetOwner().GetWorldId());
        stat.AddToBonus(template.GetPvEAttackRatio());
    }
}

internal class PvEDefendRatioFunction : StatFunction
{
    public PvEDefendRatioFunction()
    {
        Stat = StatEnum.PVE_DEFEND_RATIO;
    }

    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        WorldMapTemplate template = DataManager.WORLD_MAPS_DATA.GetTemplate(stat.GetOwner().GetWorldId());
        stat.AddToBonus(template.GetPvEDefendRatio());
    }
}

internal class PvPAttackRatioFunction : DuplicateStatFunction
{
    public PvPAttackRatioFunction()
    {
        Stat = StatEnum.PVP_ATTACK_RATIO;
    }
}

internal class DuplicateStatFunction : StatFunction
{
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        Item mainWeapon = ((Player)stat.GetOwner()).GetEquipment().GetMainHandWeapon();
        Item offWeapon = ((Player)stat.GetOwner()).GetEquipment().GetOffHandWeapon();
        if (mainWeapon == offWeapon)
            offWeapon = null;

        if (mainWeapon != null)
        {
            StatFunction func1 = null;
            StatFunction func2 = null;
            List<StatFunction> functions = new();
            List<StatFunction> functions1 = mainWeapon.GetItemTemplate().GetModifiers();

            if (functions1 != null)
            {
                List<StatFunction> f1 = GetFunctions(functions1, stat, mainWeapon);
                if (f1.Count != 0)
                {
                    func1 = f1[0];
                    functions.AddRange(f1);
                }
            }

            if (mainWeapon.HasFusionedItem())
            {
                ItemTemplate template = mainWeapon.GetFusionedItemTemplate();
                List<StatFunction> functions2 = template.GetModifiers();
                if (functions2 != null)
                {
                    List<StatFunction> f2 = GetFunctions(functions2, stat, mainWeapon);
                    if (f2.Count != 0)
                    {
                        func2 = f2[0];
                        functions.AddRange(f2);
                    }
                }
            }
            else if (offWeapon != null)
            {
                List<StatFunction> functions2 = offWeapon.GetItemTemplate().GetModifiers();
                if (functions2 != null)
                {
                    functions.AddRange(GetFunctions(functions2, stat, offWeapon));
                }
            }

            if (func1 != null && func2 != null) // for fusioned weapons
            {
                if (Math.Abs(func1.GetValue()) >= Math.Abs(func2.GetValue()))
                    functions.Remove(func2);
                else
                    functions.Remove(func1);
            }
            if (functions.Count != 0)
            {
                if (GetName() == StatEnum.PVP_ATTACK_RATIO)
                {
                    functions.ForEach(f => f.Apply(stat, calculationTypes));
                }
                else
                {
                    functions.MaxBy(f => f.GetValue()).Apply(stat, calculationTypes);
                }
                functions.Clear();
            }
        }
    }

    private List<StatFunction> GetFunctions(List<StatFunction> list, Stat2 stat, Item item)
    {
        List<StatFunction> functions = new();
        foreach (StatFunction func in list)
        {
            if (func.GetName() == GetName())
            {
                StatFunctionProxy func2 = new(item, func);
                if (func2.Validate(stat))
                    functions.Add(func);
            }
        }
        return functions;
    }

    public override int GetPriority()
    {
        return 60;
    }
}

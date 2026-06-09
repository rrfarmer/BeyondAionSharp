using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/TemperingEffect implements StatOwner.</summary>
public class TemperingEffect : Aion.GameServer.Model.Stats.Calc.IStatOwner
{
    private static readonly ILogger log = NullLogger.Instance;

    private TemperingEffect(Aion.GameServer.Model.GameObjects.Player.Player player, List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction> functions)
    {
        player.GetGameStats().AddEffect(this, functions);
    }

    public void EndEffect(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        player.GetGameStats().EndEffect(this);
    }

    private static void AddAccessoryStatFunctions(Item item, List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction> functions)
    {
        IDictionary<int, List<TemperingStat>> tempering = DataManager.TEMPERING_DATA.GetTemplates(item.GetItemTemplate());
        List<TemperingStat> temperingStats = null;
        if (tempering != null)
            tempering.TryGetValue(item.GetTempering(), out temperingStats);
        if (temperingStats == null)
            return;
        foreach (TemperingStat temperingStat in temperingStats)
            functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(temperingStat.GetStat(), temperingStat.GetValue(), false));
    }

    private static void AddPlumeStatFunctions(Item item, List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction> functions)
    {
        StatEnum st;
        int value = item.GetRndPlumeBonusValue();
        if (item.GetItemTemplate().GetTemperingName().Equals("TSHIRT_PHYSICAL"))
        {
            st = StatEnum.PHYSICAL_ATTACK;
            value += PlumStatEnum.PLUM_PHISICAL_ATTACK.GetBoostValue() * item.GetTempering();
        }
        else
        {
            st = StatEnum.BOOST_MAGICAL_SKILL;
            value += PlumStatEnum.PLUM_BOOST_MAGICAL_SKILL.GetBoostValue() * item.GetTempering();
        }
        functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(st, value, true));
        functions.Add(new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(StatEnum.MAXHP, PlumStatEnum.PLUM_HP.GetBoostValue() * item.GetTempering(), true));
    }

    public static void Apply(Aion.GameServer.Model.GameObjects.Player.Player player, Item item)
    {
        List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction> functions = new List<Aion.GameServer.Model.Stats.Calc.Functions.IStatFunction>();
        if (item.GetItemTemplate().GetItemGroup() == Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.PLUME)
        {
            AddPlumeStatFunctions(item, functions);
        }
        else
        {
            AddAccessoryStatFunctions(item, functions);
        }
        if (functions.Count == 0)
        {
            log.LogWarning("Missing tempering effect info for item " + item);
            return;
        }
        if (item.GetTemperingEffect() != null)
            item.GetTemperingEffect().EndEffect(player);
        item.SetTemperingEffect(new TemperingEffect(player, functions));
    }
}

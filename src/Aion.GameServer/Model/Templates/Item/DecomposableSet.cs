using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Java parity: model/templates/item/DecomposableSet.
/// One reward branch of a decomposable item. Branches are rolled independently of each other, and a branch that fires yields exactly one of
/// its weighted alternatives (or nothing at all, if their chances don't add up to 100).
/// </summary>
[XmlType("DecomposableSet")]
public class DecomposableSet
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("chance")] public float chance = 100;
    [XmlAttribute("min_level")] public int minLevel = 1;
    [XmlAttribute("max_level")] public int maxLevel = 80;
    [XmlAttribute("race")] public Race race = Race.PC_ALL;
    [XmlElement("item")] public List<DecomposedItem> items;

    // Java parity: @XmlList @XmlAttribute(name="player_classes") List<PlayerClass> — space-separated.
    private List<PlayerClass> playerClasses;

    [XmlAttribute("player_classes")]
    public string PlayerClassesRaw
    {
        get => playerClasses == null ? null : string.Join(" ", playerClasses);
        set => playerClasses = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => (PlayerClass)Enum.Parse(typeof(PlayerClass), s)).ToList();
    }

    public float GetChance()
    {
        return chance;
    }

    public List<DecomposedItem> GetItems()
    {
        return items ?? [];
    }

    public bool IsApplicableTo(Player player)
    {
        return (race == Race.PC_ALL || race == player.GetRace()) && (playerClasses == null || playerClasses.Contains(player.GetPlayerClass()))
            && player.GetLevel() >= minLevel && player.GetLevel() <= maxLevel;
    }

    public bool Roll()
    {
        return Rnd.Get(1, 1000) <= JRound(chance * 10); // Java Math.round
    }

    /// <summary>The one alternative this branch yields, or null if the alternatives don't cover the whole roll.</summary>
    public DecomposedItem SelectItem()
    {
        int roll = Rnd.Get(1, 10000);
        int sum = 0;
        foreach (DecomposedItem item in GetItems())
        {
            sum += JRound(item.GetChance() * 100); // Java Math.round
            if (roll <= sum)
                return item;
        }
        return null;
    }

    /// <summary>Java <c>Math.round(float)</c>: half up, which C# <c>MathF.Round</c> is not.</summary>
    private static int JRound(float a) => (int)Math.Floor(a + 0.5f);
}

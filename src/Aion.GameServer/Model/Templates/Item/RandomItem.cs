using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>
/// One random-reward entry in a decomposable item (type + count range).
/// Java parity: model/templates/item/RandomItem (@XmlType("RandomItem")).
/// </summary>
[XmlType("RandomItem")]
public class RandomItem
{
    [XmlAttribute("type")] public RandomType Type { get; set; }
    [XmlAttribute("min_count")] public int MinCount { get; set; } = 1;
    [XmlAttribute("max_count")] public int MaxCount { get; set; }

    // Java parity: afterUnmarshal — validate counts and default maxCount. Invoked post-load by the item loader.
    public void AfterUnmarshal()
    {
        if (MinCount <= 0)
            throw new ArgumentException("Decomposable random reward item of type " + Type + " min_count (" + MinCount + ") must be greater than 0");
        if (MaxCount == 0)
            MaxCount = MinCount;
        else if (MaxCount < MinCount)
            throw new ArgumentException(
                "Decomposable random reward item of type " + Type + " max_count (" + MaxCount + ") must be unset or greater than min_count (" + MinCount + ")");
    }

    // Java getType() — C# can't name a method GetType() (reserved by Object); use the Type property.
    public int GetMinCount() => MinCount;
    public int GetMaxCount() => MaxCount;
}

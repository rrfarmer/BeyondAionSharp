using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items.Purification;

/// <summary>Java parity: model/templates/item/purification/PurificationResult (Ranastic, Navyan, Estrayl).</summary>
[XmlRoot("PurificationResult")]
public class PurificationResult
{
    [XmlAttribute("result_item_id")] public int resultItemId;
    [XmlAttribute("min_enchant_count")] public int minEnchantCount;
    [XmlAttribute("necessary_abyss_points")] public int necessaryAbyssPoints;
    [XmlAttribute("necessary_kinah")] public long necessaryKinah;
    [XmlElement("req_material")] public List<RequiredMaterial> requiredMaterials;

    public int GetResultItemId()
    {
        return resultItemId;
    }

    public int GetMinEnchantCount()
    {
        return minEnchantCount;
    }

    public int GetNecessaryAbyssPoints()
    {
        return necessaryAbyssPoints;
    }

    public long GetNecessaryKinah()
    {
        return necessaryKinah;
    }

    public List<RequiredMaterial> GetRequiredMaterials()
    {
        return requiredMaterials;
    }
}

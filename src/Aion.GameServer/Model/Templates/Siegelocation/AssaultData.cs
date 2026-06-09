using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Siege;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/AssaultData (Estrayl).</summary>
[XmlType("AssaultData")]
public class AssaultData
{
    [XmlElement("assaulter")] private List<AssaulterTemplate> assaulterTemplates;

    [XmlAttribute("dredgion_id")] private int dredgionId;
    [XmlAttribute("base_budget")] private int baseBudget;
    [XmlAttribute("base_delay")] private int baseDelay;

    // Java parity: EnumMap<AssaulterType, List<Assaulter>> → Dictionary.
    [XmlIgnore] private Dictionary<AssaulterType, List<Assaulter>> processedAssaulters = new Dictionary<AssaulterType, List<Assaulter>>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        foreach (AssaulterTemplate a in assaulterTemplates)
        {
            AssaulterType type = a.GetAssaulterType();
            List<int> npcIds = a.GetNpcIds();
            List<float> spawnCosts = type.GetSpawnCosts();
            List<Assaulter> processed = new List<Assaulter>();
            for (int i = 0; i < npcIds.Count; i++)
            {
                if (type == AssaulterType.TELEPORT)
                    processed.Add(new Assaulter(npcIds[i], 0, a.GetHeadingOffset(), a.GetDistanceOffset()));
                else if (i < spawnCosts.Count)
                    processed.Add(new Assaulter(npcIds[i], spawnCosts[i], a.GetHeadingOffset(), a.GetDistanceOffset()));
            }
            processedAssaulters[type] = processed;
        }
        assaulterTemplates = null;
    }

    public Dictionary<AssaulterType, List<Assaulter>> GetProcessedAssaulters()
    {
        return processedAssaulters;
    }

    public int GetDredgionId()
    {
        return dredgionId;
    }

    public int GetBaseBudget()
    {
        return baseBudget;
    }

    public int GetBaseDelay()
    {
        return baseDelay;
    }
}

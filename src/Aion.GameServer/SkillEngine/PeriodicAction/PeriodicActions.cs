using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.periodicaction;

/// <summary>
/// Java parity: skillengine/periodicaction/PeriodicActions (antness). JAXB @XmlType(PeriodicActions) +
/// @XmlElements(hpuse/mpuse) -> stacked [XmlElement] with typeof; @XmlAttribute(checktime).
/// </summary>
[XmlType("PeriodicActions")]
public class PeriodicActions
{
    [XmlElement("hpuse", typeof(HpUsePeriodicAction))]
    [XmlElement("mpuse", typeof(MpUsePeriodicAction))]
    protected List<PeriodicAction> periodicActions;

    [XmlAttribute("checktime")]
    protected int checktime;

    public List<PeriodicAction> GetPeriodicActions()
    {
        return periodicActions;
    }

    public int GetChecktime()
    {
        return checktime;
    }
}

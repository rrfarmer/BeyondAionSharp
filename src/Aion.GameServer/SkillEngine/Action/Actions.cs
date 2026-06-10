using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Skillengine.Action;

/// <summary>Java parity: skillengine/action/Actions (ATracer). @XmlType("Actions", propOrder={actions}); @XmlElements polymorphic→repeated [XmlElement(name, typeof(T))]; getActions lazily inits live list.</summary>
[XmlType("Actions")]
public class Actions
{
    [XmlElement("itemuse", typeof(ItemUseAction))]
    [XmlElement("mpuse", typeof(MpUseAction))]
    [XmlElement("hpuse", typeof(HpUseAction))]
    [XmlElement("dpuse", typeof(DpUseAction))]
    protected List<Action> actions;

    public List<Action> GetActions()
    {
        if (actions == null)
        {
            actions = new List<Action>();
        }
        return this.actions;
    }
}

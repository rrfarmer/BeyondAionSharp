using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Staticdoor;

/// <summary>Java parity: model/templates/staticdoor/StaticDoorTemplate (Wakizashi).</summary>
[XmlType("StaticDoor")]
public class StaticDoorTemplate : VisibleObjectTemplate
{
    // XmlSerializer binds only public members (JAXB read these via @XmlAccessorType(FIELD)).
    [XmlAttribute("id")] public int id;
    [XmlAttribute("keyid")] public int keyId;
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("state")] public int state;

    public int GetId()
    {
        return id;
    }

    public int GetKeyId()
    {
        return keyId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public int GetState()
    {
        return state;
    }

    public override int GetTemplateId()
    {
        return 300001;
    }

    public override string GetName()
    {
        return "door";
    }

    public override int GetL10nId()
    {
        return 0;
    }
}

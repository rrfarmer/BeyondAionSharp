using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/MailPart (Rolandas).</summary>
[XmlType("MailPart")]
[XmlInclude(typeof(Sender))]
[XmlInclude(typeof(Header))]
[XmlInclude(typeof(Body))]
[XmlInclude(typeof(Tail))]
[XmlInclude(typeof(Title))]
public abstract class MailPart : StringParamList, IMailFormatter
{
    [XmlAttribute("id")] protected int? id;

    // Java parity: getType() — renamed GetType_ (GetType collides with object.GetType()).
    public virtual MailPartType GetType_()
    {
        return MailPartType.CUSTOM;
    }

    public int? GetId()
    {
        return id;
    }

    public string GetFormattedString(IMailFormatter customFormatter)
    {
        IMailFormatter formatter = this;
        if (customFormatter != null)
        {
            formatter = customFormatter;
        }

        string result = GetFormattedString(GetType_());

        string[] paramValues = new string[GetParams().Count];
        for (int i = 0; i < GetParams().Count; i++)
        {
            Param param = GetParams()[i];
            paramValues[i] = formatter.GetParamValue(param.GetId());
        }
        string joinedParams = string.Join(",", paramValues);
        if (result.Length == 0)
            return joinedParams;
        else if (joinedParams.Length != 0)
            result += "," + joinedParams;

        return result;
    }

    public virtual string GetFormattedString(MailPartType partType)
    {
        string result = "";
        if (id > 0)
            result += id.ToString();
        return result;
    }

    public abstract string GetParamValue(string name);
}

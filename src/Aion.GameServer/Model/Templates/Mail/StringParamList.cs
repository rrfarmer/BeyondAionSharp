using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Mail;

/// <summary>Java parity: model/templates/mail/StringParamList (Rolandas).</summary>
[XmlInclude(typeof(MailPart))]
public class StringParamList
{
    [XmlElement("param")] protected List<Param> @params;

    public List<Param> GetParams()
    {
        return @params == null ? new List<Param>() : @params;
    }

    /// <summary>Java parity: StringParamList.Param.</summary>
    [XmlType(AnonymousType = true)]
    public class Param
    {
        [XmlAttribute("id")] protected string id;

        public string GetId()
        {
            return id;
        }
    }
}

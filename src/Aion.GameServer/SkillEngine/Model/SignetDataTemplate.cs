using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Signet skill data table. Java parity: skillengine/model/SignetDataTemplate (@XmlType("signet_data_template")).</summary>
[XmlType("signet_data_template")]
public class SignetDataTemplate
{
    [XmlAttribute("signet_skill")] public SignetEnum Signet { get; set; }
    [XmlElement("signet_data")] public List<SignetData>? SignetDataList { get; set; }

    // Java parity: getSignet()
    public SignetEnum GetSignet() => Signet;

    // Java parity: getSignetDataForSignetLevel(int)
    public SignetData? GetSignetDataForSignetLevel(int level)
    {
        if (SignetDataList != null)
        {
            foreach (SignetData data in SignetDataList)
            {
                if (data.GetLevel() == level)
                    return data;
            }
        }
        return null;
    }
}

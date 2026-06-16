using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/SignetDataTemplates. @XmlRootElement(signet_data_templates); EnumMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("signet_data_templates")]
public class SignetDataTemplates
{
    [XmlElement("signet_data_template")] public List<SignetDataTemplate> signetDataTemplateList;

    [XmlIgnore] private Dictionary<SignetEnum, SignetDataTemplate> signets = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (SignetDataTemplate data in signetDataTemplateList)
        {
            signets[data.GetSignet()] = data;
        }
        signetDataTemplateList = null;
    }

    public SignetData GetSignetData(SignetEnum signet, int level)
    {
        if (signets.TryGetValue(signet, out var template) && template != null)
        {
            return template.GetSignetDataForSignetLevel(level);
        }
        return null;
    }

    public int Size()
    {
        return signets.Count;
    }
}

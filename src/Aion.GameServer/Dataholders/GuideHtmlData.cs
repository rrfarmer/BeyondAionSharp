using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Guides;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GuideHtmlData (xTz). @XmlRootElement(guides); class/race ordinal hash; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("guides")]
public class GuideHtmlData
{
    [XmlElement("guide", typeof(GuideTemplate))]
    private List<GuideTemplate> guideTemplates;

    [XmlIgnore] private readonly Dictionary<int, List<GuideTemplate>> templates = new();
    private const int CLASS_ALL = 255;

    public void AfterUnmarshal(object parent)
    {
        foreach (GuideTemplate template in guideTemplates)
        {
            AddTemplate(template);
        }
        guideTemplates = null;
    }

    private void AddTemplate(GuideTemplate template)
    {
        Race race = template.GetRace() ?? Race.PC_ALL;
        int classId = template.GetPlayerClass() == null ? CLASS_ALL : (int)template.GetPlayerClass().Value;

        int hash = MakeHash(classId, (int)race, template.GetLevel());
        if (!templates.TryGetValue(hash, out var value))
        {
            value = new List<GuideTemplate>();
            templates[hash] = value;
        }
        value.Add(template);
    }

    public int Size()
    {
        return templates.Count;
    }

    public Dictionary<int, List<GuideTemplate>> GetTemplates()
    {
        return templates;
    }

    public GuideTemplate GetTemplateByTitle(string title)
    {
        return templates.Values.SelectMany(v => v).FirstOrDefault(t => t.GetTitle().Equals(title));
    }

    public GuideTemplate[] GetTemplatesFor(PlayerClass playerClass, Race race, int level)
    {
        List<GuideTemplate> guideTemplate = new();

        List<GuideTemplate> classRaceSpecificTemplates = templates.TryGetValue(MakeHash((int)playerClass, (int)race, level), out var a) ? a : null;
        List<GuideTemplate> classSpecificTemplates = templates.TryGetValue(MakeHash((int)playerClass, (int)Race.PC_ALL, level), out var b) ? b : null;
        List<GuideTemplate> raceSpecificTemplates = templates.TryGetValue(MakeHash(CLASS_ALL, (int)race, level), out var c) ? c : null;
        List<GuideTemplate> generalTemplates = templates.TryGetValue(MakeHash(CLASS_ALL, (int)Race.PC_ALL, level), out var d) ? d : null;

        if (classRaceSpecificTemplates != null)
            guideTemplate.AddRange(classRaceSpecificTemplates);
        if (classSpecificTemplates != null)
            guideTemplate.AddRange(classSpecificTemplates);
        if (raceSpecificTemplates != null)
            guideTemplate.AddRange(raceSpecificTemplates);
        if (generalTemplates != null)
            guideTemplate.AddRange(generalTemplates);

        return guideTemplate.ToArray();
    }

    private static int MakeHash(int classType, int race, int level)
    {
        int result = classType << 8;
        result = (result | race) << 8;
        return result | level;
    }
}

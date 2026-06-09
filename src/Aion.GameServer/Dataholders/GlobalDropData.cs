using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.GlobalDrops;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/GlobalDropData (AionCool, Bobobear, Neon). @XmlRootElement(global_rules).</summary>
[XmlRoot("global_rules")]
public class GlobalDropData
{
    [XmlElement("gd_rule")] private List<GlobalRule> globalDropRules;

    public void ProcessRules(ICollection<NpcTemplate> npcs)
    {
        List<NpcTemplate> npcList = new(npcs);
        foreach (GlobalRule gr in globalDropRules)
        {
            if (gr.GetGlobalRuleNpcNames() != null)
            {
                List<GlobalDropNpc> allowedNpcs = GetAllowedNpcs(gr, npcList);
                if (allowedNpcs.Count != 0)
                {
                    gr.SetNpcs(new GlobalDropNpcs());
                    gr.GetGlobalRuleNpcs().AddNpcs(allowedNpcs);
                    gr.GetGlobalRuleNpcNames().GetGlobalDropNpcNames().Clear();
                }
            }
        }
    }

    private List<GlobalDropNpc> GetAllowedNpcs(GlobalRule rule, List<NpcTemplate> npcs)
    {
        List<GlobalDropNpc> allowedNpcs = new();
        if (rule.GetGlobalRuleNpcs() != null)
        {
            allowedNpcs = rule.GetGlobalRuleNpcs().GetGlobalDropNpcs();
        }
        if (rule.GetGlobalRuleNpcNames() != null)
        {
            foreach (GlobalDropNpcName gdNpcName in rule.GetGlobalRuleNpcNames().GetGlobalDropNpcNames())
            {
                List<NpcTemplate> matchedNpcs = new();
                if (gdNpcName.GetFunction() == StringFunction.Contains)
                    matchedNpcs = npcs.Where(npc => npc.GetName().Contains(gdNpcName.GetValue().ToLower())).ToList();
                else if (gdNpcName.GetFunction() == StringFunction.EndWith)
                    matchedNpcs = npcs.Where(npc => npc.GetName().EndsWith(gdNpcName.GetValue().ToLower())).ToList();
                else if (gdNpcName.GetFunction() == StringFunction.StartWith)
                    matchedNpcs = npcs.Where(npc => npc.GetName().StartsWith(gdNpcName.GetValue().ToLower())).ToList();
                else if (gdNpcName.GetFunction() == StringFunction.Equals)
                {
                    matchedNpcs = npcs.Where(npc => string.Equals(npc.GetName(), gdNpcName.GetValue(), StringComparison.OrdinalIgnoreCase)).ToList();
                }
                foreach (NpcTemplate npc in matchedNpcs)
                {
                    GlobalDropNpc gdNpc = new();
                    gdNpc.SetNpcId(npc.GetTemplateId());
                    if (!allowedNpcs.Contains(gdNpc))
                    {
                        allowedNpcs.Add(gdNpc);
                    }
                }
            }
        }
        return allowedNpcs;
    }

    public List<GlobalRule> GetAllRules()
    {
        return globalDropRules;
    }

    public int Size()
    {
        return globalDropRules.Count;
    }
}

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
    // Public for XmlSerializer binding (JAXB read this private field via @XmlAccessorType(FIELD); XmlSerializer
    // only binds public members). Java field name preserved; getters below keep the Java API surface.
    [XmlElement("gd_rule")] public List<GlobalRule> globalDropRules;

    /// <summary>
    /// Java parity: the static_data import graph merges the global_drops/rules dir (singleRootTag) into a single
    /// global_rules root before unmarshalling. The C# leaf-holder loader deserializes each rules file separately,
    /// so this appends another file's gd_rule rows into this holder (mirrors TryLoadMergedHolder's MergePending).
    /// </summary>
    public void MergePending(GlobalDropData other)
    {
        if (other.globalDropRules == null)
            return;
        globalDropRules ??= new List<GlobalRule>();
        globalDropRules.AddRange(other.globalDropRules);
    }

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

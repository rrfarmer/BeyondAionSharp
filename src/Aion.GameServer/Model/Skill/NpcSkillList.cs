using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/NpcSkillList (ATracer, Yeats, Neon). Collections.emptyList→new List; iterator+remove→backward-index removal; prios.sort(reverseOrder)→OrderByDescending; mapToInt.toArray→ToArray. DataManager/NpcSkillTemplate(s)/Rnd red-tolerated.</summary>
public class NpcSkillList
{
    private static readonly ILogger log = NullLogger.Instance;

    private List<NpcSkillEntry> skills;
    private int[] priorities;

    public NpcSkillList(Npc owner)
    {
        InitSkillList(owner.GetNpcId());
    }

    private void InitSkillList(int npcId)
    {
        NpcSkillTemplates npcSkillTemplates = DataManager.NPC_SKILL_DATA.GetNpcSkillList(npcId);
        List<NpcSkillTemplate> npcSkills = npcSkillTemplates == null ? null : npcSkillTemplates.GetNpcSkills();
        if (npcSkills == null || npcSkills.Count == 0)
        {
            skills = new List<NpcSkillEntry>();
        }
        else
        {
            skills = new List<NpcSkillEntry>(npcSkills.Count);
            List<int> prios = new();
            for (int i = npcSkills.Count - 1; i >= 0; i--)
            {
                NpcSkillTemplate template = npcSkills[i];
                if (DataManager.SKILL_DATA.GetSkillTemplate(template.GetSkillId()) == null)
                {
                    log.LogWarning("Missing skill " + template.GetSkillId() + " for npc " + npcId);
                    npcSkills.RemoveAt(i);
                    continue;
                }
                skills.Add(new NpcSkillTemplateEntry(template));
                if (!prios.Contains(template.GetPriority()))
                {
                    prios.Add(template.GetPriority());
                }
            }
            // Java iterated forward (skills/prios preserve npcSkills order); restore by reversing the backward pass
            skills.Reverse();
            prios.Reverse();
            prios.Sort((a, b) => b.CompareTo(a));
            priorities = prios.ToArray();
        }
    }

    public bool IsEmpty()
    {
        return skills.Count == 0;
    }

    public NpcSkillEntry GetRandomSkill()
    {
        return Rnd.Get(skills);
    }

    public NpcSkillEntry GetSkillOnPosition(int position)
    {
        if (skills.Count == 0)
            return null;
        if (position >= skills.Count)
            position = skills.Count - 1;

        return skills[position];
    }

    public List<NpcSkillEntry> GetPostSpawnSkills()
    {
        List<NpcSkillEntry> filteredSkills = new();
        foreach (NpcSkillEntry skill in skills)
            if (skill.HasPostSpawnCondition())
                filteredSkills.Add(skill);
        return filteredSkills;
    }

    public List<NpcSkillEntry> GetNpcSkills()
    {
        return skills;
    }

    public List<NpcSkillEntry> GetSkillsByPriority(int priority)
    {
        if (skills.Count == 0)
            return new List<NpcSkillEntry>();

        List<NpcSkillEntry> skillsByPriority = new();
        foreach (NpcSkillEntry skill in skills)
        {
            if (skill.GetPriority() == priority)
            {
                skillsByPriority.Add(skill);
            }
        }
        return skillsByPriority;
    }

    public int[] GetPriorities()
    {
        return priorities;
    }

    public List<NpcSkillEntry> GetChainSkills(NpcSkillEntry curSkill)
    {
        if (skills.Count == 0)
            return new List<NpcSkillEntry>();

        List<NpcSkillEntry> chainSkills = new();
        int id = curSkill.GetNextChainId();
        if (id > 0)
        {
            foreach (NpcSkillEntry skill in skills)
            {
                if (skill.GetChainId() == id)
                {
                    chainSkills.Add(skill);
                }
            }
        }
        return chainSkills;
    }
}

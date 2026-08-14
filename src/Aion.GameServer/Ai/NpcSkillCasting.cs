using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Ai;

/// <summary>
/// Helpers for AI classes that drive their own skill rotation.
/// </summary>
/// <remarks>
/// Retail AI patterns pick skills by index into the NPC's skill list and say nothing about
/// level, so a hand-written rotation should take the level from that NPC's own npc_skills
/// entry rather than hardcode one. Levels differ between an encounter's normal and hard
/// variants, and hardcoding would silently weaken or overtune a cast.
/// <para>
/// The convention for such NPCs is to leave their npc_skills entries at <c>prob="0"</c>: the
/// list still defines each skill's level, but nothing fires except what the AI queues.
/// </para>
/// </remarks>
public static class NpcSkillCasting
{
    /// <summary>
    /// Queues one of <paramref name="owner"/>'s own skills at the level its npc_skills entry
    /// defines, falling back to <paramref name="fallbackLevel"/> when the skill is not listed.
    /// </summary>
    public static void QueueAtDataLevel(Npc owner, int skillId, NpcSkillTargetAttribute target,
        int fallbackLevel = 1)
    {
        owner.QueueSkill(skillId, LevelOf(owner, skillId, fallbackLevel), 0, target);
    }

    /// <summary>Returns the level an NPC's npc_skills entry gives a skill.</summary>
    public static int LevelOf(Npc owner, int skillId, int fallbackLevel = 1)
    {
        NpcSkillList? skills = owner.GetSkillList();
        if (skills == null)
            return fallbackLevel;
        foreach (NpcSkillEntry entry in skills.GetNpcSkills())
        {
            if (entry.GetSkillId() == skillId)
                return entry.GetSkillLevel();
        }
        return fallbackLevel;
    }
}

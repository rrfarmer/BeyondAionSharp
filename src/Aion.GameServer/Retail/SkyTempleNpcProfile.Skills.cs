namespace Aion.GameServer.Retail;

// Full-cohort skill-list integration is separate from immutable stat data.
public sealed partial class SkyTempleNpcProfile
{
    internal Aion.GameServer.Model.Skill.NpcSkillList CreateSkillList() => new(Skills.Select(s =>
        new Aion.GameServer.Model.Templates.Npcskill.NpcSkillTemplate { id = s.Id48, lv = s.Level,
            // Rate/count/reuse belong to the scoped retail selector, not Java's per-entry% or randomdelay.
            prob = 0, prio = 0, cd = 0, nextSkillTime = 0 }).ToList());
}

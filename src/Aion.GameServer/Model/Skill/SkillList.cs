using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/SkillList&lt;T extends Creature&gt;.</summary>
public interface SkillList<T> where T : Creature
{
    /// <summary>Add skill to list. Returns true if operation was successful.</summary>
    bool AddSkill(T creature, int skillId, int skillLevel);

    /// <summary>Remove skill from list. Returns true if operation was successful.</summary>
    bool RemoveSkill(int skillId);

    /// <summary>Check whether skill is present in list.</summary>
    bool IsSkillPresent(int skillId);

    int GetSkillLevel(int skillId);

    /// <summary>Size of skill list.</summary>
    int Size();
}

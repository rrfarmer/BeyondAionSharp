using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;

namespace Aion.GameServer.Ai;

/// <summary>A queued skill that already knows which creature it is for.</summary>
/// <remarks>
/// Retail names a skill's target by its role in the event that fired the branch -- the creature that
/// started the fight, the one that just hit us, the one a message was about. This port could say none
/// of them, and the reason was structural rather than a missing name: <see cref="NpcSkillTargetAttribute"/>
/// is resolved by <c>SkillAttackManager</c> <b>when the queue drains</b>, out of the aggro list, and
/// these creatures are not on it by rank. 246 casts and about 90 summons were refused for that.
/// <para>
/// So the queue carries the creature instead of a rule for finding one. The creature is captured when
/// the branch runs, which is what retail means -- the attacker is whoever hit us at that moment, not
/// whoever happens to be hitting us later when the cast comes up.
/// </para>
/// </remarks>
public interface IAimedSkill
{
    /// <summary>The creature this cast was aimed at when its branch ran.</summary>
    Creature? Aim { get; }
}

/// <summary>A <see cref="NpcSkillTemplateEntry"/> that carries its own target.</summary>
/// <remarks>
/// Deliberately not a change to <see cref="NpcSkillEntry"/> or to the enum. Both are Java-parity model
/// types, and widening either would touch every npc skill in the game to serve one AI feature; this
/// subclasses the concrete entry instead, so nothing that does not ask for an aim can tell the
/// difference.
/// </remarks>
public sealed class AimedSkillEntry : NpcSkillTemplateEntry, IAimedSkill
{
    public AimedSkillEntry(NpcSkillTemplate template, Creature aim)
        : base(template)
    {
        Aim = aim;
    }

    public Creature? Aim { get; }
}

using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/OneDmgAI (xTz, Neon).</summary>
[AIName("onedmg_aggressive")]
public class OneDmgAI : AggressiveNpcAI, INpcMessageListener
{
    public OneDmgAI(Npc owner)
        : base(owner)
    {
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 1;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return 1;
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        switch (stat.GetStat())
        { // ai owner should not evade or resist
            case StatEnum.MAGICAL_RESIST:
            case StatEnum.EVASION:
                stat.SetBase(0);
                stat.SetBonus(0);
                break;
        }
    }

    /// <summary>
    /// Retail's answer to a guard's call for help. three npc(s) on <c>onedmg_aggressive</c> answer
    /// <c>23000</c>, and this class had no message handling at all -- see <see cref="GuardAnswers"/>
    /// for the table and for why these cannot use a pattern.
    /// </summary>
    public void OnNpcMessage(Npc sender, int messageType, VisibleObject? param)
    {
        GuardAnswers.AnswerCall(GetOwner(), sender, messageType, param);
    }
}

using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionConstructAI (Yeats, Estrayl).</summary>
public class AhserionConstructAI : NpcAI
{
    public AhserionConstructAI(Npc owner)
        : base(owner)
    {
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
            stat.SetBaseRate(SiegeConfig.AHSERION_MAX_PLAYERS_PER_TEAM / 100f);
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        if (attacker is Npc && effect != null)
        {
            switch (effect.GetSkillId())
            {
                case 21755: // Bombarding targets.
                case 21578: // Shield Penetration
                case 21583: // Artillery Blast
                case 21584: // Area Bombardment
                    return damage * (SiegeConfig.AHSERION_MAX_PLAYERS_PER_TEAM / 100f);
            }
        }
        return base.ModifyDamage(attacker, damage, effect);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }
}

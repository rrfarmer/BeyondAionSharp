using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("camp_defense_cannon")]
public class CampDefenseCannonAI : AggressiveNoLootNpcAI
{
    public CampDefenseCannonAI(Npc owner) : base(owner)
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

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return ItemAttackType.MAGICAL_FIRE;
    }

    protected override void HandleFinishAttack()
    {
        if (!CanThink())
            return;
        Npc npc = GetOwner();
        EmoteManager.EmoteStopAttacking(npc);
        npc.GetController().LoseAggro(false);
        npc.SetSkillNumber(0);
    }
}

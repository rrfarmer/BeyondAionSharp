using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Handler;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/custom/pvpveMap/ModifiedIronWallAggressiveAI (@author Yeats).</summary>
[AIName("modified_iron_wall_aggressive")]
public class ModifiedIronWallAggressiveAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases;

    public ModifiedIronWallAggressiveAI(Npc owner)
        : base(owner)
    {
        hpPhases = owner.GetNpcId() == 231304 ? new HpPhases(98, 77, 56, 35, 10) : null;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (hpPhases != null)
            hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        GetOwner().QueueSkill(21165, 1, 3000);
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        if (PvpMapService.GetInstance().IsRandomBoss(GetOwner()))
        {
            return damage > 7900 ? (7700 + Rnd.NextInt(600)) : damage;
        }
        switch (GetOwner().GetNpcId())
        {
            case 218547: // cleric
            case 219197:
            case 218551:
            case 219190:
            case 219169:
            case 218546: // mage
            case 218550:
            case 219196:
            case 219189:
            case 219168:
                if (effect != null)
                {
                    return GetRandomDmg(damage, 1.2f, 1.6f);
                }
                else
                {
                    return GetRandomDmg(damage, 2.4f, 3.2f);
                }
            case 219218: // keymaster chookuri (cleric, normal)
            case 219193: // keymaster dabra (mage, hero)
            case 219191: // keymaster niksi (fighter, elite)
            case 219192: // keymaster zumita (assa, elite)
                return damage;
            default:
                if (effect != null)
                {
                    return GetRandomDmg(damage, 2f, 2.8f);
                }
                else
                {
                    return GetRandomDmg(damage, 1.8f, 2.3f);
                }
        }
    }

    public override void ModifyOwnerStat(Stat2 stat)
    {
        if (stat.GetStat() == StatEnum.MAXHP)
        {
            if (PvpMapService.GetInstance().IsRandomBoss(GetOwner()))
            {
                stat.SetBase(882321);
            }
            else
            {
                switch (GetOwner().GetNpcId())
                {
                    case 219167:
                    case 219169:
                    case 219190:
                        stat.SetBaseRate(3.2f);
                        break;
                    case 219218:
                        stat.SetBaseRate(7f);
                        break;
                    case 219193:
                    case 219191:
                    case 219192:
                        break;
                    default:
                        stat.SetBaseRate(1.6f);
                        break;
                }
            }
        }
        else
        {
            base.ModifyOwnerStat(stat);
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (GetOwner().GetNpcId() == 231304)
        {
            switch (skillTemplate.GetSkillId())
            {
                case 21165:
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        Creature randomTarget = GetAggroList().GetTarget(AggroTarget.RANDOM, 26);
                        if (randomTarget == null)
                            return ValueTask.CompletedTask;
                        World.World.GetInstance().UpdatePosition(GetOwner(), randomTarget.GetX(), randomTarget.GetY(), randomTarget.GetZ(), randomTarget.GetHeading());
                        PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
                        ThreadPoolManager.GetInstance()
                            .Schedule(_ => { GetOwner().QueueSkill(21171, 1, 6000); return ValueTask.CompletedTask; }, 500L);
                        return ValueTask.CompletedTask;
                    }, 500L);
                    break;
            }
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        if (hpPhases != null)
            hpPhases.Reset();
    }

    protected override void HandleNotAtHome()
    {
        if (GetOwner().GetNpcId() == 231304)
        {
            World.World.GetInstance().UpdatePosition(GetOwner(), GetOwner().GetSpawn().GetX(), GetOwner().GetSpawn().GetY(), GetOwner().GetSpawn().GetZ(),
                GetOwner().GetSpawn().GetHeading());
            PacketSendUtility.BroadcastPacketAndReceive(GetOwner(), new SM_FORCED_MOVE(GetOwner(), GetOwner()));
            if (hpPhases != null)
                hpPhases.Reset();
            ReturningEventHandler.OnBackHome(this);
        }
        else
        {
            base.HandleNotAtHome();
        }
    }

    private float GetRandomDmg(float damage, float minMultiplier, float maxMultiplier)
    {
        return damage * Rnd.NextFloat(minMultiplier, maxMultiplier);
    }
}

using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("vasuki_lifespark")]
public class VasukiLifesparkAI : AggressiveNpcAI
{
    private readonly AtomicBoolean startedEvent = new AtomicBoolean();
    private bool think = false;

    public VasukiLifesparkAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return think;
    }

    protected override void HandleSpawned()
    {
        if (GetNpcId() == 217764)
        {
            think = true;
        }
        else
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                if (!IsDead())
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19126, 46, GetOwner()).UseNoAnimationSkill();
                return ValueTask.CompletedTask;
            }, 3000L);
        }
        base.HandleSpawned();
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player)
        {
            if (PositionUtil.GetDistance(GetOwner(), player) <= 30)
            {
                if (startedEvent.CompareAndSet(false, true))
                {
                    int level;
                    int shoutId;
                    int skill;
                    switch (GetNpcId())
                    {
                        case 217760:
                            skill = 19972;
                            level = 45;
                            shoutId = 1401107;
                            break;
                        case 217761:
                            skill = 19972;
                            level = 46;
                            shoutId = 1401171;
                            break;
                        case 217763:
                            skill = 20087;
                            level = 46;
                            shoutId = 0;
                            break;
                        default:
                            skill = 20039;
                            level = 46;
                            shoutId = 1401110;
                            break;
                    }
                    if (shoutId != 0)
                    {
                        PacketSendUtility.BroadcastMessage(GetOwner(), shoutId);
                    }
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skill, level, GetOwner()).UseNoAnimationSkill();
                    if (GetNpcId() != 217764)
                    {
                        ThreadPoolManager.GetInstance().Schedule(ct =>
                        {
                            if (!IsDead())
                            {
                                if (GetNpcId() == 217763)
                                {
                                    GetPosition().GetWorldMapInstance().SetDoorState(219, true);
                                }
                                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19967, level, GetOwner()).UseNoAnimationSkill();
                                ThreadPoolManager.GetInstance().Schedule(ct2 =>
                                {
                                    if (!IsDead())
                                        AIActions.DeleteOwner(this);
                                    return ValueTask.CompletedTask;
                                }, 3500L);
                            }
                            return ValueTask.CompletedTask;
                        }, 3500L);
                    }
                    else
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19974, 46, GetOwner()).UseNoAnimationSkill();
                    }
                }
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.IS_IMMUNE_TO_ABNORMAL_STATES => true,
            _ => base.Ask(question),
        };
    }

    protected override void HandleDied()
    {
        if (GetNpcId() == 217764)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1401111);
            Npc soul = GetPosition().GetWorldMapInstance().GetNpc(217471);
            Npc sapping = GetPosition().GetWorldMapInstance().GetNpc(217472);
            if (soul != null)
            {
                soul.GetEffectController().RemoveEffect(19126);
            }
            if (sapping != null)
            {
                sapping.GetEffectController().RemoveEffect(19126);
            }
            PacketSendUtility.BroadcastToMap(GetOwner(), 1401140);
        }
        base.HandleDied();
    }
}

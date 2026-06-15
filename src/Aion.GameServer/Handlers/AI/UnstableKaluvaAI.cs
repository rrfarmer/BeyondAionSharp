using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/unstableSplinterpath/UnstableKaluvaAI (@author Luzien, Cheatkiller).
/// </summary>
[AIName("unstablekaluva")]
public class UnstableKaluvaAI : AggressiveNpcAI
{
    private bool canThink = true;
    private bool isInMove = false;

    public UnstableKaluvaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (Rnd.Chance() < 3)
        {
            if (!isInMove)
            {
                isInMove = true;
                MoveToSpawner(RandomEgg());
            }
        }
    }

    private void MoveToSpawner(int egg)
    {
        Npc spawner = GetPosition().GetWorldMapInstance().GetNpc(egg);
        if (spawner != null)
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19152, 55, GetOwner()).UseNoAnimationSkill();
            canThink = false;
            EmoteManager.EmoteStopAttacking(GetOwner());
            SetStateIfNot(AIState.FOLLOWING);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
            GetOwner().SetTarget(spawner);
            GetMoveController().MoveToTargetObject();
        }
    }

    protected override void HandleMoveArrived()
    {
        if (!canThink)
        {
            if (GetOwner().GetTarget() is Npc spawner)
            {
                spawner.GetEffectController().RemoveEffect(19222);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19223, 55, spawner).UseNoAnimationSkill();
                GetEffectController().RemoveEffect(19152);
            }

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                canThink = true;
                Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                if (creature != null)
                {
                    GetOwner().SetTarget(creature);
                    GetOwner().GetGameStats().RenewLastAttackTime();
                    GetOwner().GetGameStats().RenewLastAttackedTime();
                    GetOwner().GetGameStats().RenewLastSkillTime();
                }
                SetStateIfNot(AIState.FIGHT);
                Think();
                return ValueTask.CompletedTask;
            }, 2000L);
            isInMove = false;
        }
        base.HandleMoveArrived();
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isInMove = false;
    }

    private int RandomEgg()
    {
        int[] npcIds = { 219971, 219952, 219970, 219969 };
        return Rnd.Get(npcIds);
    }

    public override bool CanThink()
    {
        return canThink;
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}

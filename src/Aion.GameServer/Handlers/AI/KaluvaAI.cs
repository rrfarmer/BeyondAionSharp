using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyssal_splinter/KaluvaAI (@author Luzien).</summary>
[AIName("kaluva")]
public class KaluvaAI : SummonerAI
{
    private bool canThink = true;

    public KaluvaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        VisibleObject kaluvasSpawner = Spawn();
        canThink = false;
        EmoteManager.EmoteStopAttacking(GetOwner());
        SetStateIfNot(AIState.FOLLOWING);
        GetOwner().SetState(CreatureState.ACTIVE, true);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
        GetOwner().SetTarget(kaluvasSpawner);
        GetMoveController().MoveToTargetObject();
    }

    protected override void HandleMoveArrived()
    {
        if (!canThink)
        {
            Npc egg = GetPosition().GetWorldMapInstance().GetNpc(281902);
            if (egg != null)
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19223, 55, egg).UseNoAnimationSkill();
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
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, 2000L);
        }
        base.HandleMoveArrived();
    }

    private VisibleObject Spawn()
    {
        return Rnd.Get(1, 4) switch
        {
            1 => Spawn(281902, 663.322021f, 556.731995f, 424.295013f, (sbyte)64),
            2 => Spawn(281902, 644.0224f, 523.9641f, 423.09103f, (sbyte)32),
            3 => Spawn(281902, 611.008f, 539.73395f, 423.25034f, (sbyte)119),
            _ => Spawn(281902, 628.4426f, 585.4443f, 424.31854f, (sbyte)93),
        };
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

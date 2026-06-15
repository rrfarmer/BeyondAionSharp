using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller, Estrayl
/// </summary>
[AIName("gravity_crusher")]
public class GravityCrusherAI : AggressiveNpcAI
{
    public GravityCrusherAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AIActions.TargetCreature(this, Rnd.Get(instance.GetPlayersInside()));
            SetStateIfNot(AIState.WALKING);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            GetMoveController().MoveToTargetObject();
            PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.WALK));
            Transform();
            return ValueTask.CompletedTask;
        }, 2000L);
    }

    private void Transform()
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
                GetOwner().QueueSkill(GetNpcId() == 283141 ? 20967 : 21900, 1);
            return ValueTask.CompletedTask;
        }, 30000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20967:
            case 21900:
                Spawn(GetOwner().GetLevel() == 65 ? GetNpcId() + 1 : GetNpcId() - 1, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                    (sbyte)GetOwner().GetHeading());
                AIActions.DeleteOwner(this);
                break;
        }
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        GetOwner().QueueSkill(20987, 1);
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}

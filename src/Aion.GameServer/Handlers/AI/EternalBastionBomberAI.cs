using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionBomberAI (@author Cheatkiller, Estrayl).</summary>
[AIName("eternal_bastion_bomber")]
public class EternalBastionBomberAI : EternalBastionAggressiveNpcAI
{
    private readonly AtomicBoolean isExplosionActivated = new AtomicBoolean();

    public EternalBastionBomberAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            StartWalking();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 3000L);
    }

    private void StartWalking()
    {
        WalkManager.StartWalking(this);
        GetOwner().UnsetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isExplosionActivated.CompareAndSet(false, true))
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21143, 1, GetOwner()).UseSkill();
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21143)
            isExplosionActivated.Set(true);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21143)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20549, 1, GetOwner()).UseSkill();
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, 750L); // Self-Destruction
            Spawn(284707, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0); // deals ~500k damage to gates
            Spawn(284707, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0); // not 100% sure about this second one
        }
    }
}

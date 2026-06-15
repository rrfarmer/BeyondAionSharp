using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("shugo_tomb_boss")]
public class ShugoTombBossAI : ShugoTombAttackerAI
{
    private readonly AtomicInteger usedSkills = new AtomicInteger();
    private readonly AtomicBoolean isCasting = new AtomicBoolean();

    public ShugoTombBossAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        List<NpcSkillEntry> skills = GetSkillList().GetNpcSkills();
        if (GetOwner().GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE) || usedSkills.Get() >= skills.Count)
            return;

        NpcSkillEntry entry = skills[usedSkills.Get()];
        if (GetLifeStats().GetHpPercentage() <= entry.GetTemplate().GetMaxhp() && isCasting.CompareAndSet(false, true))
        {
            if (GetMoveController().IsInMove())
                WalkManager.StopWalking(this);

            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), entry.GetSkillId(), entry.GetSkillLevel(), GetOwner()).UseWithoutPropSkill();
            usedSkills.IncrementAndGet();
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21266)
        {
            for (int i = 0; i < 4; i++)
            {
                RndSpawnInRange(219509, 2);
            }
        }
        isCasting.Set(false);
        if (!CanThink())
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                WalkManager.StartWalking(this);
                GetOwner().SetState(CreatureState.WALK_MODE);
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                return ValueTask.CompletedTask;
            }, 500L);
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.REWARD_LOOT => true,
            _ => base.Ask(question),
        };
    }
}

using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/ShulackCaptainAI.
/// Shulack captain of stage 5: his area attacks come from the untargetable stigma amplifier in the middle of the deck, the captain only tells
/// it when to strike.
/// </summary>
[AIName("empyrean_shulack_captain")]
public class ShulackCaptainAI : CrucibleBossAI
{
    private int stigmaIn, attackIn, snareIn;
    private bool secondStigmaSkill;

    public ShulackCaptainAI(Npc owner) : base(owner)
    {
    }

    protected override void OnFightStart()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 341892);
        Cast(18196, 37, NpcSkillTargetAttribute.ME);
        stigmaIn = 60;
        attackIn = 30;
        snareIn = 30;
        secondStigmaSkill = false;
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (--stigmaIn <= 0)
        {
            int[] skills = hp > 70 ? new int[] { 18197, 18198 } : hp > 30 ? new int[] { 18199, 18200 } : new int[] { 18198, 18202 };
            StigmaStrike(skills[secondStigmaSkill ? 1 : 0]);
            secondStigmaSkill = !secondStigmaSkill;
            stigmaIn = hp > 70 ? 60 : hp > 30 ? 40 : 20;
        }
        if (--attackIn <= 0)
        {
            if (hp > 70)
            {
                Cast(18193, 37);
                Cast(18195, 37);
                attackIn = 30;
            }
            else if (hp > 30)
            {
                Cast(18195, 37, NpcSkillTargetAttribute.RANDOM);
                attackIn = 20;
            }
            else
            {
                Cast(18193, 37);
                Cast(18195, 37);
                Cast(18196, 37, NpcSkillTargetAttribute.ME);
                attackIn = 20;
            }
        }
        if (hp <= 70 && --snareIn <= 0)
        {
            Cast(18194, 37);
            if (WeakestAttacker() is Creature weakest)
                GetAggroList().AddHate(weakest, 100000);
            snareIn = 60;
        }
    }

    private void StigmaStrike(int skillId)
    {
        Npc stigma = GetPosition().GetWorldMapInstance().GetNpc(281180);
        if (stigma == null || !(GetOwner().GetTarget() is Creature target))
            return;
        Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(stigma, skillId, 36, target);
        if (skill == null)
            return;
        skill.SetFirstTargetRangeCheck(false); // the stigma strikes the whole deck from where it stands
        skill.UseNoAnimationSkill();
    }
}

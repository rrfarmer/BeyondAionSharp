using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/PriestPreceptorAI (@author Luzien, w4terbomb).
/// The 80 and 30 thresholds are retail's, from pattern IDArena_S7_Named_4; upstream 1af948baf adopted them in place of its own 75/25.
/// See docs/retail-ai-fidelity.md.
/// </summary>
[AIName("priest_preceptor")]
public class PriestPreceptorAI : CrucibleBossAI
{
    private readonly int[] helpers; // warrior, ranger, mage

    public PriestPreceptorAI(Npc owner) : base(owner)
    {
        if (owner.GetNpcId() == 217581) // Thrasymedes
            helpers = new int[] { 282366, 282367, 282368 }; // Boreas, Jumentis, Charna
        else // Freyr
            helpers = new int[] { 282369, 282370, 282371 }; // Traufnir, Sigyn, Sif
    }

    private bool IsLight()
    {
        return GetNpcId() == 217581;
    }

    protected override void OnFightStart()
    {
        Say(IsLight() ? 1500219 : 1500221);
        Cast(19612, 10, NpcSkillTargetAttribute.ME);
    }

    protected override void OnFightTick(int seconds, int hp)
    {
        if (Every(seconds, 10, 15))
        {
            Cast(19587, 10);
            Cast(19588, 15);
        }
        if (Every(seconds, 50, 60))
            Cast(19613, 10, NpcSkillTargetAttribute.RANDOM);
        if (Passed(80))
            Cast(19611, 10);
        if (Passed(30))
        {
            Cast(19610, 10, NpcSkillTargetAttribute.ME);
            Cast(19614, 10, NpcSkillTargetAttribute.ME);
            WorldPosition p = GetPosition();
            for (int i = 0; i < helpers.Length; i++)
            {
                if (Spawn(helpers[i], p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading()) is Npc helper)
                {
                    int sicknessLevel = i == 0 ? 1 : i == 1 ? 2 : 4;
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        helper.QueueSkill(19594, sicknessLevel, -1, NpcSkillTargetAttribute.ME);
                        return ValueTask.CompletedTask;
                    }, TimeSpan.FromMilliseconds(1000));
                }
            }
        }
    }

    protected override void HandleDied()
    {
        Say(IsLight() ? 1500220 : 1500222);
        // the warrior and the mage go with the priest, the ranger only runs for 10 seconds and then fights on
        foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(helpers[0], helpers[2]))
            npc.GetController().DeleteIfAliveOrCancelRespawn();
        Npc ranger = GetPosition().GetWorldMapInstance().GetNpc(helpers[1]);
        if (ranger != null && !ranger.IsDead() && ranger.GetAi() is NpcAI rangerAi)
        {
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(ranger, 19604, 1, ranger).UseNoAnimationSkill();
            CrucibleRun.Start(rangerAi, null, 10000, null);
        }
        base.HandleDied();
    }

    protected override void OnFightEnd()
    {
        if (!IsDead())
            foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(helpers))
                npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}

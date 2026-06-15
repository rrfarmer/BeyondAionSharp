using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/pvpArenas/HarmonyShifterAI (xTz).</summary>
[AIName("harmony_shifter")]
public class HarmonyShifterAI : ShifterAI
{
    private AtomicBoolean isRewarded = new AtomicBoolean(false);

    public HarmonyShifterAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        base.HandleUseItemFinish(player);
        if (isRewarded.CompareAndSet(false, true))
        {
            AIActions.HandleUseItemFinish(this, player);
            switch (GetNpcId())
            {
                case 207116:
                    UseSkill(GetNpcs(207118));
                    UseSkill(GetNpcs(207119));
                    break;
                case 207099:
                    UseSkill(GetNpcs(207100));
                    break;
            }
            AIActions.ScheduleRespawn(this);
            AIActions.DeleteOwner(this);
        }
    }

    private void UseSkill(List<Npc> npcs)
    {
        HarmonyArenaScore instance = (HarmonyArenaScore)GetPosition().GetWorldMapInstance().GetInstanceHandler().GetInstanceScore();
        foreach (Npc npc in npcs)
        {
            int skill = instance.GetNpcBonusSkill(npc.GetNpcId());
            SkillEngine.SkillEngine.GetInstance().GetSkill(npc, skill >> 8, skill & 0xFF, npc).UseNoAnimationSkill();
        }
    }

    private List<Npc> GetNpcs(int npcId)
    {
        return GetPosition().GetWorldMapInstance().GetNpcs(npcId);
    }
}

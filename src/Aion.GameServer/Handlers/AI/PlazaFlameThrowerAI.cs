using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/pvpArenas/PlazaFlameThrowerAI (@author xTz).</summary>
[AIName("plaza_flame_thrower")]
public class PlazaFlameThrowerAI : ShifterAI
{
    private bool isRewarded;

    public PlazaFlameThrowerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        InstanceScore instance = GetPosition().GetWorldMapInstance().GetInstanceHandler().GetInstanceScore();
        if (instance != null && !instance.IsStartProgress())
        {
            return;
        }

        base.HandleDialogStart(player);
    }

    protected override void HandleUseItemFinish(Player player)
    {
        base.HandleUseItemFinish(player);
        if (!isRewarded)
        {
            isRewarded = true;
            AIActions.HandleUseItemFinish(this, player);
            switch (GetNpcId())
            {
                case 701169:
                    UseSkill(GetNpcs(701178));
                    UseSkill(GetNpcs(701192));
                    break;
                case 701170:
                    UseSkill(GetNpcs(701177));
                    UseSkill(GetNpcs(701191));
                    break;
                case 701171:
                    UseSkill(GetNpcs(701176));
                    UseSkill(GetNpcs(701190));
                    break;
                case 701172:
                    UseSkill(GetNpcs(701175));
                    UseSkill(GetNpcs(701189));
                    break;
            }

            AIActions.ScheduleRespawn(this);
            AIActions.DeleteOwner(this);
        }
    }

    private void UseSkill(List<Npc> npcs)
    {
        PvPArenaScore instance = (PvPArenaScore)GetPosition().GetWorldMapInstance().GetInstanceHandler().GetInstanceScore();
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

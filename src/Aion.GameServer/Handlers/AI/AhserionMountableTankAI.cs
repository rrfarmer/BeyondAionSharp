using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionMountableTankAI (@author Estrayl).</summary>
[AIName("ahserion_mountable_tank")]
public class AhserionMountableTankAI : ActionItemNpcAI
{
    private readonly AtomicBoolean canUse = new AtomicBoolean(true);
    private PanesterraFaction ownerFaction;

    public AhserionMountableTankAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ownerFaction = GetNpcId() switch
        {
            277233 or 277238 => PanesterraFaction.BELUS,
            277234 or 277239 => PanesterraFaction.ASPIDA,
            277235 or 277240 => PanesterraFaction.ATANATOS,
            277236 or 277241 => PanesterraFaction.DISILLON,
            _ => PanesterraFaction.BALAUR,
        };
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(12)));
    }

    protected override void HandleUseItemFinish(Player player)
    {
        if (CanUseTank(player) && canUse.CompareAndSet(true, false))
        {
            int skillId = GetSkillId();
            if (skillId != 0)
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), skillId, 65, player).UseNoAnimationSkill();
            AIActions.DeleteOwner(this);
        }
    }

    private bool CanUseTank(Player player)
    {
        if (AhserionRaid.GetInstance().IsStarted())
        {
            PanesterraTeam team = PanesterraService.GetInstance().GetTeam(player);
            return team != null && team.GetFaction() == ownerFaction;
        }
        return false;
    }

    private int GetSkillId()
    {
        return GetNpcId() switch
        {
            // Board the Chariot
            277233 => 21582, // Belus
            277234 => 21589, // Aspida
            277235 => 21590, // Atanatos
            277236 => 21591, // Disillon
            // Board the Ignus Engine
            277238 => 21579, // Belus
            277239 => 21586, // Aspida
            277240 => 21587, // Atanatos
            277241 => 21588, // Disillon
            _ => 0,
        };
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}

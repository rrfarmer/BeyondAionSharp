using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/pvpArenas/AntiAirCraftGunAI (xTz).</summary>
[AIName("antiaircraftgun")]
public class AntiAirCraftGunAI : ActionItemNpcAI
{
    public AntiAirCraftGunAI(Npc owner)
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
        Npc owner = GetOwner();
        TeleportService.TeleportTo(player, owner.GetWorldId(), owner.GetInstanceId(), owner.GetX(), owner.GetY(), owner.GetZ(), owner.GetHeading());
        int morphSkill = 0;
        switch (GetNpcId())
        {
            case 701185: // 46 lvl morph 218803
            case 701321:
                morphSkill = 0x4E502E; // 20048 46
                break;
            case 701199: // 51 lvl morph 218804
            case 701322:
                morphSkill = 0x4E5133; // 20049 51
                break;
            case 701213: // 56 lvl morph 218805
            case 701323:
                morphSkill = 0x4E5238; // 20050 56
                break;
        }
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), morphSkill >> 8, morphSkill & 0xFF, player).UseNoAnimationSkill();
        AIActions.ScheduleRespawn(this);
        AIActions.DeleteOwner(this);
    }
}

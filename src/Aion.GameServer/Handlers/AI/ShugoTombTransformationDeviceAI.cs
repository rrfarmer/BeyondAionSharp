using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/shugoImperialTomb/ShugoTombTransformationDeviceAI (Estrayl).</summary>
[AIName("shugo_tomb_transformation_device")]
public class ShugoTombTransformationDeviceAI : ActionItemNpcAI
{
    public ShugoTombTransformationDeviceAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        ApplyTransformationEffect(player);
        base.HandleUseItemFinish(player);
    }

    private void ApplyTransformationEffect(Player player)
    {
        int skillId = GetNpcId() switch
        {
            831097 => player.GetRace() == Race.ASMODIANS ? 21105 : 21096,
            831096 => player.GetRace() == Race.ASMODIANS ? 21104 : 21095,
            _ => 0,
        };
        if (skillId != 0)
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(skillId, player, player);
                return ValueTask.CompletedTask;
            }, 1000L);
    }
}

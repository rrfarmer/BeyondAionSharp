using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/iceFestival/IceSculptureAI.</summary>
[AIName("icefestival_ice_sculpture")]
public class IceSculptureAI : ActionItemNpcAI
{
    public IceSculptureAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        base.HandleUseItemFinish(player);
        if (Rnd.Chance() < 66)
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 11020, 60, player).UseSkill(); // world_event_icebuff_stat
        else
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 22718, 60, player).UseWithoutPropSkill(); // world_event_npc_frozen
    }
}

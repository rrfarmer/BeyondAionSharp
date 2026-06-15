using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Should also be able to request support once by dropping below 35% HP.
/// Java parity: ai/worlds/panesterra/ahserionsflight/AhserionAssaultCommanderAI (Estrayl).
/// </summary>
[AIName("ahserion_assault_commander")]
public class AhserionAssaultCommanderAI : AhserionAggressiveNpcAI
{
    public AhserionAssaultCommanderAI(Npc owner)
        : base(owner)
    {
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 20648) // Pincer Attack
            AddHateToRndTarget();
    }
}

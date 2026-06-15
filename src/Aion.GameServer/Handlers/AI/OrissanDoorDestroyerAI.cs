using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/OrissanDoorDestroyerAI (@author Estrayl).</summary>
[AIName("orissan_door_destroyer")]
public class OrissanDoorDestroyerAI : GeneralNpcAI
{
    public OrissanDoorDestroyerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleGateDestruction();
    }

    private void ScheduleGateDestruction()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501313, 7000);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (Npc npc in GetPosition().GetWorldMapInstance().GetNpcs(731580))
                if (IsInRange(npc, 15))
                    SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 20840, 1, npc).UseWithoutPropSkill();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 14000L);
    }
}

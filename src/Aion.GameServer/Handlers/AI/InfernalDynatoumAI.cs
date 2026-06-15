using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/InfernalDynatoumAI (@author Estrayl).</summary>
[AIName("infernal_dynatoum")]
public class InfernalDynatoumAI : DynatoumAI
{
    public InfernalDynatoumAI(Npc owner)
        : base(owner)
    {
    }

    protected override void ScheduleDespawn(int delayInSec)
    {
        despawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                switch (delayInSec)
                {
                    case 1:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_HARD_BOSS_TIMER_01());
                        ScheduleDespawn(2);
                        break;
                    case 2:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_HARD_BOSS_TIMER_02());
                        ScheduleDespawn(240);
                        break;
                    case 240:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_HARD_BOSS_TIMER_03());
                        ScheduleDespawn(60);
                        break;
                    case 60:
                        GetOwner().QueueSkill(21534, 1, 3000);
                        break;
                }
            }
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, (long)delayInSec * 1000);
    }

    protected override void RemoveBossEntries()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_HARD_BOSS_PORTAL_DESTROY());
        foreach (Npc p in GetPosition().GetWorldMapInstance().GetNpcs(702216))
            if (p != null)
                p.GetController().Delete();
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21534:
                GetOwner().GetController().Delete();
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_HARD_BOSS_TIMER_04());
                break;
        }
    }
}

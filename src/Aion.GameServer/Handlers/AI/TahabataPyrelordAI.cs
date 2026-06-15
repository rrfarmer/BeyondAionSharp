using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu, Estrayl
/// </summary>
[AIName("tahabata_pyrelord")]
public class TahabataPyrelordAI : AggressiveNpcAI
{
    private ScheduledTask wipeTask;

    public TahabataPyrelordAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleWipe();
    }

    private void ScheduleWipe()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_TIME());
        wipeTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead())
                GetOwner().QueueSkill(19679, 50, 3000);
            return ValueTask.CompletedTask;
        }, 300000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 19679: // You are unworthy.
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_S_RANK_BATTLE_END());
                AIActions.DeleteOwner(this);
                break;
            case 18236:
                Spawn(281258, 1191.2714f, 1220.5795f, 144.2901f, (sbyte)36);
                Spawn(281258, 1188.3695f, 1257.1322f, 139.66028f, (sbyte)80);
                Spawn(281258, 1177.1423f, 1253.9136f, 140.58705f, (sbyte)97);
                Spawn(281258, 1163.5889f, 1231.9149f, 145.40042f, (sbyte)118);
                break;
            case 18241:
                Spawn(281259, 1182.0021f, 1244.0125f, 142.67587f, (sbyte)88);
                Spawn(281259, 1192.3885f, 1236.5231f, 142.50638f, (sbyte)68);
                Spawn(281259, 1185.647f, 1227.2747f, 144.2261f, (sbyte)32);
                Spawn(281259, 1172.3302f, 1232.5709f, 144.70761f, (sbyte)12);
                break;
        }
    }

    private void CancelTask()
    {
        if (wipeTask != null && !wipeTask.IsCancelled)
            wipeTask.Cancel(true);
    }

    protected override void HandleDied()
    {
        CancelTask();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }
}

using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/CalindiFlamelordAI (Ritsu, Estrayl).</summary>
[AIName("calindi_flamelord")]
public class DarkPoetaCalindiFlamelordAI : AggressiveNpcAI
{
    private ScheduledTask wipeTask;

    public DarkPoetaCalindiFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleWipe();
    }

    private void ScheduleWipe()
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_A_RANK_BATTLE_TIME());
        wipeTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
                GetOwner().QueueSkill(19679, 50, 3000);
            return ValueTask.CompletedTask;
        }, 600000L);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 18233:
                if (GetLifeStats().GetHpPercentage() >= 30 && GetLifeStats().GetHpPercentage() <= 60)
                {
                    Spawn(281267, 1191.2714f, 1220.5795f, 144.2901f, (sbyte)36);
                    Spawn(281267, 1188.3695f, 1257.1322f, 139.66028f, (sbyte)80);
                    Spawn(281267, 1177.1423f, 1253.9136f, 140.58705f, (sbyte)97);
                    Spawn(281267, 1163.5889f, 1231.9149f, 145.40042f, (sbyte)118);
                }
                else
                {
                    RndSpawnInRange(281268, 1, 2);
                    RndSpawnInRange(281268, 1, 2);
                }
                break;
            case 19679: // You are unworthy.
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_A_RANK_BATTLE_END());
                AIActions.DeleteOwner(this);
                break;
        }
    }

    protected override void HandleDied()
    {
        if (wipeTask != null && !wipeTask.IsCancelled)
            wipeTask.Cancel(true);
        base.HandleDied();
    }
}

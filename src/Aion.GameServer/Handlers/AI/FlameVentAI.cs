using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/idgelDome/FlameVentAI (@author Ritsu, Estrayl).</summary>
[AIName("flame_vent")]
public class FlameVentAI : ActionItemNpcAI
{
    public FlameVentAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        switch (GetNpcId())
        {
            case 802548:
                {
                    Npc repelling1 = (Npc)Spawn(855010, 234.4599f, 194.21619f, 79.589996f, (sbyte)119);
                    Npc fx1 = (Npc)Spawn(702405, 232.19067f, 185.85762f, 80.199997f, (sbyte)75);
                    Npc fx2 = (Npc)Spawn(702405, 238.54938f, 200.84813f, 80f, (sbyte)15);
                    repelling1.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { repelling1.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    fx1.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { fx1.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    fx2.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { fx2.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    GetOwner().GetController().Die();
                    break;
                }
            case 802549:
                {
                    Npc repelling2 = (Npc)Spawn(855010, 294.62436f, 324.11783f, 79.790443f, (sbyte)119);
                    Npc fx3 = (Npc)Spawn(702405, 290.67102f, 317.26324f, 80.099998f, (sbyte)75);
                    Npc fx4 = (Npc)Spawn(702405, 297.08356f, 332.35382f, 80.099998f, (sbyte)15);
                    repelling2.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { repelling2.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    fx3.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { fx3.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    fx4.GetController().AddTask(TaskId.DESPAWN,
                        ThreadPoolManager.GetInstance().Schedule(ct => { fx4.GetController().Delete(); return System.Threading.Tasks.ValueTask.CompletedTask; }, 60000L));
                    GetOwner().GetController().Die();
                    break;
                }
        }
    }
}

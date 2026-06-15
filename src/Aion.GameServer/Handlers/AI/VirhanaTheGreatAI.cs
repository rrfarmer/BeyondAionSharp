using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Luzien
/// </summary>
[AIName("virhana")]
public class VirhanaTheGreatAI : AggressiveNpcAI
{
    private bool isStart;
    private int count;

    public VirhanaTheGreatAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (!isStart)
        {
            isStart = true;
            ScheduleRage();
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        isStart = false;
    }

    private void ScheduleRage()
    {
        if (IsDead() || !isStart)
        {
            return;
        }
        AIActions.UseSkill(this, 19121);

        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            StartRage();
            return ValueTask.CompletedTask;
        }, 70000L);
    }

    private void StartRage()
    {
        if (IsDead() || !isStart)
        {
            return;
        }
        if (count < 12)
        {
            AIActions.UseSkill(this, 18897);
            count++;

            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                StartRage();
                return ValueTask.CompletedTask;
            }, 10000L);
        }
        else
        { // restart after a douzen casts
            count = 0;
            ScheduleRage();
        }
    }
}

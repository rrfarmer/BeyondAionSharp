using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

[AIName("kaluvaspawn")]
public class KaluvaSpawnAI : NpcAI
{
    private ScheduledTask task;

    public KaluvaSpawnAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (task != null && !task.IsDone())
            task.Cancel(true);
        CheckKaluva();
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ScheduleHatch();
    }

    private void CheckKaluva()
    {
        Npc kaluva = GetPosition().GetWorldMapInstance().GetNpc(216950);
        if (kaluva != null && !kaluva.IsDead())
        {
            kaluva.GetEffectController().RemoveEffect(19152);
        }
        AIActions.DeleteOwner(this);
    }

    private void ScheduleHatch()
    {
        task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                HatchAdds();
                CheckKaluva();
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(22000)); // schedule hatch when debuff ends(20s)
    }

    private void HatchAdds() // 4 different spawn-formations; See Powerwiki for more information
    {
        WorldPosition p = GetPosition();
        switch (Rnd.Get(1, 4))
        {
            case 1:
                Spawn(281911, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(281911, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
            case 2:
                for (int i = 0; i < 12; i++)
                {
                    Spawn(281912, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                }
                break;
            case 3:
                Spawn(282057, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
            case 4:
                Spawn(281911, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(281912, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(281912, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                Spawn(281912, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
                break;
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}

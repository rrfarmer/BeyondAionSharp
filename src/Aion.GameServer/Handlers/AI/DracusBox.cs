using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Spawns Chaos Dracus after the Mysterious Crate dies, and schedules Crate respawn after Dracus dies.
/// Java parity: ai/worlds/eltnen/DracusBox (Neon).
/// </summary>
[AIName("dracusbox")]
public class DracusBox : OneDmgNoActionAI
{
    private const int DRACUS_ID = 211800;

    public DracusBox(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDied()
    {
        int spawnId;
        switch (Rnd.Get(1, 3))
        {
            case 1:
                spawnId = 211792; // elroco
                break;
            case 2:
                spawnId = 211799; // oozing clodworm
                break;
            default:
                spawnId = DRACUS_ID; // chaos dracus
                break;
        }

        Npc mysteriousCrate = GetOwner();
        Npc spawn = (Npc)Spawn(spawnId, mysteriousCrate.GetX(), mysteriousCrate.GetY(), mysteriousCrate.GetZ(), (sbyte)mysteriousCrate.GetHeading());
        AIActions.DeleteOwner(this); // delete the huge box instantly so we can see the spawned mob
        if (spawn.GetNpcId() == DRACUS_ID)
        {
            spawn.GetObserveController().Attach(new DeathObserver(_ => AIActions.ScheduleRespawn(this)));
        }
        else
        {
            AIActions.ScheduleRespawn(this);
        }
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.ALLOW_RESPAWN:
                return false;
            default:
                return base.Ask(question);
        }
    }
}

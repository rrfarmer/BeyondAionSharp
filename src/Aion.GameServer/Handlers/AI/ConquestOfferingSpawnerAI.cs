using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// this ai handles (re-)spawns of random npc with the title "conquest offering"
/// Java parity: ai/ConquestOfferingSpawnerAI (Yeats).
/// </summary>
[AIName("conquest_offering_spawner")]
public class ConquestOfferingSpawnerAI : NpcAI
{
    private ScheduledTask respawnTask;

    public ConquestOfferingSpawnerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SpawnRandomNpc();
    }

    private void SpawnRandomNpc()
    {
        switch (GetNpcId())
        {
            // Inggison
            case 856150:
            case 856156:
                Spawn(236307, 236335);
                break;
            case 856151:
            case 856157:
                Spawn(236311, 236339);
                break;
            case 856152:
            case 856158:
                Spawn(236315, 236343);
                break;
            case 856153:
            case 856159:
                Spawn(236319, 236347);
                break;
            case 856154:
            case 856160:
                Spawn(236323, 236351);
                break;
            case 856155:
            case 856161:
                Spawn(236327, 236355);
                break;

            // Gelkmaros
            case 856162:
            case 856168:
                Spawn(236363, 236391);
                break;
            case 856163:
            case 856169:
                Spawn(236367, 236395);
                break;
            case 856164:
            case 856170:
                Spawn(236371, 236399);
                break;
            case 856165:
            case 856171:
                Spawn(236375, 236403);
                break;
            case 856166:
            case 856172:
                Spawn(236379, 236407);
                break;
            case 856167:
            case 856173:
                Spawn(236383, 236411);
                break;
        }
    }

    // Ncsoft calls them "normal" and "party"
    private void Spawn(int startNormal, int startParty)
    {
        int npcId;
        // calculate what kind of npc will be spawned 'normal'(70%) and 'party' (30%)
        if (Rnd.Chance() < 70)
        {
            // theres another kind of spawn called 'all'(30%)
            if (Rnd.Chance() < 30)
            {
                npcId = GetRndNpc(startNormal);
            }
            else
            {
                npcId = startNormal + Rnd.Get(0, 3);
            }
        }
        else
        {
            // theres another kind of spawn called 'all'(30%)
            if (Rnd.Chance() < 30)
            {
                npcId = GetRndNpc(startNormal);
            }
            else
            {
                npcId = startParty + Rnd.Get(0, 3);
            }
        }

        if (npcId != 0)
            Spawn(npcId, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)GetOwner().GetHeading());
    }

    private int GetRndNpc(int curId)
    {
        if (curId <= 236327)
        { // normal
            return 236331 + Rnd.Get(0, 3);
        }
        else if (curId <= 236355)
        { // party
            return 236359 + Rnd.Get(0, 3);
        }
        else if (curId <= 236383)
        { // normal
            return 236387 + Rnd.Get(0, 3);
        }
        else if (curId <= 236411)
        { // party
            return 236415 + Rnd.Get(0, 3);
        }

        return 0;
    }

    protected override void HandleCustomEvent(int eventId, params object[] args)
    {
        if (eventId == 1)
        { // spawned npc died, schedule respawn
            if (respawnTask != null && !respawnTask.IsCancelled && !respawnTask.IsDone())
                return;
            long respawnDelay = 600000 + (Rnd.Get(0, 2) * 300000L); // random 10, 15 or 20 minutes
            respawnTask = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SpawnRandomNpc();
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(respawnDelay));
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        CancelTask();
        base.HandleDespawned();
    }

    private void CancelTask()
    {
        if (respawnTask != null && !respawnTask.IsCancelled)
            respawnTask.Cancel(false);
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        return 0;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return 0;
    }
}

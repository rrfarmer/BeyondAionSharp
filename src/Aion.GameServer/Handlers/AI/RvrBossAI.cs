using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Bobobear
/// </summary>
[AIName("rvr_boss")]
public class RvrBossAI : AggressiveNpcAI
{
    public RvrBossAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        // add player to event list for additional reward
        if (creature is Player && GetPosition().GetMapId() == 600010000)
        {
            SiegeService.GetInstance().CheckRvrEventPlayer((Player)creature);
        }
        // TODO Spawn defensive guards (only for bosses in silentera Canyon)
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DespawnEnemyBoss();
        ScheduleRespawn();
        SendParticipationRewards();
    }

    // despawn enemy boss (only for silentera)
    private void DespawnEnemyBoss()
    {
        if (GetPosition().GetMapId() == 600010000)
        {
            WorldMapInstance instance = GetPosition().GetWorldMapInstance();
            DeleteNpcs(instance.GetNpcs(GetNpcId() == 220948 ? 220949 : 220948));
        }
    }

    // schedule respawn of both silentera bosses (bosses in other maps must not respawn)
    private void ScheduleRespawn()
    {
        if (GetPosition().GetMapId() == 600010000)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                Spawn(220948, 658.7087f, 795.21857f, 293.14087f, (sbyte)7);
                Spawn(220949, 657.95105f, 737.5624f, 293.19818f, (sbyte)0);
                return ValueTask.CompletedTask;
            }, 43200000L);
        }
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
                npc.GetController().Delete();
        }
    }

    private void SendParticipationRewards()
    {
        if (GetPosition().GetMapId() == 600010000)
        {
            int hour = ServerTime.Now().Hour;
            if (hour >= 19 && hour <= 23)
            {
                foreach (Player rewardedPlayer in SiegeService.GetInstance().GetRvrEventPlayers())
                {
                    SystemMailService.SendMail("EventService", rewardedPlayer.GetName(), "EventReward", "Medal", 186000147, 1, 0, LetterType.NORMAL);
                }
            }
            SiegeService.GetInstance().ClearRvrEventPlayers();
        }
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
    }
}

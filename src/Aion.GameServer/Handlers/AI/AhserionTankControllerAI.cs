using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Panesterra.Ahserion;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/panesterra/ahserionsflight/AhserionTankControllerAI (@author Yeats, Estrayl).</summary>
[AIName("ahserion_tank_controller")]
public class AhserionTankControllerAI : AhserionConstructAI
{
    private readonly AtomicBoolean canShout = new AtomicBoolean(true);

    public AhserionTankControllerAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        BroadcastAttack();
    }

    private void BroadcastAttack()
    {
        if (canShout.CompareAndSet(true, false))
        {
            if (!GetOwner().IsDead() && GetOwner().GetWorldId() == 400030000)
            {
                switch (GetOwner().GetSpawn().GetStaticId())
                {
                    case 180:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_C_ATTACKED());
                        break;
                    case 181:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_B_ATTACKED());
                        break;
                    case 182:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_D_ATTACKED());
                        break;
                    case 183:
                        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_A_ATTACKED());
                        break;
                }
                ThreadPoolManager.GetInstance().Schedule(_ => { AllowShout(); return ValueTask.CompletedTask; }, 3000L);
            }
        }
    }

    private void AllowShout()
    {
        if (!IsDead())
            canShout.Set(true);
    }

    private void DeleteRelatedNpcs()
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (PositionUtil.IsInRange(GetOwner(), npc, 40))
            {
                // Tanks, Flag, Attacker
                if (npc.GetRace() == Race.CONSTRUCT || npc.GetNpcTemplateType() == NpcTemplateType.FLAG || npc.GetNpcId() == 297185)
                    npc.GetController().DeleteIfAliveOrCancelRespawn();
            }
        });
    }

    protected override void HandleDespawned()
    {
        DeleteRelatedNpcs();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        DeleteRelatedNpcs();
        switch (GetOwner().GetSpawn().GetStaticId())
        {
            case 180:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_C_BROKEN());
                break;
            case 181:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_B_BROKEN());
                break;
            case 182:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_D_BROKEN());
                break;
            case 183:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_GAB1_SUB_TANK_A_BROKEN());
                break;
        }
        Dictionary<PanesterraFaction, int> panesterraDamage = new Dictionary<PanesterraFaction, int>();

        // Only players or balaur can attack the controller, there are no other npcs on this map
        foreach (DamageInfo<Creature> damageInfo in GetAggroList().GetFinalDamageList().GetCreatureDamages())
        {
            PanesterraFaction? faction = null;
            if (damageInfo.GetAttacker() is Player player)
            {
                PanesterraTeam team = PanesterraService.GetInstance().GetTeam(player);
                if (team != null && !team.IsEliminated())
                    faction = team.GetFaction();
            }
            else
                faction = PanesterraFaction.BALAUR;

            if (faction.HasValue)
                panesterraDamage[faction.Value] = (panesterraDamage.TryGetValue(faction.Value, out int v) ? v : 0) + damageInfo.GetDamage();
        }
        int staticId = GetSpawnTemplate().GetStaticId();

        ThreadPoolManager.GetInstance().Schedule(_ => { SpawnNewTankBase(staticId, FindWinnerTeam(panesterraDamage)); return ValueTask.CompletedTask; }, 3000L);
        base.HandleDied();
    }

    private PanesterraFaction FindWinnerTeam(Dictionary<PanesterraFaction, int> panesterraDamage)
    {
        // just in case: we'll spawn balaur construct again
        PanesterraFaction winner = PanesterraFaction.BALAUR;
        int maxDmg = panesterraDamage.TryGetValue(PanesterraFaction.BALAUR, out int balaurDmg) ? balaurDmg : 0;
        foreach (PanesterraFaction faction in Enum.GetValues<PanesterraFaction>())
        {
            int? dmg = panesterraDamage.TryGetValue(faction, out int d) ? d : (int?)null;
            PanesterraTeam team = PanesterraService.GetInstance().GetTeam(faction);
            if (dmg != null && team != null && !team.IsEliminated())
            {
                if (dmg > maxDmg)
                {
                    maxDmg = dmg.Value;
                    winner = faction;
                }
            }
        }
        return winner;
    }

    private void SpawnNewTankBase(int staticId, PanesterraFaction faction)
    {
        if (AhserionRaid.GetInstance().IsStarted())
            AhserionRaid.GetInstance().SpawnStage(staticId, faction);
    }
}

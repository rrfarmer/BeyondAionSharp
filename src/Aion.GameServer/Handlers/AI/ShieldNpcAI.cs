using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/ShieldNpcAI (@author Source).</summary>
[AIName("siege_shieldnpc")]
public class ShieldNpcAI : SiegeNpcAI
{
    public ShieldNpcAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        // prevent field stone from resetting
        return GetOwner().GetRace() != Race.CONSTRUCT;
    }

    protected override void HandleDespawned()
    {
        UpdateFortressShieldStatus(false);
        base.HandleDespawned();
    }

    protected override void HandleSpawned()
    {
        UpdateFortressShieldStatus(true);
        base.HandleSpawned();
    }

    public override bool Ask(AIQuestion question)
    {
        switch (question)
        {
            case AIQuestion.REWARD_AP_XP_DP_LOOT:
            case AIQuestion.REWARD_AP:
                return true;
            case AIQuestion.REWARD_LOOT:
            case AIQuestion.ALLOW_RESPAWN:
                return false;
            default:
                return base.Ask(question);
        }
    }

    private void UpdateFortressShieldStatus(bool hasShield)
    {
        int siegeLocationId = GetSpawnTemplate().GetSiegeId();
        SiegeService.GetInstance().GetFortress(siegeLocationId).SetUnderShield(hasShield);
        PacketSendUtility.BroadcastToMap(GetPosition().GetWorldMapInstance(), new SM_SHIELD_EFFECT(siegeLocationId));
    }
}

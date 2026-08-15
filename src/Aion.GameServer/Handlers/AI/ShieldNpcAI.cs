using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The fortress field generator. Java parity: ai/siege/ShieldNpcAI (@author Source), plus one
/// mechanic from retail pattern <c>LGuard_Shield</c>.
/// </summary>
/// <remarks>
/// Retail-sourced addition; see docs/retail-ai-fidelity.md. Once the generator falls below 35% it
/// drops an ice sheet (295074) on whoever is attacking it, one time, lasting ten minutes. That NPC was
/// spawned by nothing in our server.
/// <para>
/// The rest of the pattern is deliberately not translated. This is siege infrastructure rather than an
/// encounter — the class exists to raise and drop the fortress shield — and the pattern's nine skill
/// indices have nothing to corroborate them against our nine skills. Restructuring a siege class on an
/// unresolvable rotation is not a trade worth making; adding the one mechanic that needs no index is.
/// </para>
/// </remarks>
[AIName("siege_shieldnpc")]
public class ShieldNpcAI : SiegeNpcAI
{
    private const int IceSheet = 295074;

    /// <summary>Retail arms this at 20s and re-checks every 15s, so the sheet is not instantaneous.</summary>
    private const long FirstCheckMillis = 20000L;
    private const long RecheckMillis = 15000L;

    private const int IceSheetThreshold = 35;
    private const int IceSheetLifeMillis = 600000;

    private readonly AtomicBoolean checking = new AtomicBoolean(false);
    private readonly AtomicBoolean sheetDropped = new AtomicBoolean(false);
    private ScheduledTask? checkTask;

    public ShieldNpcAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (checking.CompareAndSet(false, true))
            StartIceSheetCheck();
    }

    /// <summary>Watches for the one moment the generator passes 35%.</summary>
    private void StartIceSheetCheck()
    {
        checkTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            // Retail's timer keeps re-arming for the whole fight; it is the flag var that stops a
            // second sheet, not the timer stopping. Cancelling here instead would make the flag
            // decorative and hide a regression that removed it.
            if (IsDead())
                CancelIceSheetCheck();
            else if (GetLifeStats().GetHpPercentage() < IceSheetThreshold
                && sheetDropped.CompareAndSet(false, true))
                DropIceSheet();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(FirstCheckMillis), System.TimeSpan.FromMilliseconds(RecheckMillis));
    }

    private void DropIceSheet()
    {
        if (GetOwner().GetTarget() is not Creature target)
            return;

        WorldPosition at = target.GetPosition();
        double angle = Rnd.NextFloat(360f) * System.Math.PI / 180.0;
        float distance = Rnd.NextFloat(2f);
        float x = at.GetX() + (float)(System.Math.Cos(angle) * distance);
        float y = at.GetY() + (float)(System.Math.Sin(angle) * distance);

        if (Spawn(IceSheet, x, y, at.GetZ(), (sbyte)at.GetHeading()) is not Npc sheet)
            return;

        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            sheet.GetController().DeleteIfAliveOrCancelRespawn();
            return ValueTask.CompletedTask;
        }, IceSheetLifeMillis);
    }

    private void CancelIceSheetCheck()
    {
        if (checkTask != null && !checkTask.IsDone())
            checkTask.Cancel(true);
        checkTask = null;
    }

    protected override void HandleBackHome()
    {
        CancelIceSheetCheck();
        checking.Set(false);
        sheetDropped.Set(false);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        CancelIceSheetCheck();
        base.HandleDied();
    }

    public override bool CanThink()
    {
        // prevent field stone from resetting
        return GetOwner().GetRace() != Race.CONSTRUCT;
    }

    protected override void HandleDespawned()
    {
        CancelIceSheetCheck();
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

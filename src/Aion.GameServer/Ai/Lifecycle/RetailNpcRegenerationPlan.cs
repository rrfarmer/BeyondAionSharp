namespace Aion.GameServer.Ai.Lifecycle;

/// <summary>
/// Inactive, single-observation arithmetic from the pinned NPC/Main binaries.
/// This neither obtains effective stats nor schedules, mutates HP or sends packets.
/// Normal-mode eligibility must be the actual native virtual/state-stack results;
/// a C# AIState or a missing owner does not establish those inputs.
/// </summary>
internal static class RetailNpcRegenerationPlan
{
    internal const string NpcBinarySha256 =
        "c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350";
    internal const string MainBinarySha256 =
        "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";

    // NPC::TimerExpired 0x140126470: unsigned subtraction, strict >5999.
    internal static bool HasHeartbeatOpportunity(uint observedTick, uint lastHeartbeatTick) =>
        unchecked(observedTick - lastHeartbeatTick) > 5999u;

    // Consumes an original inclusive [0,200] draw; it does not generate or skip RNG.
    internal static int TimerZeroRearmDelay(bool nativeSleepFlag, int originalJitterDraw)
    {
        if (originalJitterDraw is < 0 or > 200)
            throw new ArgumentOutOfRangeException(nameof(originalJitterDraw));
        return (nativeSleepFlag ? 6000 : 2000) + originalJitterDraw;
    }

    /// <summary>
    /// NPC::OnHeartBeat 0x140126bc0 through NPC::IncHp 0x140102580.
    /// The tentative capped value is only a send guard. The request carries the
    /// original delta and observed HP; NPC HP is unchanged until Main's echo.
    /// Native reloads observed HP after ReleaseAttackerMap; this coherent snapshot
    /// cannot authorize using stale pre-release HP in a future runtime adapter.
    /// </summary>
    internal static RetailNpcHpRequest? PlanNormalHpRequest(
        bool nativeVirtual22Eligible, bool nativeAttackStatePresent, uint nativeFlags,
        int nativeOrderWord, int currentNpcHp, int maximumNpcHp, int effectiveNpcHpRegen)
    {
        RequireHp(currentNpcHp, maximumNpcHp);
        if (!nativeVirtual22Eligible || nativeAttackStatePresent || (nativeFlags & 0x80u) != 0 ||
            currentNpcHp >= maximumNpcHp)
            return null;

        int delta = nativeOrderWord == 2 ? unchecked(effectiveNpcHpRegen * 2) : effectiveNpcHpRegen;
        if (delta <= 0 || currentNpcHp <= 0)
            return null;

        int tentative = unchecked(currentNpcHp + delta);
        bool overMaximum = tentative > maximumNpcHp;
        if (overMaximum)
            tentative = maximumNpcHp;
        return tentative == currentNpcHp ? null :
            new RetailNpcHpRequest(delta, currentNpcHp, 3, overMaximum);
    }

    /// <summary>
    /// Main 0x1400bc2b0: prediction for one coherent observation, NOT an atomic
    /// commit implementation. Native performs separate reads and XADD/XCHG;
    /// concurrent-writer ordering remains an integration/capture obligation.
    /// The request's old NPC HP is not used to overwrite current Main HP.
    /// </summary>
    internal static RetailNpcMainHpPrediction PredictMainHp(
        int nativeLifeStateWord, int currentMainHp, int maximumMainHp, int originalDelta)
    {
        RequireHp(currentMainHp, maximumMainHp);
        if (nativeLifeStateWord != 0)
            return new(false, currentMainHp, 0);

        long sum = (long)currentMainHp + originalDelta;
        int next = sum >= maximumMainHp ? maximumMainHp : sum < 0 ? 0 : (int)sum;
        // The native active branch sends HP and HPChange even for a zero delta.
        return new(true, next, originalDelta);
    }

    /// <summary>
    /// NS_CHANGE_ATTR kind0x11 at 0x1401f6360: XCHG installs Main HP, then a
    /// previously nonpositive NPC HP causes a second XCHG to zero. This is a
    /// single-observation prediction; it does not emulate the intervening race.
    /// </summary>
    internal static int PredictNpcHpEcho(int previousNpcHp, int mainHpPayload)
    {
        if (mainHpPayload < 0)
            throw new ArgumentOutOfRangeException(nameof(mainHpPayload), "Negative Main HP is outside the admitted domain.");
        return previousNpcHp < 1 ? 0 : mainHpPayload;
    }

    private static void RequireHp(int current, int maximum)
    {
        if (current < 0)
            throw new ArgumentOutOfRangeException(nameof(current));
        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
    }
}

internal readonly record struct RetailNpcHpRequest(
    int OriginalDelta, int ObservedNpcHp, byte NativeReason, bool ReleaseAttackersBeforeSend);

internal readonly record struct RetailNpcMainHpPrediction(
    bool SendsHpNotifications, int PredictedHp, int OriginalNotificationDelta);

using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Chief Gunner Koakoa (215070), Steel Rake. Retail pattern <c>IDSlk_Gunner</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/rakes/ChiefGunnerKoakoaAI (@author xTz). Retail-sourced additions below; see
/// docs/retail-ai-fidelity.md.
/// <para>
/// <b>He did not walk at all here, and retail has him pacing the gun deck.</b> Unlike the three
/// encounters before him his route was <i>not</i> already in our data -- it had to come from the client.
/// <c>IDShip_Mobpath_ShulackRaAtilleryChKnmd_45_Ah</c> in <c>Map/Worlds/idshulackship</c> carries his own
/// devname and its first point is 0.35m from his spawn: seven points out along the deck and back. It is
/// now <c>route_id="3001000001"</c> in the Steel Rake walker file and bound to his spot.
/// </para>
/// <para>
/// <b>At point 3 -- the far end of the walk -- he fires a six-rung cascade</b>: 17% A, 33% B, 50% C,
/// 67% D, 83% E, and an unguarded last rung that is E again. One npc, at his own feet, with
/// <c>live_time=6</c>. <b>Six seconds is the tell</b>: these are the muzzle effects of the guns he is
/// checking, not adds, which is why five of them can be spawned in a row without filling the deck.
/// </para>
/// <para>
/// <b>Not translated.</b> The <c>use_skill</c> beside each rung (skill-index blocked) and the
/// <c>BTIMERI_INDEX_1</c> re-arm at 20000 that each one sets, which drives a handler this class does not
/// model. The HP-percent spawning below is aionemu's own -- retail's spawn actions never name 281212 or
/// 281213 -- and is left alone for the same reason Grogget's is: it fires in combat, this fires on
/// patrol, and removing a fight's adds is a separate decision.
/// </para>
/// </remarks>
[AIName("gunnerkoakoa")]
public class ChiefGunnerKoakoaAI : SummonerAI
{
    public ChiefGunnerKoakoaAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>Retail's <c>is_waypoint_index</c> for the cascade: the far end of the deck.</summary>
    public const int GunWaypoint = 3;

    /// <summary>Retail's <c>live_time</c> on every rung. Six seconds, so these are effects, not adds.</summary>
    public const int MuzzleLifeSeconds = 6;

    // BIDShulack_GunnerSum{A..E}_45_n, resolved through ai_binding.tsv.
    private const int MuzzleA = 281220;
    private const int MuzzleB = 281221;
    private const int MuzzleC = 281222;
    private const int MuzzleD = 281223;
    private const int MuzzleE = 281296;

    /// <summary>
    /// Retail's six rungs at waypoint 3, in priority order. The last carries no <c>test_probability</c> at
    /// all, and spawns E just as the rung above it does.
    /// </summary>
    protected override void HandleMoveArrived()
    {
        RouteStep arrived = GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (arrived == null || arrived.GetStepIndex() != GunWaypoint)
            return;

        int muzzle;
        if (Rnd.Chance() < 17) muzzle = MuzzleA;
        else if (Rnd.Chance() < 33) muzzle = MuzzleB;
        else if (Rnd.Chance() < 50) muzzle = MuzzleC;
        else if (Rnd.Chance() < 67) muzzle = MuzzleD;
        else muzzle = MuzzleE;

        SpawnFor(muzzle, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading(), MuzzleLifeSeconds);
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        if (GetEffectController().HasAbnormalEffect(18552))
        {
            CheckAbnormalEffect();
        }
        RandomSpawn(Rnd.Get(1, 3));
    }

    private void CheckAbnormalEffect()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            GetEffectController().RemoveEffect(18552);
            // to do remove pause
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 21000L);
    }

    private void RandomSpawn(int i)
    {
        // to do pause boss
        Spawn(281212, 757.39746f, 508.70383f, 1012.30084f, (sbyte)0);
        switch (i)
        {
            case 1:
                Spawn(281212, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281212, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281212, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281212, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281212, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281212, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281212, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281213, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281213, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281213, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281213, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281213, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281213, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281213, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
            case 2:
                Spawn(281213, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281213, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281212, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281213, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281213, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281212, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281212, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281212, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281213, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281212, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281212, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281212, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281213, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281213, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
            case 3:
                Spawn(281212, 726.1167f, 503.28836f, 1012.6846f, (sbyte)0);
                Spawn(281212, 736.4446f, 505.3141f, 1012.1576f, (sbyte)0);
                Spawn(281213, 746.9261f, 503.50122f, 1012.68335f, (sbyte)0);
                Spawn(281212, 728.9705f, 492.59402f, 1012.68335f, (sbyte)0);
                Spawn(281212, 739.9526f, 491.54123f, 1011.692f, (sbyte)0);
                Spawn(281213, 749.754f, 491.74677f, 1011.8663f, (sbyte)0);
                Spawn(281213, 756.9996f, 500.01736f, 1011.692f, (sbyte)0);
                Spawn(281213, 736.9722f, 514.6446f, 1011.8599f, (sbyte)0);
                Spawn(281212, 747.5162f, 514.51715f, 1011.692f, (sbyte)0);
                Spawn(281213, 726.8303f, 514.5155f, 1012.6845f, (sbyte)0);
                Spawn(281213, 727.9019f, 524.578f, 1012.68365f, (sbyte)0);
                Spawn(281213, 738.52844f, 525.0482f, 1011.692f, (sbyte)0);
                Spawn(281212, 758.3127f, 520.59143f, 1011.692f, (sbyte)0);
                Spawn(281212, 748.7474f, 525.84f, 1011.859f, (sbyte)0);
                break;
        }
    }
}

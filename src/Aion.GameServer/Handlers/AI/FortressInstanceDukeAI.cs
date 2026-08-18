using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/abyss/FortressInstanceDukeAI (@author Estrayl, since AION 4.8).</summary>
[AIName("fortress_instance_duke")]
public class FortressInstanceDukeAI : AggressiveNpcAI
{
    public FortressInstanceDukeAI(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail <c>ABRwd_DrGuardianChiefGate_65_Ae</c> stands for ten minutes.
    /// </summary>
    /// <remarks>
    /// <b>This gate is spawned once per cast of 18003 and had no bound at all.</b> The class deletes
    /// 284978-284981 when the duke dies or despawns, which cleans up after the fight but does nothing
    /// during it, so a long fight accumulated one gate per cast. <b>Death cleanup is not a lifetime</b> --
    /// the same distinction Yamennes' golems needed.
    /// </remarks>
    private const int GateLife = 600;

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 18003)
            SpawnFor(284978, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                (sbyte)GetOwner().GetHeading(), GateLife);
    }

    /// <summary>
    /// Retail <c>BGuard_ChiefD_Tune405</c> <c>on_die</c>: seven drakan-departure npcs at four points,
    /// two of them leaving by teleporter and one group by barrier.
    /// </summary>
    /// <remarks>
    /// <b>Retail-sourced; see docs/retail-ai-fidelity.md.</b> This is the same wave
    /// <see cref="AwakenedChamberLordAI"/> already runs from its own pattern — the three barracks share
    /// the three chambers' layout, and this duke stands at (526, 845), inside the pattern's box. Java has
    /// no equivalent, so this is a sanctioned divergence rather than a parity fix.
    /// <para>
    /// Retail writes the branch twice, flagged and unflagged, with <b>identical</b> actions, so only one
    /// is needed here — unlike the Ophidan pair, where the flagged half carried an extra step.
    /// </para>
    /// </remarks>
    private const int DrakanByTeleporter = 296339;
    private const int DrakanByBarrier = 296338;

    private const int TeleportedLife = 18;
    private const int BarrierLife = 12;

    private static readonly (float X, float Y)[] TeleportPoints =
        [(496f, 847f), (529f, 874f), (554f, 850f)];

    private static readonly (float X, float Y) BarrierPoint = (580f, 840f);

    /// <summary>Placed at the duke's own height, as the chamber lord's wave is.</summary>
    private void DeathWave()
    {
        float z = GetOwner().GetZ();
        sbyte heading = (sbyte)GetOwner().GetHeading();

        foreach ((float x, float y) in TeleportPoints)
        {
            SpawnFor(DrakanByTeleporter, x, y, z, heading, TeleportedLife);
            SpawnFor(DrakanByTeleporter, x, y, z, heading, TeleportedLife);
        }

        for (int i = 0; i < 3; i++)
            SpawnFor(DrakanByBarrier, BarrierPoint.X, BarrierPoint.Y, z, heading, BarrierLife);
    }

    private void DeleteSummons()
    {
        GetPosition().GetWorldMapInstance().ForEachNpc(npc =>
        {
            if (npc.GetNpcId() >= 284978 && npc.GetNpcId() <= 284981)
                npc.GetController().Delete();
        });
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        DeleteSummons();
        DeathWave();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        DeleteSummons();
    }
}

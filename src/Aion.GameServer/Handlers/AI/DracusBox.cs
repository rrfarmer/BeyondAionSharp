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

    /// <summary>
    /// Retail's <c>live_time</c> on this spawn. <b>An hour is not a mechanic</b> - it is retail bounding
    /// an npc that would otherwise outlive the reason it was summoned. Ported for the same reason: the
    /// bound is cheap, and its absence is only visible on a server that has been up a long time.
    /// </summary>
    private const int SummonLife = 9600;

    /// <summary>Retail's <c>test_probability</c> on each rung, richest first.</summary>
    public const int DracusPercent = 1;
    public const int ClodwormPercent = 20;
    public const int MosbearPercent = 20;
    public const int RatmanPercent = 20;
    public const int AmurruPercent = 9;

    /// <summary>Retail's <c>num_to_spawn</c> on the clodworm rung. Not one.</summary>
    public const int ClodwormCount = 6;

    // Resolved from the pattern's npc_nameid values through ai_binding.tsv; see audit_summon_ids.py.
    private const int Clodworm = 211799;       // LF2ChestClodwormNm_30_An
    private const int MosbearBaby = 211797;    // LF2ChestMosbearBabyNm_35_Ae  "cursed muku"
    private const int MosbearMom = 211796;     // LF2ChestMosbearMomNm_34_Ae   "cursed miku"
    private const int MosbearPapa = 211795;    // LF2ChestMosbearPapaNm_34_Ae  "cursed camu"
    private const int RatmanWife = 211794;     // LF2ChestRatmanChWife_34_Ae   "mumu zoo"
    private const int Ratman = 211793;         // LF2ChestRatmanCh_35_Ae       "mumu mon"
    private const int Amurru = 211798;         // LF2ChestLycanFiNm_35_Ae
    private const int Elroco = 211792;         // LF2ChestMinxNm_1_n -- the unconditional last rung

    /// <summary>
    /// Retail's <c>ND2_CheatboxSu</c>: six <c>on_killed_by_user</c> rungs, tried in priority order, each
    /// one an independent <c>test_probability</c> that ends the chain when it passes. The lowest rung
    /// carries no condition at all, so something always comes out of the crate.
    /// </summary>
    /// <remarks>
    /// This replaces a uniform roll over three npcs, which gave <b>chaos dracus a one-in-three chance
    /// where retail gives it one in a hundred</b>, spawned a single clodworm where retail spawns six, and
    /// had no mosbear family, no mumu pair and no amurru at all.
    /// </remarks>
    protected override void HandleDied()
    {
        Npc crate = GetOwner();
        float x = crate.GetX(), y = crate.GetY(), z = crate.GetZ();
        sbyte heading = (sbyte)crate.GetHeading();

        Npc dracus = null;
        if (Rnd.Chance() < DracusPercent)
        {
            dracus = Spawn(DRACUS_ID, x, y, z, heading);
        }
        else if (Rnd.Chance() < ClodwormPercent)
        {
            for (int i = 0; i < ClodwormCount; i++)
                Spawn(Clodworm, x, y, z, heading);
        }
        else if (Rnd.Chance() < MosbearPercent)
        {
            Spawn(MosbearBaby, x, y, z, heading);
            Spawn(MosbearMom, x, y, z, heading);
            Spawn(MosbearPapa, x, y, z, heading);
        }
        else if (Rnd.Chance() < RatmanPercent)
        {
            Spawn(RatmanWife, x, y, z, heading);
            Spawn(Ratman, x, y, z, heading);
        }
        else if (Rnd.Chance() < AmurruPercent)
        {
            Spawn(Amurru, x, y, z, heading);
        }
        else
        {
            Spawn(Elroco, x, y, z, heading);
        }

        AIActions.DeleteOwner(this); // delete the huge box instantly so we can see the spawned mob
        if (dracus != null)
        {
            dracus.GetObserveController().Attach(new DeathObserver(_ => AIActions.ScheduleRespawn(this)));
        }
        else
        {
            AIActions.ScheduleRespawn(this);
        }
    }

    private Npc Spawn(int npcId, float x, float y, float z, sbyte heading) =>
        (Npc)SpawnFor(npcId, x, y, z, heading, SummonLife);

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

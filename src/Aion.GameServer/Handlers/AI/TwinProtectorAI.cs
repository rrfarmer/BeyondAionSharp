using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four twin protectors of the Seal of Destruction (@author Estrayl), with the hellfire field
/// taken from retail patterns <c>IDSeal_Twin_P</c>, <c>_P_Failed</c>, <c>IDSeal_Twin_M</c> and
/// <c>_M_Failed</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by
/// <c>tools/client-extract/audit_shared_ai_names.py</c>: all four protectors share this class, the
/// two sides' patterns name different NPCs, and one of them was reachable by nobody.
/// <para>
/// <b>Neither side's hellfire field was ever placed.</b> Every one of the four patterns opens by
/// putting one on a fixed mark — the lava side's <b>cinderhorn ravager</b> (855626) at 530.5/212 and
/// the heatvent side's <b>cinderspeak immolator</b> (855712) at 531.4/151 — and clearing it when the
/// protector falls. Both are HERO-rated NPCs rather than scenery, and this class had the "Raging
/// Hellfire" cast that names the mechanic without the thing the mechanic is about.
/// </para>
/// <para>
/// The side is chosen by the same parity the adds already use: the two heatvent protectors are even
/// (236226, 236228) and the two lava ones odd (236225, 236227). Reading the four patterns confirms
/// the split rather than inferring it.
/// </para>
/// <para>
/// <b>The hellfire wave was the other side's.</b> Every <c>spawn_on_multi_target</c> branch in the
/// heatvent patterns calls <c>BIDSeal_Twin_M_Sum_Tornado</c> (855625) and every one in the lava
/// patterns calls <c>BIDSeal_Twin_P_Sum_65_Ae</c> (855621) — this class had the tornado hardcoded for
/// all four protectors, so the two lava protectors summoned a heatvent NPC. The phase ladder's own
/// wave was already split by the same parity; only the hellfire one was not, which is how a
/// side-specific id hides in a fight where most of the summons are already side-specific.
/// </para>
/// <para>
/// <b>And the waves arrive fighting.</b> Retail carries <c>hatepoints_to_add=1000</c> on every one of
/// those branches, both sides. Without it the adds stand where they were put until a player walks
/// into them.
/// </para>
/// <para>
/// <b>Correction.</b> This comment used to say <c>BIDSeal_Twin_P_Sum_Crater</c> (855623) was "not
/// spawned by anything". It is: the magma glutten (855621) spawns it on its own spot when it answers
/// <c>22710</c>, and the crater in turn spawns <c>_Crater_Skill</c> (855624) and erupts three times on
/// a six-second beat. Neither step is built here — see docs/retail-ai-fidelity.md, "The crater the
/// twins log said nobody spawned", for the chain and for what blocks it.
/// </para>
/// <para>
/// <b>Not translated:</b> the <c>Source</c> NPC each pattern leaves behind on dying, its condition
/// spawn variables, and the four <c>broadcast_message</c> numbers around them — instance sequencing
/// with nothing on our side listening. The HP ladder, the adds and the Raging Hellfire cast are ours
/// and are left exactly as they were.
/// </para>
/// </remarks>
[AIName("twin_protector")]
public class TwinProtectorAI : AggressiveNoLootNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(65, 40, 25, 15, 10);
    private readonly List<Npc> adds = new List<Npc>();

    /// <summary>The two fields and the marks retail puts them on, one per side.</summary>
    private const int LavaField = 855626;
    private const int HeatventField = 855712;

    /// <summary>
    /// What each side's <c>spawn_on_multi_target</c> branches call up: the heatvent side's
    /// <b>tornado</b> (<c>BIDSeal_Twin_M_Sum_Tornado</c>) and the lava side's own wave
    /// (<c>BIDSeal_Twin_P_Sum_65_Ae</c>).
    /// </summary>
    private const int HeatventWave = 855625;
    private const int LavaWave = 855621;

    /// <summary>
    /// Retail's <c>hatepoints_to_add</c> on every one of those branches, both sides.
    /// </summary>
    private const int OnArrival = 1000;

    private static readonly (float X, float Y, float Z) LavaMark = (530.5f, 212f, 1682f);
    private static readonly (float X, float Y, float Z) HeatventMark = (531.4f, 151f, 1682f);

    private Npc? field;

    /// <summary>Which side this protector is on. Even ids are the heatvent side.</summary>
    private bool IsHeatvent => GetNpcId() % 2 == 0;

    /// <summary>Which field this protector opens with. Even ids are the heatvent side.</summary>
    internal static int FieldFor(int protectorId) => protectorId % 2 == 0 ? HeatventField : LavaField;

    /// <summary>Which wave its hellfire calls up, by the same parity.</summary>
    internal static int WaveFor(int protectorId) => protectorId % 2 == 0 ? HeatventWave : LavaWave;

    public TwinProtectorAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        // Retail's on_wake_up, on all four patterns: the field goes on its own fixed mark.
        (float x, float y, float z) = GetNpcId() % 2 == 0 ? HeatventMark : LavaMark;
        if (Spawn(FieldFor(GetNpcId()), x, y, z, (sbyte)0) is Npc placed)
            field = placed;
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 65:
            case 40:
            case 15:
                GetOwner().ClearQueuedSkills();
                GetOwner().QueueSkill(21644, 1, 10000); // Raging Hellfire
                break;
            case 25:
            case 10:
                SpawnAdds(IsHeatvent ? 855622 : 855621, 20);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21644) // Raging Hellfire
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 21645, 1, GetTarget()).UseNoAnimationSkill();
            SpawnAdds(WaveFor(GetNpcId()), 50);
        }
    }

    /// <summary>
    /// One wave, on up to <c>count</c> of the players in reach.
    /// </summary>
    /// <remarks>
    /// Retail's <c>spawn_on_multi_target</c> carries <c>hatepoints_to_add=1000</c> on every branch of
    /// both patterns, so a wave arrives <em>already fighting</em> whoever it landed on. Without it the
    /// adds stand where they were put until someone walks into them, which is a materially easier
    /// fight — the same difference recorded for every other <c>attack_target_after_spawn</c> spawn.
    /// </remarks>
    private void SpawnAdds(int npcId, int hpThreshold)
    {
        int count = GetLifeStats().GetHpPercentage() < hpThreshold ? 3 : 1;
        foreach (var target in GetAggroList().StreamValidTargets(20).Take(count))
        {
            if (Spawn(npcId, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0) is not Npc add)
                continue;

            adds.Add(add);
            AttackAfterSpawn.Now(add, target, OnArrival);
        }
    }

    private void DespawnAdds()
    {
        foreach (Npc npc in adds)
            if (npc != null)
                npc.GetController().Delete();

        // Retail clears SPAWN_ID_2 -- the field -- on both of its death branches.
        field?.GetController().DeleteIfAliveOrCancelRespawn();
        field = null;
    }

    /// <summary>
    /// Retail clears both spawn groups on dying, which this class did only on despawning and on
    /// going home — so a killed protector left its adds and its field standing until they decayed.
    /// </summary>
    protected override void HandleDied()
    {
        DespawnAdds();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        DespawnAdds();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        DespawnAdds();
        base.HandleBackHome();
        hpPhases.Reset();
    }
}

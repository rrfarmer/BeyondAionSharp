using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Grand Commander Pashid (231130), the Eternal Bastion's fifth wave. Retail pattern
/// <c>IDF5_TD_Wave5_Boss</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/eternalBastion/EternalBastionCommanderPashidAI (@author Estrayl).
/// Retail-sourced correction below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>He was spawning the ordinary rider's strike, not his own.</b> There are two: <c>284697</c>
/// "pashid siege dragon" is <c>BIDF5_TD_DragonRiderStrike</c>, spawned by the rank-and-file
/// <c>IDF5_TD_DragonRider_N_65_Ae</c> and by the wave's four summon patterns; <c>284698</c>
/// "grand commander pashid" is <c>BIDF5_TD_DragonRiderChiefStrike</c>, and it is the one
/// <c>IDF5_TD_Wave5_Boss</c> — his pattern — puts down on <c>on_wake_up</c>.
/// </para>
/// <para>
/// <b>The correction is currently inert, and that is worth knowing before anyone tests it.</b> Both
/// strike npcs run <c>useSkillAndDie</c>, which reads the npc's skill list and deletes the npc when it
/// is empty — and <b>neither 284697 nor 284698 has an <c>npc_skills</c> entry in our data</b>. So both
/// ids appear and immediately remove themselves. Fixing the id is still right: it is what retail spawns,
/// and it is the half of the problem that lives in code rather than in data.
/// </para>
/// </remarks>
[AIName("eternal_bastion_commander_pashid")]
public class EternalBastionCommanderPashidAI : EternalBastionAggressiveNpcAI
{
    /// <summary>Retail's <c>BIDF5_TD_DragonRiderChiefStrike</c>, spawned by his own wake rung.</summary>
    public const int ChiefStrike = 284698;

    private Npc commander;
    private double dist;

    public EternalBastionCommanderPashidAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Spawn(ChiefStrike, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
        commander = GetPosition().GetWorldMapInstance().GetNpc(209516);
        if (commander == null)
            commander = GetPosition().GetWorldMapInstance().GetNpc(209517);
        HateCommander(500000);
    }

    private void HateCommander(int hate)
    {
        if (commander != null && !GetOwner().GetAggroList().IsHating(commander) && IsInRange(commander, 20))
            GetOwner().GetAggroList().AddHate(commander, hate);
    }

    /*
     * Simulate retail behaviour here.
     * Pashid and the defense commander will occasionally exchange messages which will result in some shout events and adding mutual hate.
     */
    protected override void HandleTargetChanged(Creature creature)
    {
        base.HandleTargetChanged(creature);
        ThreadPoolManager.GetInstance().Schedule(_ => { HateCommander(2000000); return ValueTask.CompletedTask; }, 2000L);
    }

    public override AttackTypeAnimation GetAttackTypeAnimation(Creature target)
    {
        dist = PositionUtil.GetDistance(GetOwner(), target) - GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()
            - target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide();
        return dist > 10 ? AttackTypeAnimation.RANGED : AttackTypeAnimation.MELEE;
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return effect == null && dist > 10 ? damage * 1.3f : damage;
    }

    public override ItemAttackType ModifyAttackType(ItemAttackType type)
    {
        return dist > 10 ? ItemAttackType.MAGICAL_EARTH : ItemAttackType.PHYSICAL;
    }

    public override AttackHandAnimation ModifyAttackHandAnimation(AttackHandAnimation attackHandAnimation)
    {
        return Rnd.Get(Enum.GetValues<AttackHandAnimation>());
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 21239: // Gnaw
                GetAggroList().AddHate(GetAggroList().GetTarget(AggroTarget.RANDOM), 100000);
                break;
            case 21236: // Exultation
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500757);
                break;
        }
    }

    protected override void HandleDied()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500759);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        commander = null;
    }
}

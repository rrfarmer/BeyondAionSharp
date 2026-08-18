using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/sauroBase/BrigadeGeneralShebaAI (@author Estrayl, Yeats).</summary>
[AIName("brigade_general_sheba")]
public class BrigadeGeneralShebaAI : AggressiveNpcAI
{
    public BrigadeGeneralShebaAI(Npc owner) : base(owner)
    {
    }

    private float multiplier = 1f;
    private bool spawnCenter = true;

    /// <summary>
    /// Retail's <c>IDVritra_Base_Boss1</c> raises the base alarm at fifty metres as it engages, and the
    /// supply base's guards answer by taking whoever he is fighting. Added to this class rather than
    /// translated into one, because everything else here is a Java port. See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly CombatAlarm alarm = new CombatAlarm(ShebanBladesmanAI.BaseAlarm, 50f);

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        alarm.Raise(GetOwner());
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * multiplier;
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        multiplier = 1f;
        switch (skillTemplate.GetSkillId())
        {
            case 21188:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500775);
                multiplier = 0.55f;
                break;
            case 21183:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500779);
                multiplier = 0.55f;
                break;
            case 21187:
            case 21410:
            case 21425:
            case 21450:
                multiplier = 0.55f;
                break;
            case 21186:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500780);
                multiplier = 0.55f;
                break;
            case 21189:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500774);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        multiplier = 1f;
        switch (skillTemplate.GetSkillId())
        {
            case 21189: // danuar henchman
                Spawn(284435, 891.17f, 880.87f, 411.54f, (sbyte)14);
                Spawn(284435, 890.8f, 898.97f, 411.46f, (sbyte)105);
                Spawn(284435, 904.95f, 880.1f, 411.34f, (sbyte)40);
                Spawn(284435, 905.95f, 900.22f, 411.34f, (sbyte)80);
                break;
            case 21184: // danuar channeling
                if (spawnCenter)
                {
                    Spawn(284436, 900.1f, 889.84f, 411.54f, (sbyte)20);
                    spawnCenter = false;
                }
                else
                {
                    Spawn(284436, 887.63f, 902.31f, 411.46f, (sbyte)105);
                    Spawn(284436, 882.2f, 889.77f, 411.46f, (sbyte)120);
                    Spawn(284436, 887.59f, 877.32f, 411.46f, (sbyte)15);
                    Spawn(284436, 900f, 872.28f, 411.46f, (sbyte)30);
                    Spawn(284436, 912.39f, 877.44f, 411.46f, (sbyte)45);
                    Spawn(284436, 917.2f, 889.89f, 411.46f, (sbyte)60);
                    Spawn(284436, 912.33f, 902f, 411.46f, (sbyte)75);
                    Spawn(284436, 900f, 907.16f, 411.46f, (sbyte)90);
                    spawnCenter = true;
                }
                break;
            case 21186:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500781);
                break;
        }
    }

    protected override void HandleBackHome()
    {
        alarm.Rearm();
        RemoveAdds();
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        alarm.Rearm();
        RemoveAdds();
        base.HandleDied();
    }

    private void RemoveAdds()
    {
        GetOwner().GetWorldMapInstance().GetNpcs(284435, 284436).ToList().ForEach(npc => npc.GetController().Delete());
    }
}

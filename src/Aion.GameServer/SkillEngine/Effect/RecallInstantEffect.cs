using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/RecallInstantEffect (Bio, Sippolo) : EffectTemplate. applyEffect: anonymous RequestResponseHandler&lt;Creature&gt; capturing effect world/instance/loc→nested RecallRequestHandler; deny→STR_MSG_Recall_Rejected_EFFECT both sides, accept→TeleportService.teleportTo; putRequest STR_SUMMON_PARTY_DO_YOU_ACCEPT_REQUEST→SM_QUESTION_WINDOW (30s); calculate: Player + not in combat + same world + effector not in instance + not enemy→setTargetPosition + addSuccessEffect. RequestResponseHandler/SM_QUESTION_WINDOW red-tolerated.</summary>
[XmlType("RecallInstantEffect")]
public class RecallInstantEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        Player effected = (Player)effect.GetEffected();

        // TODO need to confirm if cannot be summoned while on abnormal effects stunned, sleeping, feared, etc.
        RequestResponseHandler<Creature> rrh = new RecallRequestHandler(effector, effect);

        if (effected.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_SUMMON_PARTY_DO_YOU_ACCEPT_REQUEST, rrh))
            PacketSendUtility.SendPacket(effected,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_SUMMON_PARTY_DO_YOU_ACCEPT_REQUEST, 0, 0, effector.GetName(), "Summon Group Member", 30));
    }

    public override void Calculate(Effect effect)
    {
        Creature effector = effect.GetEffector();

        if (!(effect.GetEffected() is Player))
            return;
        Player effected = (Player)effect.GetEffected();

        if (effected.GetController().IsInCombat())
            return;

        if (effector.GetWorldId() == effected.GetWorldId() && !effector.IsInInstance() && !(effector.IsEnemy(effected)))
        {
            effect.GetSkill().SetTargetPosition(effector.GetX(), effector.GetY(), effector.GetZ(), (sbyte)effector.GetHeading());
            effect.AddSuccessEffect(this);
        }
    }

    private sealed class RecallRequestHandler : RequestResponseHandler<Creature>
    {
        private readonly int worldId;
        private readonly int instanceId;
        private readonly float locationX;
        private readonly float locationY;
        private readonly float locationZ;
        private readonly byte locationH;

        public RecallRequestHandler(Creature effector, Effect effect)
            : base(effector)
        {
            worldId = effect.GetWorldId();
            instanceId = effect.GetInstanceId();
            locationX = effect.GetSkill().GetX();
            locationY = effect.GetSkill().GetY();
            locationZ = effect.GetSkill().GetZ();
            locationH = (byte)effect.GetSkill().GetH();
        }

        public override void DenyRequest(Creature effector, Player effected)
        {
            PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Rejected_EFFECT(effected.GetName()));
            PacketSendUtility.SendPacket(effected, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Rejected_EFFECT(effector.GetName()));
        }

        public override void AcceptRequest(Creature effector, Player effected)
        {
            TeleportService.TeleportTo(effected, worldId, instanceId, locationX, locationY, locationZ, locationH);
        }
    }
}

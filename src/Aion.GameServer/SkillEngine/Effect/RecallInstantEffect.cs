using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/RecallInstantEffect (Bio, Sippolo, SVDNESS) : EffectTemplate. applyEffect: SM_RECALLED_BY_OTHER window with 3-way answer handling (0 accept, 1 refuse, 2 timeout) and a duplicate-effect message when a request is already pending; calculate: Player + not in combat + same world + not enemy + canRecallTo; canRecallTo checks destination zones and world map RECALL flag.</summary>
[XmlType("RecallInstantEffect")]
public class RecallInstantEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        Player effected = (Player)effect.GetEffected();
        RequestResponseHandler<Creature> rrh = new RecallRequestHandler(effector, effect);
        if (effected.GetResponseRequester().PutRequest(SM_RECALLED_BY_OTHER.RECALL_REQUEST_ID, rrh))
        {
            PacketSendUtility.SendPacket(effected, new SM_RECALLED_BY_OTHER(effector.GetName(), effect.GetSkillId(), 30));
        }
        else
        {
            // You cannot summon %0 as you are already under the same effect.
            PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_MSG_Recall_DUPLICATE_EFFECT(effected.GetName()));
        }
    }

    public override void Calculate(Effect effect)
    {
        Creature effector = effect.GetEffector();
        if (!(effect.GetEffected() is Player effected))
        {
            return;
        }
        if (effected.GetController().IsInCombat())
        {
            return;
        }
        if (effector.GetWorldId() != effected.GetWorldId())
        {
            return;
        }
        if (effector.IsEnemy(effected))
        {
            return;
        }
        if (!CanRecallTo(effector))
        {
            return;
        }
        effect.GetSkill().SetTargetPosition(effector.GetX(), effector.GetY(), effector.GetZ(), (sbyte)effector.GetHeading());
        effect.AddSuccessEffect(this);
    }

    /// <summary>Single check for recall restrictions in the destination zone and world. Used before and after the cast.</summary>
    public static bool CanRecallTo(Creature effector)
    {
        foreach (Aion.GameServer.World.Zone.ZoneInstance zone in effector.FindZones())
        {
            if (!zone.CanRecall())
            {
                return false;
            }
        }
        return effector.GetPosition().GetWorldMapInstance().GetParent().CanRecall();
    }

    private sealed class RecallRequestHandler : RequestResponseHandler<Creature>
    {
        private readonly Creature effector;
        private readonly int worldId;
        private readonly int instanceId;
        private readonly float locationX;
        private readonly float locationY;
        private readonly float locationZ;
        private readonly byte locationH;

        public RecallRequestHandler(Creature effector, Effect effect)
            : base(effector)
        {
            this.effector = effector;
            worldId = effect.GetWorldId();
            instanceId = effect.GetInstanceId();
            locationX = effect.GetSkill().GetX();
            locationY = effect.GetSkill().GetY();
            locationZ = effect.GetSkill().GetZ();
            locationH = (byte)effect.GetSkill().GetH();
        }

        public override void AcceptRequest(Creature effector, Player effected)
        {
            TeleportService.TeleportTo(effected, worldId, instanceId, locationX, locationY, locationZ, locationH);
        }

        public override void Handle(Player responder, int answer)
        {
            switch (answer)
            {
                case 0: // Accept.
                    AcceptRequest(effector, responder);
                    break;
                case 1: // Refuse.
                    // %0 declined your summoning.
                    PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Rejected_EFFECT(responder.GetName()));
                    // You declined %0's summoning.
                    PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Reject_EFFECT(effector.GetName()));
                    break;
                case 2: // Time-out.
                    // Summoning of %0 is cancelled as the confirmation stand-by time has been exceeded.
                    PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_MSG_Recall_DONOT_ACCEPT_EFFECT(responder.GetName()));
                    break;
                default:
                    break;
            }
        }
    }
}

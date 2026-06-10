using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ReturnPointEffect (ATracer) : EffectTemplate. applyEffect: effector Player; RESTING→unset+SM_EMOTION STAND w/ targetObjectId; itemTemplate returnWorldId/returnAlias→TeleportService.useTeleportScroll; calculate: itemTemplate!=null→addSuccessEffect; getTargetObjectId helper (null target→0). ItemTemplate/TeleportService red-tolerated.</summary>
[XmlType("ReturnPointEffect")]
public class ReturnPointEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Player player = (Player)effect.GetEffector();
        if (player.IsInState(CreatureState.RESTING))
        {
            player.UnsetState(CreatureState.RESTING);
            PacketSendUtility.BroadcastPacket(player,
                new SM_EMOTION(player, EmotionType.STAND, 0, player.GetX(), player.GetY(), player.GetZ(), player.GetHeading(), GetTargetObjectId(player)),
                true);
        }
        ItemTemplate itemTemplate = effect.GetItemTemplate();
        int worldId = itemTemplate.GetReturnWorldId();
        string pointAlias = itemTemplate.GetReturnAlias();
        TeleportService.UseTeleportScroll(((Player)effect.GetEffector()), pointAlias, worldId);
    }

    public override void Calculate(Effect effect)
    {
        ItemTemplate itemTemplate = effect.GetItemTemplate();
        if (itemTemplate != null)
            effect.AddSuccessEffect(this);
    }

    private int GetTargetObjectId(Player player)
    {
        return player.GetTarget() == null ? 0 : player.GetTarget().GetObjectId();
    }
}

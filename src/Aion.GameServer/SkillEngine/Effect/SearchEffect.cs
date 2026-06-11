using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SearchEffect (Sweetkr) : EffectTemplate. @XmlAttribute state(CreatureSeeState); applyEffect→addToEffectedController; startEffect: setSeeState+updateKnownlist+SM_PLAYER_STATE; endEffect: unsetSeeState+updateKnownlist+SM_PLAYER_STATE. CreatureSeeState red-tolerated.</summary>
[XmlType("SearchEffect")]
public class SearchEffect : EffectTemplate
{
    [XmlAttribute]
    protected CreatureSeeState state;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();

        effected.UnsetSeeState(state);
        effected.UpdateKnownlist();

        PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_PLAYER_STATE(effected));
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();

        effected.SetSeeState(state);
        effected.UpdateKnownlist();

        PacketSendUtility.BroadcastPacketAndReceive(effected, new SM_PLAYER_STATE(effected));
    }
}

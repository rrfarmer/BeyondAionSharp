using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUMMON_EMOTION (ATracer). Summon/mercenary emotion (fly/land/jump/attack-mode) broadcast. EmotionType ported PascalCase (EmotionTypes.FromId). SM_EMOTION/CreatureState red-tolerated.</summary>
public class CM_SUMMON_EMOTION : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_SUMMON_EMOTION));

    private int objId;
    private int emotionTypeId;

    public CM_SUMMON_EMOTION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objId = ReadD();
        emotionTypeId = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Creature summonOrMercenary = player.GetSummonOrMercenary(objId);
        if (summonOrMercenary == null) // commonly due to lags when the pet dies
            return;

        EmotionType emotionType = EmotionTypes.FromId(emotionTypeId);
        switch (emotionType)
        {
            case EmotionType.Fly:
            case EmotionType.Land:
                PacketSendUtility.BroadcastPacket(summonOrMercenary, new SM_EMOTION(summonOrMercenary, EmotionType.ChangeSpeed));
                PacketSendUtility.BroadcastPacket(summonOrMercenary, new SM_EMOTION(summonOrMercenary, emotionType));
                break;
            case EmotionType.Jump:
            case EmotionType.SummonStopJump:
                PacketSendUtility.BroadcastPacket(summonOrMercenary, new SM_EMOTION(summonOrMercenary, emotionType));
                break;
            case EmotionType.AttackModeInMove: // start attacking
                summonOrMercenary.SetState(CreatureState.WeaponEquipped);
                PacketSendUtility.BroadcastPacket(summonOrMercenary, new SM_EMOTION(summonOrMercenary, emotionType));
                break;
            case EmotionType.NeutralModeInMove: // stop attacking
                summonOrMercenary.UnsetState(CreatureState.WeaponEquipped);
                PacketSendUtility.BroadcastPacket(summonOrMercenary, new SM_EMOTION(summonOrMercenary, emotionType));
                break;
            case EmotionType.None:
                if (emotionTypeId != (int)EmotionType.None)
                    log.LogWarning("Unknown emotion type " + emotionTypeId + " from " + player);
                break;
        }
    }
}

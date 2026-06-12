using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EMOTION (SoulKeeper, nerolory). Handles player emotion/state transitions (sit/stand/fly/land/sprint/powershard/etc). EmotionType ported PascalCase by the loop (EmotionTypes.FromId). SM_EMOTION/AbnormalState red-tolerated.</summary>
public class CM_EMOTION : AionClientPacket
{
    /// <summary>Logger</summary>
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_EMOTION));
    /// <summary>Emotion number</summary>
    private EmotionType emotionType;
    /// <summary>Emotion number</summary>
    private int emotion;
    /// <summary>Coordinates of player</summary>
    private float x, y, z;
    private byte heading;

    private int targetObjectId;

    public CM_EMOTION(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        int et = ReadUC();
        emotionType = EmotionTypes.FromId(et);

        switch (emotionType)
        {
            case EmotionType.SELECT_TARGET:// select target
            case EmotionType.JUMP: // jump
            case EmotionType.SIT: // resting
            case EmotionType.STAND: // end resting
            case EmotionType.LAND_FLYTELEPORT: // fly teleport land
            case EmotionType.FLY: // fly up
            case EmotionType.LAND: // land
            case EmotionType.DIE: // die
            case EmotionType.EMOTE_END: // duel end
            case EmotionType.WALK: // walk on
            case EmotionType.RUN: // walk off
            case EmotionType.OPEN_DOOR: // open static doors
            case EmotionType.CLOSE_DOOR: // close static doors
            case EmotionType.POWERSHARD_ON: // powershard on
            case EmotionType.POWERSHARD_OFF: // powershard off
            case EmotionType.ATTACKMODE_IN_MOVE: // get equip weapon
            case EmotionType.ATTACKMODE_IN_STANDING: // get equip weapon
            case EmotionType.NEUTRALMODE_IN_MOVE: // remove equip weapon
            case EmotionType.NEUTRALMODE_IN_STANDING: // remove equip weapon
            case EmotionType.END_SPRINT:
                break;
            case EmotionType.WINDSTREAM_STRAFE:
                ReadC(); // unk 2
                break;
            case EmotionType.START_SPRINT:
                ReadD(); // unk 1
                break;
            case EmotionType.EMOTE:
                emotion = ReadUH();
                targetObjectId = ReadD();
                break;
            case EmotionType.CHAIR_SIT: // sit on chair
            case EmotionType.CHAIR_UP: // stand on chair
                x = ReadF();
                y = ReadF();
                z = ReadF();
                heading = ReadC();
                break;
            default:
                log.LogError("Unknown emotion type? 0x" + et.ToString("X", CultureInfo.InvariantCulture)/* !!!!! */);
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsDead())
        {
            return;
        }

        if (emotionType != EmotionType.SELECT_TARGET && emotionType != EmotionType.ATTACKMODE_IN_MOVE
            && emotionType != EmotionType.ATTACKMODE_IN_STANDING && emotionType != EmotionType.NEUTRALMODE_IN_MOVE
            && emotionType != EmotionType.NEUTRALMODE_IN_STANDING)
        {
            if (player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE) || player.GetEffectController().IsUnderFear() || player.GetEffectController().IsConfused())
            {
                return;
            }
        }

        if (player.IsInState(CreatureState.PRIVATE_SHOP) || player.IsInAttackMode()
            && (emotionType == EmotionType.CHAIR_SIT || emotionType == EmotionType.JUMP))
            return;

        Item usingItem = player.GetUsingItem();
        if (usingItem == null || !HasRideAction(usingItem)) // don't cancel getting on mount
            player.GetController().CancelUseItem();
        if (emotionType == EmotionType.SELECT_TARGET)
            return;

        player.GetController().CancelCurrentSkill(null);

        // check for stance
        if (player.GetController().IsUnderStance())
        {
            switch (emotionType)
            {
                case EmotionType.FLY:
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_TAKE_OFF__WHILE_IN_CURRENT_STANCE());
                    return;
                default:
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CHANGE_MODE__WHILE_IN_CURRENT_STANCE());
                    return;
            }
        }

        switch (emotionType)
        {
            case EmotionType.SIT:
                if (player.IsInState(CreatureState.PRIVATE_SHOP))
                {
                    return;
                }
                player.GetObserveController().NotifySitObservers();
                if (player.IsInPlayerMode(PlayerMode.RIDE))
                {
                    player.UnsetPlayerMode(PlayerMode.RIDE);
                }
                player.SetState(CreatureState.RESTING);
                break;
            case EmotionType.STAND:
                player.UnsetState(CreatureState.RESTING);
                break;
            case EmotionType.CHAIR_SIT:
                player.SetState(CreatureState.CHAIR, true);
                break;
            case EmotionType.CHAIR_UP:
                if (player.IsInState(CreatureState.CHAIR))
                    player.SetState(CreatureState.ACTIVE, true);
                break;
            case EmotionType.LAND_FLYTELEPORT:
                player.GetController().OnFlyTeleportEnd();
                break;
            case EmotionType.FLY:
                if (!player.GetFlyController().StartFly(false, false))
                    return;
                break;
            case EmotionType.LAND:
                player.GetFlyController().EndFly(false);
                break;
            case EmotionType.ATTACKMODE_IN_MOVE:
            case EmotionType.ATTACKMODE_IN_STANDING:
                player.SetState(CreatureState.WEAPON_EQUIPPED);
                break;
            case EmotionType.NEUTRALMODE_IN_MOVE:
            case EmotionType.NEUTRALMODE_IN_STANDING:
                player.UnsetState(CreatureState.WEAPON_EQUIPPED);
                break;
            case EmotionType.WALK:
                if (player.IsFlying()) // cannot toggle walk when flying or gliding
                    return;
                player.SetState(CreatureState.WALK_MODE);
                break;
            case EmotionType.RUN:
                player.UnsetState(CreatureState.WALK_MODE);
                break;
            case EmotionType.OPEN_DOOR:
            case EmotionType.CLOSE_DOOR:
                break;
            case EmotionType.POWERSHARD_ON:
                if (!player.GetEquipment().IsPowerShardEquipped())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_NO_BOOSTER_EQUIPED());
                    return;
                }
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_STARTED());
                player.SetState(CreatureState.POWERSHARD);
                break;
            case EmotionType.POWERSHARD_OFF:
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_ENDED());
                player.UnsetState(CreatureState.POWERSHARD);
                break;
            case EmotionType.START_SPRINT:
                if (!player.IsInPlayerMode(PlayerMode.RIDE) || player.GetLifeStats().GetCurrentFp() < player.ride.GetStartFp() || player.IsFlying()
                    || !player.ride.CanSprint())
                {
                    return;
                }
                player.SetSprintMode(true);
                player.GetLifeStats().TriggerFpReduce();
                break;
            case EmotionType.END_SPRINT:
                if (!player.IsInPlayerMode(PlayerMode.RIDE) || !player.ride.CanSprint() || !player.IsInSprintMode())
                {
                    return;
                }
                player.SetSprintMode(false);
                player.GetLifeStats().TriggerFpRestore();
                break;
        }

        if (player.GetEmotions().CanUse(emotion))
        {
            PacketSendUtility.BroadcastToSightedPlayers(player, new SM_EMOTION(player, emotionType, emotion, x, y, z, heading, GetTargetObjectId(player)), true);
        }

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();
    }

    private bool HasRideAction(Item item)
    {
        ItemActions actions = item.GetItemTemplate().GetActions();
        return actions != null && actions.GetRideAction() != null;
    }

    private int GetTargetObjectId(Player player)
    {
        return player.GetTarget() == null ? targetObjectId : player.GetTarget().GetObjectId();
    }
}

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
using State = Aion.GameServer.Network.Aion.AionConnection.State;

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
            case EmotionType.SelectTarget:// select target
            case EmotionType.Jump: // jump
            case EmotionType.Sit: // resting
            case EmotionType.Stand: // end resting
            case EmotionType.LandFlyTeleport: // fly teleport land
            case EmotionType.Fly: // fly up
            case EmotionType.Land: // land
            case EmotionType.Die: // die
            case EmotionType.EmoteEnd: // duel end
            case EmotionType.Walk: // walk on
            case EmotionType.Run: // walk off
            case EmotionType.OpenDoor: // open static doors
            case EmotionType.CloseDoor: // close static doors
            case EmotionType.PowershardOn: // powershard on
            case EmotionType.PowershardOff: // powershard off
            case EmotionType.AttackModeInMove: // get equip weapon
            case EmotionType.AttackModeInStanding: // get equip weapon
            case EmotionType.NeutralModeInMove: // remove equip weapon
            case EmotionType.NeutralModeInStanding: // remove equip weapon
            case EmotionType.EndSprint:
                break;
            case EmotionType.WindstreamStrafe:
                ReadC(); // unk 2
                break;
            case EmotionType.StartSprint:
                ReadD(); // unk 1
                break;
            case EmotionType.Emote:
                emotion = ReadUH();
                targetObjectId = ReadD();
                break;
            case EmotionType.ChairSit: // sit on chair
            case EmotionType.ChairUp: // stand on chair
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

        if (emotionType != EmotionType.SelectTarget && emotionType != EmotionType.AttackModeInMove
            && emotionType != EmotionType.AttackModeInStanding && emotionType != EmotionType.NeutralModeInMove
            && emotionType != EmotionType.NeutralModeInStanding)
        {
            if (player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_MOVE_STATE) || player.GetEffectController().IsUnderFear() || player.GetEffectController().IsConfused())
            {
                return;
            }
        }

        if (player.IsInState(CreatureState.PrivateShop) || player.IsInAttackMode()
            && (emotionType == EmotionType.ChairSit || emotionType == EmotionType.Jump))
            return;

        Item usingItem = player.GetUsingItem();
        if (usingItem == null || !HasRideAction(usingItem)) // don't cancel getting on mount
            player.GetController().CancelUseItem();
        if (emotionType == EmotionType.SelectTarget)
            return;

        player.GetController().CancelCurrentSkill(null);

        // check for stance
        if (player.GetController().IsUnderStance())
        {
            switch (emotionType)
            {
                case EmotionType.Fly:
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_TAKE_OFF__WHILE_IN_CURRENT_STANCE());
                    return;
                default:
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CHANGE_MODE__WHILE_IN_CURRENT_STANCE());
                    return;
            }
        }

        switch (emotionType)
        {
            case EmotionType.Sit:
                if (player.IsInState(CreatureState.PrivateShop))
                {
                    return;
                }
                player.GetObserveController().NotifySitObservers();
                if (player.IsInPlayerMode(PlayerMode.RIDE))
                {
                    player.UnsetPlayerMode(PlayerMode.RIDE);
                }
                player.SetState(CreatureState.Resting);
                break;
            case EmotionType.Stand:
                player.UnsetState(CreatureState.Resting);
                break;
            case EmotionType.ChairSit:
                player.SetState(CreatureState.Chair, true);
                break;
            case EmotionType.ChairUp:
                if (player.IsInState(CreatureState.Chair))
                    player.SetState(CreatureState.Active, true);
                break;
            case EmotionType.LandFlyTeleport:
                player.GetController().OnFlyTeleportEnd();
                break;
            case EmotionType.Fly:
                if (!player.GetFlyController().StartFly(false, false))
                    return;
                break;
            case EmotionType.Land:
                player.GetFlyController().EndFly(false);
                break;
            case EmotionType.AttackModeInMove:
            case EmotionType.AttackModeInStanding:
                player.SetState(CreatureState.WeaponEquipped);
                break;
            case EmotionType.NeutralModeInMove:
            case EmotionType.NeutralModeInStanding:
                player.UnsetState(CreatureState.WeaponEquipped);
                break;
            case EmotionType.Walk:
                if (player.IsFlying()) // cannot toggle walk when flying or gliding
                    return;
                player.SetState(CreatureState.WalkMode);
                break;
            case EmotionType.Run:
                player.UnsetState(CreatureState.WalkMode);
                break;
            case EmotionType.OpenDoor:
            case EmotionType.CloseDoor:
                break;
            case EmotionType.PowershardOn:
                if (!player.GetEquipment().IsPowerShardEquipped())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_NO_BOOSTER_EQUIPED());
                    return;
                }
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_STARTED());
                player.SetState(CreatureState.Powershard);
                break;
            case EmotionType.PowershardOff:
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WEAPON_BOOST_BOOST_MODE_ENDED());
                player.UnsetState(CreatureState.Powershard);
                break;
            case EmotionType.StartSprint:
                if (!player.IsInPlayerMode(PlayerMode.RIDE) || player.GetLifeStats().GetCurrentFp() < player.ride.GetStartFp() || player.IsFlying()
                    || !player.ride.CanSprint())
                {
                    return;
                }
                player.SetSprintMode(true);
                player.GetLifeStats().TriggerFpReduce();
                break;
            case EmotionType.EndSprint:
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

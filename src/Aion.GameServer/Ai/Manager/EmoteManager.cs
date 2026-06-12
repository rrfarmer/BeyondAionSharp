using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/EmoteManager (ATracer). Broadcasts NPC movement/combat emotions. EmotionType divergent enum (CHANGE_SPEED/
/// ATTACKMODE_IN_MOVE/NEUTRALMODE_IN_MOVE/WALK -> ChangeSpeed/AttackModeInMove/NeutralModeInMove/Walk); CreatureState.WALK_MODE/
/// WEAPON_EQUIPPED -> WalkMode/WeaponEquipped. SM_SYSTEM_MESSAGE catalog red-tolerated.
/// </summary>
public class EmoteManager
{
    /// <summary>Npc starts attacking from idle state</summary>
    public static void EmoteStartAttacking(Npc owner, Creature target)
    {
        owner.UnsetState(CreatureState.WALK_MODE);
        if (!owner.IsInState(CreatureState.WEAPON_EQUIPPED))
        {
            owner.SetState(CreatureState.WEAPON_EQUIPPED);
            PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED, 0, target.GetObjectId()));
            PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.ATTACKMODE_IN_MOVE, 0, target.GetObjectId()));
        }
    }

    /// <summary>Npc stops attacking</summary>
    public static void EmoteStopAttacking(Npc owner)
    {
        owner.UnsetState(CreatureState.WEAPON_EQUIPPED);
        VisibleObject target = owner.GetTarget();
        if (target is Player player)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UI_COMBAT_NPC_RETURN(owner.GetObjectTemplate().GetL10n()));
        }
    }

    /// <summary>Npc starts following other creature</summary>
    public static void EmoteStartFollowing(Npc owner)
    {
        owner.UnsetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED, 0, 0));
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.NEUTRALMODE_IN_MOVE, 0, 0));
    }

    /// <summary>Npc starts walking (either random or path)</summary>
    public static void EmoteStartWalking(Npc owner)
    {
        owner.SetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.WALK));
    }

    /// <summary>Npc stops walking</summary>
    public static void EmoteStopWalking(Npc owner)
    {
        owner.UnsetState(CreatureState.WALK_MODE);
    }

    /// <summary>Npc starts returning to spawn location</summary>
    public static void EmoteStartReturning(Npc owner)
    {
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED, 0, 0));
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.NEUTRALMODE_IN_MOVE, 0, 0));
    }

    /// <summary>Npc starts idling</summary>
    public static void EmoteStartIdling(Npc owner)
    {
        owner.SetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.CHANGE_SPEED, 0, 0));
        PacketSendUtility.BroadcastPacket(owner, new SM_EMOTION(owner, EmotionType.NEUTRALMODE_IN_MOVE, 0, 0));
    }
}

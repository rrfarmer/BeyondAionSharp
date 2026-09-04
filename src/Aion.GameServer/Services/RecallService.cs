using System.Collections.Concurrent;
using System.Threading.Tasks;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/RecallService. Handles pending summon requests (Summon Group Member, example skillId: 3777).
/// The request is not re-validated when the summoned player accepts it. Instead, every state change that would
/// invalidate it cancels the request, see Cancel(Player, CancelReason).
/// </summary>
public class RecallService
{
    public enum CancelReason
    {
        /// <summary>Nobody answered within CONFIRMATION_SECONDS. Only the caster is notified.</summary>
        TIMEOUT,
        /// <summary>The summoned player declined.</summary>
        DECLINED,
        /// <summary>Something invalidated the request (combat, death, logout, teleport, ...). Both sides are notified.</summary>
        CANCELLED,
    }

    private const int CONFIRMATION_SECONDS = 30;

    private readonly ConcurrentDictionary<int, Request> requests = new();

    private static readonly RecallService Instance = new();

    public static RecallService GetInstance()
    {
        return Instance;
    }

    private RecallService()
    {
    }

    public bool HasPendingRequest(Player summoned)
    {
        return requests.ContainsKey(summoned.GetObjectId());
    }

    /// <summary>
    /// Asks the summoned player whether he wants to be teleported to the position the caster is standing on.
    /// </summary>
    public void RequestSummon(Player caster, Player summoned, int skillId)
    {
        Request request = new Request(caster.GetObjectId(), caster.GetWorldId(), caster.GetInstanceId(), caster.GetX(), caster.GetY(), caster.GetZ(),
            (byte)caster.GetHeading());
        if (!requests.TryAdd(summoned.GetObjectId(), request))
            return;
        request.Timeout = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (requests.TryGetValue(summoned.GetObjectId(), out Request current) && current == request) // never time out a request which replaced this one
                Cancel(summoned, CancelReason.TIMEOUT);
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(CONFIRMATION_SECONDS * 1000));
        PacketSendUtility.SendPacket(summoned, new SM_RECALLED_BY_OTHER(caster.GetName(), skillId, CONFIRMATION_SECONDS));
    }

    /// <summary>
    /// Teleports the player to the caster of his pending request, without validating it again.
    /// </summary>
    public void Accept(Player summoned)
    {
        Request request = Remove(summoned);
        if (request != null)
            TeleportService.TeleportTo(summoned, request.WorldId, request.InstanceId, request.X, request.Y, request.Z, request.Heading);
    }

    /// <summary>
    /// Drops the pending request of the given player, if there is one, and notifies both sides as the reason dictates.
    /// </summary>
    public void Cancel(Player summoned, CancelReason reason)
    {
        Request request = Remove(summoned);
        if (request == null)
            return;
        if (reason == CancelReason.TIMEOUT || reason == CancelReason.CANCELLED)
            PacketSendUtility.SendPacket(summoned, new SM_RECALLED_BY_OTHER()); // the client closes the window itself only when it answered

        Player caster = Aion.GameServer.World.World.GetInstance().GetPlayer(request.CasterObjectId);
        if (caster == null)
            return;
        switch (reason)
        {
            case CancelReason.TIMEOUT:
                PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_MSG_Recall_DONOT_ACCEPT_EFFECT(summoned.GetName()));
                break;
            case CancelReason.DECLINED:
                PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Rejected_EFFECT(summoned.GetName()));
                PacketSendUtility.SendPacket(summoned, SM_SYSTEM_MESSAGE.STR_MSG_Recall_Reject_EFFECT(caster.GetName()));
                break;
            case CancelReason.CANCELLED:
                PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_MSG_Recall_CANCEL_EFFECT(summoned.GetName()));
                PacketSendUtility.SendPacket(summoned, SM_SYSTEM_MESSAGE.STR_MSG_Recall_CANCEL_EFFECT(caster.GetName()));
                break;
        }
    }

    private Request Remove(Player summoned)
    {
        requests.TryRemove(summoned.GetObjectId(), out Request request);
        if (request != null && request.Timeout != null)
            request.Timeout.Cancel(false);
        return request;
    }

    /// <summary>
    /// Checks everything a summon skill needs before it may be cast and tells the caster why it failed.
    /// </summary>
    /// <returns>True, if the cast may start</returns>
    public static bool ValidateCast(Player caster, VisibleObject target)
    {
        if (caster.IsFlying())
        {
            PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_SKILL_RESTRICTION_NO_FLY());
            return false;
        }
        if (!CanRecallAt(caster))
        {
            PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_IN_CURRENT_POSTION());
            return false;
        }
        if (!(target is Player targetPlayer))
        {
            PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
            return false;
        }
        if (GetInstance().HasPendingRequest(targetPlayer))
        {
            PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_MSG_Recall_DUPLICATE_EFFECT(targetPlayer.GetName()));
            return false;
        }
        if (!CanBeSummoned(caster, targetPlayer))
        {
            PacketSendUtility.SendPacket(caster, SM_SYSTEM_MESSAGE.STR_MSG_Recall_CANNOT_ACCEPT_EFFECT(targetPlayer.GetName()));
            return false;
        }
        return true;
    }

    /// <summary>True, if the given player may be summoned by the caster right now.</summary>
    public static bool CanBeSummoned(Creature caster, Creature summoned)
    {
        if (!(summoned is Player summonedPlayer) || caster == summoned)
            return false;
        if (caster.GetWorldId() != summoned.GetWorldId() || caster.GetInstanceId() != summoned.GetInstanceId())
            return false;
        if (caster.IsEnemy(summoned) || summonedPlayer.IsDead())
            return false;
        if (summonedPlayer.GetController().IsInCombat() || summonedPlayer.IsUsingFlightTransporterOrWindstream())
            return false;
        if (summonedPlayer.IsInState(CreatureState.PRIVATE_SHOP))
            return false;
        if (summonedPlayer.GetInteractionTask() != null) // gathering or crafting
            return false;
        return !summonedPlayer.GetTransformModel().CantRecall();
    }

    /// <summary>
    /// True, if summon skills may be cast at the position of the given player. Of the limit zones covering him, the one
    /// with the lowest priority decides, and it can only forbid the summon, never allow one the world map forbids.
    /// </summary>
    public static bool CanRecallAt(Player caster)
    {
        ZoneTemplate decisive = null;
        foreach (ZoneInstance zone in caster.FindZones())
        {
            ZoneTemplate template = zone.GetZoneTemplate();
            if (template.GetZoneType() != ZoneClassName.LIMIT || template.GetFlags() == -1) // no flags at all means no information
                continue;
            if (decisive == null || template.GetPriority() < decisive.GetPriority())
                decisive = template;
        }
        if (decisive != null && (decisive.GetFlags() & (int)ZoneAttributes.Recall) == 0)
            return false;
        return Aion.GameServer.World.World.GetInstance().GetWorldMap(caster.GetWorldId()).CanRecall();
    }

    private class Request
    {
        internal readonly int CasterObjectId;
        internal readonly int WorldId;
        internal readonly int InstanceId;
        internal readonly float X, Y, Z;
        internal readonly byte Heading;
        internal ScheduledTask Timeout;

        internal Request(int casterObjectId, int worldId, int instanceId, float x, float y, float z, byte heading)
        {
            CasterObjectId = casterObjectId;
            WorldId = worldId;
            InstanceId = instanceId;
            X = x;
            Y = y;
            Z = z;
            Heading = heading;
        }
    }
}

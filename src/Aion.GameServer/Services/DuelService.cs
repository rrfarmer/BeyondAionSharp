using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/DuelService (Simple, Sphinx, xTz). ConcurrentHashMap&lt;Integer,Integer&gt;→ConcurrentDictionary&lt;int,int&gt; (get→TryGetValue/int?, remove→TryRemove); Future&lt;?&gt;→ScheduledTask (cancel(false)→Cancel()); anonymous RequestResponseHandler subclasses→nested DuelRequestHandler/DuelWithdrawHandler; schedule(...,5,MINUTES)→Schedule(TimeSpan.FromMinutes(5)); stream/map/filter/forEach→LINQ. SM_* packets/RequestResponseHandler red-tolerated.</summary>
public class DuelService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DuelService));
    private readonly ConcurrentDictionary<int, int> duels = new ConcurrentDictionary<int, int>();
    private readonly ConcurrentDictionary<int, ScheduledTask> drawTasks = new ConcurrentDictionary<int, ScheduledTask>();

    public static DuelService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private DuelService()
    {
        log.LogInformation("DuelService started.");
    }

    /// <summary>Send the duel request to the target.</summary>
    public void OnDuelRequest(Player requester, Player targetPlayer)
    {
        if (targetPlayer == null || requester.Equals(targetPlayer))
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_NO_USER_TO_REQUEST());
            return;
        }
        if (requester.IsInInstance() && !InstanceConfig.INSTANCE_DUEL_ENABLE)
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_MSG_DUEL_CANT_IN_THIS_ZONE());
            return;
        }
        if (IsDueling(requester))
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_YOU_ARE_IN_DUEL_ALREADY());
            return;
        }
        if (IsDueling(targetPlayer))
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_PARTNER_IN_DUEL_ALREADY(targetPlayer.GetName()));
            return;
        }
        if (targetPlayer.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.DUEL))
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_DUEL(targetPlayer.GetName()));
            return;
        }
        if (requester.IsDead() || targetPlayer.IsDead())
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_PARTNER_INVALID(targetPlayer.GetName()));
            return;
        }
        foreach (ZoneInstance zone in targetPlayer.FindZones())
        {
            if (!zone.IsOtherRaceDuelsAllowed() && !targetPlayer.GetRace().Equals(requester.GetRace())
                || (!zone.IsSameRaceDuelsAllowed() && targetPlayer.GetRace().Equals(requester.GetRace())))
            {
                PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_MSG_DUEL_CANT_IN_THIS_ZONE());
                return;
            }
        }

        RequestResponseHandler<Player> rrh = new DuelRequestHandler(this, requester);
        if (targetPlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_ACCEPT_REQUEST, rrh))
        {
            PacketSendUtility.SendPacket(targetPlayer,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_ACCEPT_REQUEST, 0, 0, requester.GetName()));
            PacketSendUtility.SendPacket(targetPlayer, SM_SYSTEM_MESSAGE.STR_DUEL_REQUESTED(requester.GetName()));
            ConfirmDuelWith(requester, targetPlayer);
        }
        else
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_CANT_REQUEST_WHEN_HE_IS_ASKED_QUESTION(targetPlayer.GetName()));
        }
    }

    /// <summary>Asks confirmation for the duel request.</summary>
    public void ConfirmDuelWith(Player requester, Player targetPlayer)
    {
        // Check if requester isn't already in a duel and responder is same race
        if (requester.IsEnemy(targetPlayer))
            return;

        RequestResponseHandler<Player> rrh = new DuelWithdrawHandler(this, targetPlayer);
        requester.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_WITHDRAW_REQUEST, rrh);
        PacketSendUtility.SendPacket(requester,
            new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_WITHDRAW_REQUEST, 0, 0, targetPlayer.GetName()));
        PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_DUEL_REQUEST_TO_PARTNER(targetPlayer.GetName()));
    }

    /// <summary>Rejects the duel request.</summary>
    private void RejectDuelRequest(Player requester, Player responder)
    {
        PacketSendUtility.SendPacket(requester, SM_CLOSE_QUESTION_WINDOW.STR_DUEL_HE_REJECT_DUEL(responder.GetName()));
        PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_DUEL_REJECT_DUEL(requester.GetName()));
        requester.GetResponseRequester().Remove(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_WITHDRAW_REQUEST);
    }

    private void CancelDuelRequest(Player canceller, Player target)
    {
        PacketSendUtility.SendPacket(target, SM_CLOSE_QUESTION_WINDOW.STR_DUEL_REQUESTER_WITHDRAW_REQUEST(canceller.GetName()));
        PacketSendUtility.SendPacket(canceller, SM_SYSTEM_MESSAGE.STR_DUEL_WITHDRAW_REQUEST(target.GetName()));
        target.GetResponseRequester().Remove(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_ACCEPT_REQUEST);
    }

    /// <summary>Starts the duel.</summary>
    private void StartDuel(Player requester, Player responder)
    {
        if (requester.GetResponseRequester().Remove(SM_QUESTION_WINDOW.STR_DUEL_DO_YOU_WITHDRAW_REQUEST))
            PacketSendUtility.SendPacket(requester, SM_CLOSE_QUESTION_WINDOW.CLOSE_QUESTION_WINDOW());
        PacketSendUtility.SendPacket(requester, SM_DUEL.SM_DUEL_STARTED(responder.GetObjectId()));
        PacketSendUtility.SendPacket(responder, SM_DUEL.SM_DUEL_STARTED(requester.GetObjectId()));
        RegisterDuel(requester.GetObjectId(), responder.GetObjectId());
        CreateTask(requester, responder);
        if (requester.IsInAnyHide())
            requester.GetController().OnHide();
        if (responder.IsInAnyHide())
            responder.GetController().OnHide();
    }

    /// <summary>send SM_DELETE a second time to fix client not fading out the char (only happens when dueling with a team member of a group or alliance)</summary>
    public void FixTeamVisibility(Player hiddenDuelist)
    {
        int? opponentId = DuelService.GetInstance().GetOpponentId(hiddenDuelist);
        if (opponentId != null)
        {
            Player opponent = World.GetInstance().GetPlayer(opponentId.Value);
            if (opponent != null && opponent.GetKnownList().Knows(hiddenDuelist) && !opponent.GetKnownList().Sees(hiddenDuelist)
                && hiddenDuelist.IsInSameTeam(opponent))
                PacketSendUtility.SendPacket(opponent, new SM_DELETE(hiddenDuelist));
        }
    }

    /// <summary>Lets the given player lose the duel, ending it.</summary>
    public void LoseDuel(Player loser)
    {
        int? opponentId = GetOpponentId(loser);
        if (opponentId == null) // not dueling
            return;
        OnDuelEnd(DuelResult.DUEL_LOST, loser, opponentId.Value); // Chain of Suffering must be ended before calling removeDuel
        Player winner = World.GetInstance().GetPlayer(opponentId.Value);
        if (winner != null)
            OnDuelEnd(DuelResult.DUEL_WON, winner, loser.GetObjectId()); // Chain of Suffering must be ended before calling removeDuel
        RemoveDuel(loser);
    }

    private void EndDebuffsByOpponent(Player player, int opponentId)
    {
        foreach (var effect in player.GetEffectController().GetAbnormalEffects())
        {
            if (effect.GetTargetSlot() == SkillTargetSlot.DEBUFF && effect.GetEffectorId() == opponentId)
                effect.EndEffect();
        }
    }

    private void CancelSummonedObjectAttacks(Player target, int summonerId)
    {
        target.GetKnownList().ForEachNpc(npc =>
        {
            if (npc.GetMaster().GetObjectId() == summonerId)
            {
                Skill castingSkill = npc.GetCastingSkill();
                if (castingSkill != null && target.Equals(castingSkill.GetFirstTarget()))
                    npc.GetController().CancelCurrentSkill(null);
            }
        });
    }

    private void CreateTask(Player requester, Player responder)
    {
        // Schedule for draw
        ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (IsDueling(requester, responder))
            {
                OnDuelEnd(DuelResult.DUEL_DRAW, requester, responder.GetObjectId());
                OnDuelEnd(DuelResult.DUEL_DRAW, responder, requester.GetObjectId());
                RemoveDuel(requester);
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMinutes(5)); // 5 minutes battle retail like

        drawTasks[requester.GetObjectId()] = task;
        drawTasks[responder.GetObjectId()] = task;
    }

    private void OnDuelEnd(DuelResult duelResult, Player player, int opponentId)
    {
        if (player.IsTargeting(opponentId))
            player.GetController().CancelCurrentSkill(null);
        EndDebuffsByOpponent(player, opponentId);
        CancelSummonedObjectAttacks(player, opponentId);
        foreach (var attacker in player.GetAggroList()
            .Select(ai => ai.GetAttacker())
            .Where(attacker => attacker.GetMaster().GetObjectId() == opponentId)
            .ToList())
            player.GetAggroList().Remove(attacker, false);
        PacketSendUtility.SendPacket(player, SM_DUEL.SM_DUEL_RESULT(duelResult, PlayerService.GetPlayerName(opponentId)));
    }

    public int? GetOpponentId(Player player)
    {
        return duels.TryGetValue(player.GetObjectId(), out int v) ? v : (int?)null;
    }

    /// <summary>true if player is dueling</summary>
    public bool IsDueling(Player player)
    {
        int? opponentId = GetOpponentId(player);
        return opponentId != null && duels.ContainsKey(opponentId.Value);
    }

    /// <summary>true if player is dueling given target</summary>
    public bool IsDueling(Player player, Player opponent)
    {
        int? opponentId = GetOpponentId(player);
        return opponentId != null && opponentId.Value == opponent.GetObjectId();
    }

    private void RegisterDuel(int requesterObjId, int responderObjId)
    {
        duels[requesterObjId] = responderObjId;
        duels[responderObjId] = requesterObjId;
    }

    private void RemoveDuel(Player player)
    {
        if (duels.TryRemove(player.GetObjectId(), out int opponentId))
        {
            duels.TryRemove(opponentId, out _);
            RemoveAndEndTask(player.GetObjectId());
            RemoveAndEndTask(opponentId);
        }
    }

    private void RemoveAndEndTask(int playerId)
    {
        if (drawTasks.TryRemove(playerId, out ScheduledTask task) && task != null)
            task.Cancel(false);
    }

    private static class SingletonHolder
    {
        internal static readonly DuelService instance = new DuelService();
    }

    // Java parity: anonymous RequestResponseHandler<Player> in onDuelRequest (denyRequest/acceptRequest overrides).
    private sealed class DuelRequestHandler : RequestResponseHandler<Player>
    {
        private readonly DuelService svc;

        public DuelRequestHandler(DuelService svc, Player requester) : base(requester)
        {
            this.svc = svc;
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            svc.RejectDuelRequest(requester, responder);
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            if (!svc.IsDueling(requester))
                svc.StartDuel(requester, responder);
        }
    }

    // Java parity: anonymous RequestResponseHandler<Player> in confirmDuelWith (acceptRequest override).
    private sealed class DuelWithdrawHandler : RequestResponseHandler<Player>
    {
        private readonly DuelService svc;

        public DuelWithdrawHandler(DuelService svc, Player targetPlayer) : base(targetPlayer)
        {
            this.svc = svc;
        }

        public override void AcceptRequest(Player targetPlayer, Player responder)
        {
            svc.CancelDuelRequest(responder, targetPlayer);
        }
    }
}

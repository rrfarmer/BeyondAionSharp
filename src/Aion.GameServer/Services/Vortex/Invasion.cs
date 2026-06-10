using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Services.Vortex;

/// <summary>Java parity: services/vortex/Invasion (Source) : DimensionalVortex&lt;VortexLocation&gt;. Invader/defender alliances; start/stopInvasion (kisk die, kick invaders, spawn states), addPlayer (alliance create at 2 participants), kickPlayer (alliance remove, teleport home), updateDefenders (anonymous RequestResponseHandler->nested DefenderResponseHandler), updateInvaders, updateAlliance. ConcurrentHashMap->ConcurrentDictionary; map put/remove/containsKey/values; Arrays.asList->array; inline LoggerFactory.warn. PlayerAlliance/Kisk/VortexLocation red-tolerated.</summary>
public class Invasion : DimensionalVortex<VortexLocation>
{
    private readonly ConcurrentDictionary<int, Player> invaders = new ConcurrentDictionary<int, Player>();
    private readonly ConcurrentDictionary<int, Player> defenders = new ConcurrentDictionary<int, Player>();
    private PlayerAlliance invAlliance, defAlliance;

    public Invasion(VortexLocation vortex)
        : base(vortex)
    {
    }

    protected override void StartInvasion()
    {
        GetVortexLocation().SetActiveVortex(this);
        Despawn();
        Spawn(VortexStateType.INVASION);
        InitRiftGenerator();
        UpdateAlliance();
    }

    protected override void StopInvasion()
    {
        GetVortexLocation().SetActiveVortex(null);
        foreach (Kisk kisk in new List<Kisk>(GetVortexLocation().GetInvadersKisks().Values))
        {
            kisk.GetController().Die();
        }
        foreach (Player invader in invaders.Values)
        {
            if (invader.IsOnline())
            {
                KickPlayer(invader, true);
            }
        }
        Despawn();
        Spawn(VortexStateType.PEACE);
    }

    public override void AddPlayer(Player player, bool isInvader)
    {
        ConcurrentDictionary<int, Player> participants = isInvader ? invaders : defenders;
        PlayerAlliance alliance = isInvader ? invAlliance : defAlliance;

        if (alliance != null && !alliance.IsDisbanded())
        {
            PlayerAllianceService.AddPlayer(alliance, player);
        }
        else if (participants.Count == 1)
        { // create alliance once two players participate in this invasion
            Player otherPlayer = participants.Values.First();
            foreach (Player p in new[] { player, otherPlayer })
            {
                if (p.IsInGroup())
                {
                    PlayerGroupService.RemovePlayer(p);
                }
                else if (p.IsInAlliance())
                {
                    PlayerAllianceService.RemovePlayer(p);
                }
            }

            if (isInvader)
                invAlliance = PlayerAllianceService.CreateAlliance(otherPlayer, player, TeamType.ALLIANCE_OFFENCE);
            else
                defAlliance = PlayerAllianceService.CreateAlliance(otherPlayer, player, TeamType.ALLIANCE_DEFENCE);
        }
        else if (participants.Count > 1)
        { // should never happen
            NullLoggerFactory.Instance.CreateLogger(nameof(Invasion)).LogWarning("Couldn't add " + player + " to " + (isInvader ? "invaders" : "defenders")
                + " (alliance not initialized). Current participants: " + participants.Count);
            return;
        }
        participants[player.GetObjectId()] = player;
    }

    public override void KickPlayer(Player player, bool isInvader)
    {
        ConcurrentDictionary<int, Player> participants = isInvader ? invaders : defenders;
        PlayerAlliance alliance = isInvader ? invAlliance : defAlliance;

        participants.TryRemove(player.GetObjectId(), out _);

        if (alliance != null && alliance.HasMember(player.GetObjectId()))
        {
            if (player.IsOnline())
            {
                PacketSendUtility.SendPacket(player, new SM_SYSTEM_MESSAGE(isInvader ? 1401452 : 1401476));
            }
            PlayerAllianceService.RemovePlayer(player);
            if (alliance.IsDisbanded())
            {
                if (isInvader)
                {
                    invAlliance = null;
                }
                else
                {
                    defAlliance = null;
                }
            }
        }

        if (isInvader && player.IsOnline() && player.GetWorldId() == GetVortexLocation().GetInvasionWorldId())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INVADE_DIRECT_PORTAL_OUT_COMPULSION());
            TeleportService.TeleportTo(player, GetVortexLocation().GetHomePoint());
        }

        GetVortexLocation().GetVortexController().GetPassedPlayers().Remove(player.GetObjectId());
        GetVortexLocation().GetVortexController().SyncPassed(true);
    }

    public override void UpdateDefenders(Player defender)
    {
        if (defenders.ContainsKey(defender.GetObjectId()))
        {
            return;
        }

        if (defAlliance == null || !defAlliance.IsFull())
        {
            RequestResponseHandler<Player> responseHandler = new DefenderResponseHandler(this, defender);

            bool requested = defender.GetResponseRequester().PutRequest(904306, responseHandler);
            if (requested)
            {
                PacketSendUtility.SendPacket(defender, new SM_QUESTION_WINDOW(904306, 0, 0));
            }
        }
    }

    public override void UpdateInvaders(Player invader)
    {
        if (invaders.ContainsKey(invader.GetObjectId()))
        {
            return;
        }

        AddPlayer(invader, true);
    }

    private void UpdateAlliance()
    {
        foreach (Player player in GetVortexLocation().GetPlayers().Values)
        {
            if (player.GetRace().Equals(GetVortexLocation().GetDefendersRace()))
            {
                UpdateDefenders(player);
            }
        }
    }

    public override Dictionary<int, Player> GetInvaders()
    {
        return new Dictionary<int, Player>(invaders);
    }

    public override Dictionary<int, Player> GetDefenders()
    {
        return new Dictionary<int, Player>(defenders);
    }

    private sealed class DefenderResponseHandler : RequestResponseHandler<Player>
    {
        private readonly Invasion outer;

        public DefenderResponseHandler(Invasion outer, Player defender)
            : base(defender)
        {
            this.outer = outer;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            if (responder.IsInGroup())
            {
                PlayerGroupService.RemovePlayer(responder);
            }
            else if (responder.IsInAlliance())
            {
                PlayerAllianceService.RemovePlayer(responder);
            }

            if (outer.defAlliance == null || !outer.defAlliance.IsFull())
            {
                outer.AddPlayer(responder, false);
            }
        }
    }
}

using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/ArtifactSiege (SoulKeeper) extends Siege&lt;ArtifactLocation&gt;. Endless artifact siege: onSiegeStart (boss init, initial delay, balaur assault), onSiegeFinish (despawn, onCapture if boss killed, respawn peace, persist, restart), onCapture (winner race/legion + system messages). getWinnerLegionId Integer->int? (??0); keySet().iterator().next()->Keys.First(); forEachPlayer lambda; AP control no-op. ArtifactLocation/Legion/SM_ red-tolerated.</summary>
public class ArtifactSiege : Siege<ArtifactLocation>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ArtifactSiege));

    public ArtifactSiege(ArtifactLocation siegeLocation)
        : base(siegeLocation)
    {
    }

    protected override void OnSiegeStart()
    {
        InitSiegeBoss();
        GetSiegeLocation().SetInitialDelay(GetStartTime());
        // Check for Balaur Assault
        if (SiegeConfig.BALAUR_AUTO_ASSAULT)
            BalaurAssaultService.GetInstance().OnSiegeStart(this);
    }

    protected override void OnSiegeFinish()
    {
        // despawn npcs
        DespawnNpcs(GetSiegeLocationId());

        // for artifact should be always true
        if (IsBossKilled())
            OnCapture();
        else
            log.LogError("Artifact siege (artifactId:" + GetSiegeLocationId() + ") ended without killing a boss.");

        // add new spawns
        SpawnNpcs(GetSiegeLocationId(), GetSiegeLocation().GetRace(), SiegeModType.PEACE);

        // Store siege results in DB
        SiegeDAO.UpdateSiegeLocation(GetSiegeLocation());

        BroadcastUpdate(GetSiegeLocation());
        StartSiege(GetSiegeLocationId());
    }

    protected void OnCapture()
    {
        // Update winner counter
        SiegeRaceCounter wRaceCounter = GetWinnerRaceCounter();
        GetSiegeLocation().SetRace(wRaceCounter.GetSiegeRace());

        // Update legion
        int? wLegionId = wRaceCounter.GetWinnerLegionId();
        GetSiegeLocation().SetLegionId(wLegionId ?? 0);

        // misc stuff to send player system message
        if (GetSiegeLocation().GetRace() == SiegeRace.BALAUR)
        {
            PacketSendUtility.BroadcastToWorld(
                SM_SYSTEM_MESSAGE.STR_GUILD_EVENT_LOSE_ARTIFACT(GetSiegeLocation().GetL10n(), GetSiegeLocation().GetRace().GetL10n()));
        }
        else
        {
            // Prepare packet data
            string wPlayerName = "";
            Race wRace = wRaceCounter.GetSiegeRace() == SiegeRace.ELYOS ? Race.ELYOS : Race.ASMODIANS;
            Legion wLegion = wLegionId != null ? LegionService.GetInstance().GetLegion(wLegionId.Value) : null;
            if (wRaceCounter.GetPlayerDamageCounter().Count != 0)
            {
                int wPlayerId = wRaceCounter.GetPlayerDamageCounter().Keys.First();
                wPlayerName = PlayerService.GetPlayerName(wPlayerId);
            }
            string winnerName = wLegion != null ? wLegion.GetName() : wPlayerName;

            // prepare packets, we can use single packet instance
            AionServerPacket wRacePacket = SM_SYSTEM_MESSAGE.STR_GUILD_EVENT_WIN_ARTIFACT(wRace.GetL10n(), winnerName,
                GetSiegeLocation().GetL10n());
            AionServerPacket lRacePacket = SM_SYSTEM_MESSAGE.STR_GUILD_EVENT_LOSE_ARTIFACT(GetSiegeLocation().GetL10n(), wRace.GetL10n());

            // send update to players
            World.GetInstance().ForEachPlayer(p => PacketSendUtility.SendPacket(p, p.GetRace().Equals(wRace) ? wRacePacket : lRacePacket));
        }
    }

    public override bool IsEndless()
    {
        return true;
    }

    public override void OnAbyssPointsAdded(Player player, int abysPoints)
    {
        // No need to control AP
    }
}

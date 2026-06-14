using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/LegionDominionService (Yeats). Weekly territory-control calculation. TreeMap→SortedDictionary (sorted by key); new Timestamp(currentTimeMillis())→DateTimeOffset.FromUnixTimeMilliseconds(UtcNow…); ZonedDateTime→DateTimeOffset (getDayOfWeek→DayOfWeek, getHour→Hour); Map.get→GetValueOrDefault/indexer, values()→.Values, size()→Count; schedule(...,ms)→Schedule(TimeSpan.FromMilliseconds). DAO/Legion/SM_*/RiftService red-tolerated.</summary>
public class LegionDominionService
{
    private static readonly LegionDominionService instance = new LegionDominionService();

    private readonly SortedDictionary<int, LegionDominionLocation> legionDominionLocations = new SortedDictionary<int, LegionDominionLocation>();

    public static LegionDominionService GetInstance()
    {
        return instance;
    }

    public void InitLocations()
    {
        foreach (LegionDominionLocationTemplate temp in DataManager.LEGION_DOMINION_DATA.GetLocationTemplates())
        {
            legionDominionLocations[temp.GetId()] = new LegionDominionLocation(temp);
        }
        LegionDominionDAO.LoadOrCreateLegionDominionLocations(legionDominionLocations);
        foreach (LegionDominionLocation loc in legionDominionLocations.Values)
        {
            loc.SetParticipantInfo(LegionDominionDAO.LoadParticipants(loc));
        }
    }

    public ICollection<LegionDominionLocation> GetLegionDominions()
    {
        return legionDominionLocations.Values;
    }

    public LegionDominionLocation GetLegionDominionLoc(int locId)
    {
        return legionDominionLocations.GetValueOrDefault(locId);
    }

    public bool Join(int legionId, int locId)
    {
        return legionDominionLocations.GetValueOrDefault(locId).Join(legionId);
    }

    public void OnFinishInstance(Legion legion, int points, long time)
    {
        if (legion != null)
        {
            LegionDominionLocation loc = GetLegionDominionLoc(legion.GetCurrentLegionDominion());
            if (loc != null)
            {
                LegionDominionParticipantInfo info = loc.GetParticipantInfo(legion.GetLegionId());
                if (info != null && info.GetPoints() < points)
                {
                    info.SetDate(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                    info.SetPoints(points);
                    info.SetTime((int)(time / 1000));
                    LegionDominionDAO.UpdateInfo(info);
                    loc.UpdateRanking();
                }
            }
        }
    }

    public void StartWeeklyCalculation()
    {
        foreach (LegionDominionLocation loc in legionDominionLocations.Values)
        {
            // determine winner
            List<LegionDominionParticipantInfo> legionRanking = loc.GetLegionRanking(true);
            int newOccupyingLegionId = 0;

            // reset current occupying legion
            int previousOccupyingLegionId = loc.GetLegionId();
            if (previousOccupyingLegionId != 0)
            {
                Legion legion = LegionService.GetInstance().GetLegion(previousOccupyingLegionId);
                UpdateLegionOccupation(legion, loc, false);
            }

            // find winner of stonespear reach challenge
            LegionDominionParticipantInfo winner = legionRanking.Count == 0 ? null : legionRanking[0];
            if (winner != null)
            {
                Legion winningLegion = LegionService.GetInstance().GetLegion(winner.GetLegionId());
                if (winningLegion != null)
                {
                    if (!winningLegion.IsDisbanding())
                    {
                        newOccupyingLegionId = winningLegion.GetLegionId();
                    }
                    else
                    {
                        NullLoggerFactory.Instance.CreateLogger(nameof(LegionDominionService)).LogWarning(
                            "[Legion dominion] Skipped occupy of location {LocId} for legion [id={Id}, name={Name}] due to disbanding", loc.GetLocationId(),
                            winningLegion.GetLegionId(), winningLegion.GetName());
                    }
                }
            }

            loc.SetLegionId(newOccupyingLegionId);
            loc.SetOccupiedDate(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

            // update all participated legions & store them to db
            foreach (LegionDominionParticipantInfo info in loc.GetParticipantInfo().Values)
            {
                // skip previous legion since its already updated and didnt reoccupy it
                if (info.GetLegionId() != previousOccupyingLegionId || info.GetLegionId() == newOccupyingLegionId)
                {
                    Legion legion = LegionService.GetInstance().GetLegion(info.GetLegionId());
                    if (legion != null)
                    {
                        int occupiedId = 0;
                        if (winner != null && legion.GetLegionId() == newOccupyingLegionId)
                            occupiedId = loc.GetLocationId();
                        UpdateLegionOccupation(legion, loc, occupiedId > 0);
                    }
                }
                LegionDominionDAO.Delete(info);
            }

            Dictionary<int, List<LegionDominionReward>> dominionRewards = loc.GetRewards();
            for (int i = 0; i < legionRanking.Count; i++)
            {
                if (i >= dominionRewards.Count)
                    break;
                LegionDominionParticipantInfo participantInfo = legionRanking[i];
                Legion legion = LegionService.GetInstance().GetLegion(participantInfo.GetLegionId());
                if (legion == null || legion.IsDisbanding())
                    continue;
                List<LegionDominionReward> legionRewards = dominionRewards[i + 1];
                if (legionRewards.Count != 0)
                {
                    string playerName = legion.GetBrigadeGeneral().GetName();
                    foreach (LegionDominionReward reward in legionRewards)
                    {
                        // TODO send proper system (most likely $$GD_REWARD_MAIL) mail
                        SystemMailService.SendMail("Legion Dominion", playerName, "Reward Mail", "", reward.GetItemId(), reward.GetCount(),
                            0, LetterType.NORMAL);
                    }
                }
            }
            // reset locations participant info and update this location
            loc.Reset();
            LegionDominionDAO.UpdateLegionDominionLocation(loc);
        }
        PacketSendUtility.BroadcastToWorld(new SM_LEGION_DOMINION_LOC_INFO());
    }

    private void UpdateLegionOccupation(Legion legion, LegionDominionLocation location, bool shouldOccupy)
    {
        if (legion == null)
            return;
        legion.SetOccupiedLegionDominion(shouldOccupy ? location.GetLocationId() : 0);
        legion.SetLastLegionDominion(location.GetLocationId());
        legion.SetCurrentLegionDominion(0);
        LegionDAO.StoreLegion(legion);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_DOMINION_RANK(location, legion));
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_INFO(legion));
    }

    public bool IsInCalculationTime()
    {
        DateTimeOffset now = ServerTime.Now();
        return now.DayOfWeek == DayOfWeek.Wednesday && now.Hour >= 8 && now.Hour <= 10;
    }

    public bool OpenInvasionRift(int territoryId)
    {
        LegionDominionLocation location = GetLegionDominionLoc(territoryId);
        if (location == null || location.GetInvasionRift() == null)
            return false;
        LegionDominionInvasionRift invasionRift = location.GetInvasionRift();
        if (IsInCalculationTime())
            return false;
        if (RiftService.GetInstance().IsRiftOpened(invasionRift.GetRiftId()))
            return false;
        bool openedSuccessfully = RiftService.GetInstance().OpenRifts(invasionRift.GetRiftId(), false).Succeeded;
        if (openedSuccessfully)
        {
            if (location.GetRace() == Race.ELYOS)
            {
                PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_LIGHT_SIDE_LEGION_DIRECT_PORTAL_OPEN());
            }
            else
            {
                PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_DARK_SIDE_LEGION_DIRECT_PORTAL_OPEN());
            }
            // Schedule rift close
            ThreadPoolManager.GetInstance().Schedule(ct => { RiftService.GetInstance().CloseRifts(invasionRift.GetRiftId()); return System.Threading.Tasks.ValueTask.CompletedTask; },
                System.TimeSpan.FromMilliseconds(RiftService.GetInstance().GetDuration() * 3540 * 1000));
        }
        return openedSuccessfully;
    }
}

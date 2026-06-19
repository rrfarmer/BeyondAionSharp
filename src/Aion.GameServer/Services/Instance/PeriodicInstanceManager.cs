using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using static Aion.GameServer.Configs.Main.AutoGroupConfig;
using Quartz;

namespace Aion.GameServer.Services.Instance;

/// <summary>Java parity: services/instance/PeriodicInstanceManager (ViAl, Sykra, Estrayl). Singleton; schedules periodic instance registrations (dredgion/kamar/ophidan/iron-wall/idgel) via CronService; openRegistration/closeRegistration (synchronized→lock), broadcastRegistrationUpdate (level-range filter), checkAndSendOpenRegistrations, handleRequest. using static AutoGroupConfig; HashSet/HashMap→HashSet/Dictionary; Future→ScheduledTask (isDone→IsDone, cancel→Cancel); schedule(lambda, cronExpr) / schedule(lambda, period*60000)→async delegate; forEachPlayer. CronExpression/AutoGroupType/SM_AUTO_GROUP red-tolerated.</summary>
public class PeriodicInstanceManager
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PeriodicInstanceManager));
    private static readonly PeriodicInstanceManager INSTANCE = new PeriodicInstanceManager();
    private readonly HashSet<int> openedRegistrations = new HashSet<int>();
    private readonly Dictionary<int, ScheduledTask> registrationCloseTasksByMaskId = new Dictionary<int, ScheduledTask>();

    public static PeriodicInstanceManager GetInstance()
    {
        return INSTANCE;
    }

    private PeriodicInstanceManager()
    {
        if (AutoGroupConfig.AUTO_GROUP_ENABLE)
        {
            ScheduleRegistration(DREDGION_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDAB1_DREADGION(), 1, DREDGION_REGISTRATION_PERIOD);
            ScheduleRegistration(DREDGION_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDDREADGION_02(), 2, DREDGION_REGISTRATION_PERIOD);
            ScheduleRegistration(DREDGION_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDDREADGION_03(), 3, DREDGION_REGISTRATION_PERIOD);
            ScheduleRegistration(KAMAR_BATTLEFIELD_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDKamar(), 107, KAMAR_BATTLEFIELD_REGISTRATION_PERIOD);
            ScheduleRegistration(ENGULFED_OPHIDAN_BRIDGE_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Under_01_War(), 108,
                ENGULFED_OPHIDAN_BRIDGE_REGISTRATION_PERIOD);
            ScheduleRegistration(IRON_WALL_WARFRONT_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDF5_TD_war(), 109,
                IRON_WALL_WARFRONT_REGISTRATION_PERIOD);
            ScheduleRegistration(IDGEL_DOME_TIMES, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_OPEN_IDLDF5_Fortress_Re(), 111, IDGEL_DOME_REGISTRATION_PERIOD);
        }
    }

    private void ScheduleRegistration(CronExpression[] startExpressions, SM_SYSTEM_MESSAGE openingMsg, int maskId, long registrationPeriod)
    {
        foreach (CronExpression startExpression in startExpressions)
        {
            CronService.GetInstance().Schedule(() => OpenRegistration(openingMsg, maskId, registrationPeriod), startExpression);
            log.LogInformation("Scheduled registration opening for {Type} based on cron expression: {Cron}", AutoGroupTypeExtensions.GetAGTByMaskId(maskId), startExpression);
            log.LogInformation("Scheduled " + AutoGroupTypeExtensions.GetAGTByMaskId(maskId) + ": based on cron expression: " + startExpression + " Duration: "
                + registrationPeriod + " in minutes");
        }
    }

    public bool OpenRegistration(SM_SYSTEM_MESSAGE openingMsg, int maskId, long registrationPeriod)
    {
        lock (this)
        {
            if (openedRegistrations.Contains(maskId))
                return false;

            openedRegistrations.Add(maskId);
            BroadcastRegistrationUpdate(openingMsg, maskId, false);

            registrationCloseTasksByMaskId[maskId] = ThreadPoolManager.GetInstance().Schedule(ct => { CloseRegistration(maskId); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(registrationPeriod * 60000));
            log.LogInformation("Scheduled registration closing for {Type} in {Period} minutes", AutoGroupTypeExtensions.GetAGTByMaskId(maskId), registrationPeriod);
            return true;
        }
    }

    public bool CloseRegistration(int maskId)
    {
        lock (this)
        {
            if (!openedRegistrations.Contains(maskId))
                return false;
            openedRegistrations.Remove(maskId);
            BroadcastRegistrationUpdate(null, maskId, true);
            AutoGroupService.GetInstance().StopRegistrationsByMaskId(maskId);

            if (registrationCloseTasksByMaskId.ContainsKey(maskId))
            {
                ScheduledTask closeTask = registrationCloseTasksByMaskId[maskId];
                registrationCloseTasksByMaskId.Remove(maskId);
                if (closeTask != null && !closeTask.IsDone())
                    closeTask.Cancel(false);
            }
            return true;
        }
    }

    private void BroadcastRegistrationUpdate(SM_SYSTEM_MESSAGE msg, int maskId, bool isClosed)
    {
        AutoGroupType? typeN = AutoGroupTypeExtensions.GetAGTByMaskId(maskId); // Java parity: null when not found (.Value would throw)
        if (typeN != null)
        {
            AutoGroupType type = typeN.Value;
            World.World.GetInstance().ForEachPlayer(player =>
            {
                if (IsInLvlRange(player.GetLevel(), type.GetTemplate().GetMinLvl(), type.GetTemplate().GetMaxLvl()))
                {
                    PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId, SM_AUTO_GROUP.WND_ENTRY_ICON, isClosed));
                    if (msg != null)
                        PacketSendUtility.SendPacket(player, msg);
                }
            });
        }
    }

    public void CheckAndSendOpenRegistrations(int objectId)
    {
        Player player = World.World.GetInstance().GetPlayer(objectId);
        if (player != null)
            CheckAndSendOpenRegistrations(player);
    }

    public void CheckAndSendOpenRegistrations(Player player)
    {
        foreach (int maskId in openedRegistrations)
        {
            AutoGroupType? agtN = AutoGroupTypeExtensions.GetAGTByMaskId(maskId); // Java parity: null when not found (.Value would throw)
            if (agtN != null)
            {
                AutoGroupType agt = agtN.Value;
                AutoGroup data = agt.GetTemplate();
                if (IsInLvlRange(player.GetLevel(), data.GetMinLvl(), data.GetMaxLvl())
                    && !player.GetPortalCooldownList().IsPortalUseDisabled(data.GetInstanceMapId()))
                    PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId, SM_AUTO_GROUP.WND_ENTRY_ICON, false));
            }
        }
    }

    public bool IsRegistrationOpen(int maskId)
    {
        return openedRegistrations.Contains(maskId);
    }

    private bool IsInLvlRange(int playerLvl, int minLvl, int maxLvl)
    {
        return playerLvl >= minLvl && playerLvl <= maxLvl;
    }

    public void HandleRequest(Player player, int maskId)
    {
        if (openedRegistrations.Contains(maskId))
        {
            AutoGroupType? typeN = AutoGroupTypeExtensions.GetAGTByMaskId(maskId); // Java parity: null when not found (.Value would throw)
            if (typeN != null)
            {
                AutoGroupType type = typeN.Value;
                AutoGroup template = type.GetTemplate();
                if (IsInLvlRange(player.GetLevel(), template.GetMinLvl(), template.GetMaxLvl()))
                    PacketSendUtility.SendPacket(player, new SM_AUTO_GROUP(maskId));
            }
        }
    }
}

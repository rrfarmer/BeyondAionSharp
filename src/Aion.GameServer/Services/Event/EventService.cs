using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Globaldrops;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Cron;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using ForceType = Aion.GameServer.Skillengine.Model.Effect.ForceType;

namespace Aion.GameServer.Services.Event;

/// <summary>Java parity: services/event/EventService (Rolandas, Neon). Singleton; volatile active-events/drop-rules/quests/force-types/theme state; start/stop (CronService 5-min check), checkActiveEvents (diff old vs new via SetEquals, start/stop events, theme update), event hooks (login/team/map/pve/pvp-kill), collectActiveEvents/collectQuestIds/collectDropRules, getActiveEventConfigProperties (nullsFirst by startDate). Quartz JobDetail red-tolerated; Collections.empty*->new; TemporaryPlayerTeam<? extends TeamMember<Player>>-><TeamMember<Player>>; streams->LINQ; Set.equals->SetEquals; Objects::nonNull->!=null; ServerTime.now->DateTimeOffset/.DateTime; Properties red-tolerated. EventTemplate/Event/DAO red-tolerated.</summary>
public class EventService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(EventService));

    private volatile JobDetail checkTask = null;
    private volatile HashSet<Event> activeEvents = new HashSet<Event>();
    private volatile List<GlobalRule> activeEventDropRules = new List<GlobalRule>();
    private volatile HashSet<int> activeEventQuests = new HashSet<int>();
    private volatile HashSet<ForceType> effectForceTypes = new HashSet<ForceType>();
    private EventTheme eventTheme = EventTheme.NONE;

    private EventService()
    {
    }

    public bool Start()
    {
        if (checkTask != null)
            return false;

        ValidateConfiguredEventNames();
        CheckActiveEvents();
        checkTask = CronService.GetInstance().Schedule(OnTimeChanged, "0 0/5 * ? * *");
        return true;
    }

    public void Stop()
    {
        if (checkTask != null)
        {
            CronService.GetInstance().Cancel(checkTask);
            checkTask = null;
            HashSet<Event> oldActiveEvents = activeEvents;
            activeEvents = new HashSet<Event>();
            activeEventQuests = new HashSet<int>();
            activeEventDropRules = new List<GlobalRule>();
            UpdateEventTheme();
            foreach (Event ev in oldActiveEvents) // iterate after emptying activeEvents to ensure correct handling in stop()
                ev.Stop();
        }
    }

    private void ValidateConfiguredEventNames()
    {
        if (!IsAllEvents(EventsConfig.DISABLED_EVENTS))
        {
            HashSet<string> eventNames = DataManager.EVENT_DATA.GetEvents().Select(et => et.GetName()).ToHashSet();
            foreach (string eventName in EventsConfig.DISABLED_EVENTS)
            {
                if (!eventNames.Contains(eventName))
                    log.LogWarning("Unknown event \"" + eventName + "\" configured as disabled");
            }
        }
    }

    private void CheckActiveEvents()
    {
        HashSet<Event> oldActiveEvents = activeEvents;
        HashSet<Event> newActiveEvents = CollectActiveEvents();
        if (!oldActiveEvents.SetEquals(newActiveEvents))
        {
            activeEvents = newActiveEvents;
            activeEventQuests = CollectQuestIds(activeEvents);
            activeEventDropRules = CollectDropRules(activeEvents);
            UpdateEventTheme();
            StartOrStopEvents(oldActiveEvents, activeEvents);
            effectForceTypes = activeEvents.Select(e => e.GetEffectForceType()).Where(f => f != null).ToHashSet();
        }
    }

    public bool IsInactiveEventForceType(ForceType forceType)
    {
        return Event.IsEventEffectForceType(forceType) && !effectForceTypes.Contains(forceType);
    }

    private void OnTimeChanged()
    {
        CheckActiveEvents();
        if (activeEvents.Count != 0)
        {
            DateTimeOffset now = ServerTime.Now();
            foreach (Event ev in activeEvents)
                ev.OnTimeChanged(now);
        }
    }

    public void OnPlayerLogin(Player player)
    {
        foreach (Event ev in activeEvents)
            ev.OnPlayerLogin(player);
    }

    public void OnEnteredTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        foreach (Event ev in activeEvents)
            ev.OnEnteredTeam(player, team);
    }

    public void OnLeftTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        foreach (Event ev in activeEvents)
            ev.OnLeftTeam(player, team);
    }

    public void OnEnterMap(Player player)
    {
        foreach (Event ev in activeEvents)
            ev.OnEnterMap(player);
    }

    public void OnPveKill(Player killer, Npc victim)
    {
        foreach (Event ev in activeEvents)
            ev.OnPveKill(killer, victim);
    }

    public void OnPvpKill(Player killer, Player victim)
    {
        foreach (Event ev in activeEvents)
            ev.OnPvpKill(killer, victim);
    }

    private bool IsAllEvents(HashSet<string> list)
    {
        return list.Count == 1 && "*".Equals(list.First());
    }

    private HashSet<Event> CollectActiveEvents()
    {
        if (IsAllEvents(EventsConfig.DISABLED_EVENTS))
            return new HashSet<Event>();
        DateTime now = ServerTime.Now().DateTime;
        return DataManager.EVENT_DATA.GetEvents()
            .Where(et => !EventsConfig.DISABLED_EVENTS.Contains(et.GetName()) && et.IsInEventPeriod(now))
            .Select(et => FindOrCreateEvent(et))
            .ToHashSet();
    }

    private Event FindOrCreateEvent(EventTemplate et)
    {
        return activeEvents.Where(ev => et.Equals(ev.GetEventTemplate())).FirstOrDefault() ?? new Event(et);
    }

    private void StartOrStopEvents(HashSet<Event> oldActiveEvents, HashSet<Event> newActiveEvents)
    {
        foreach (Event oldActiveEvent in oldActiveEvents)
        {
            if (!newActiveEvents.Contains(oldActiveEvent))
                oldActiveEvent.Stop();
        }
        if (newActiveEvents.Count != 0)
        {
            bool cleanedOldBuffData = false;
            foreach (Event newActiveEvent in newActiveEvents)
            {
                if (!oldActiveEvents.Contains(newActiveEvent))
                {
                    if (!cleanedOldBuffData)
                    {
                        cleanedOldBuffData = true;
                        EventDAO.DeleteOldBuffData();
                    }
                    newActiveEvent.Start();
                }
            }
        }
    }

    private HashSet<int> CollectQuestIds(HashSet<Event> events)
    {
        HashSet<int> questIds = new HashSet<int>();
        foreach (Event ev in events)
        {
            questIds.UnionWith(ev.GetEventTemplate().GetStartableQuests());
            questIds.UnionWith(ev.GetEventTemplate().GetMaintainableQuests());
        }
        return questIds;
    }

    private List<GlobalRule> CollectDropRules(HashSet<Event> events)
    {
        return events.Where(e => e.GetEventTemplate().GetEventDropRules() != null)
            .SelectMany(e => e.GetEventTemplate().GetEventDropRules()).ToList();
    }

    public HashSet<Event> GetActiveEvents()
    {
        return activeEvents;
    }

    public bool IsEventActive(string eventName)
    {
        return activeEvents.Any(e => e.GetEventTemplate().GetName().Equals(eventName));
    }

    public bool IsActiveEventQuest(int questId)
    {
        return activeEventQuests.Contains(questId);
    }

    public Properties GetActiveEventConfigProperties()
    {
        Properties eventConfigProperties = new Properties();
        activeEvents
            .Select(e => e.GetEventTemplate())
            .Where(t => t.HasConfigProperties())
            .OrderBy(et => et.GetStartDate())
            .ToList()
            .ForEach(et =>
            {
                try
                {
                    eventConfigProperties.PutAll(et.LoadConfigProperties());
                }
                catch (Exception e)
                {
                    log.LogError(e, "Could not load config properties of event " + et.GetName());
                }
            });
        return eventConfigProperties;
    }

    private void UpdateEventTheme()
    {
        EventTheme oldEventTheme = eventTheme;
        EventTheme newEventTheme = EventTheme.NONE;
        foreach (Event ev in activeEvents)
        {
            if (ev.GetEventTemplate().GetTheme() != null)
            {
                newEventTheme = ev.GetEventTemplate().GetTheme();
                break;
            }
        }
        if (oldEventTheme != newEventTheme)
        {
            eventTheme = newEventTheme;
            PacketSendUtility.BroadcastToWorld(new SM_VERSION_CHECK(eventTheme)); // update city decoration (logged in players see changes after teleport)
        }
    }

    public EventTheme GetEventTheme()
    {
        return eventTheme;
    }

    public List<GlobalRule> GetActiveEventDropRules()
    {
        return activeEventDropRules;
    }

    private static class SingletonHolder
    {
        internal static readonly EventService instance = new EventService();
    }

    public static EventService GetInstance()
    {
        return SingletonHolder.instance;
    }
}

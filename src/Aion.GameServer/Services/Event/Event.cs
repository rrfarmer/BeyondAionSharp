using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Utils;
using Aion.GameServer.Configs;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Templates.Guides;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Event;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;
using ActionType = Aion.GameServer.Network.Aion.Serverpackets.SM_QUEST_ACTION.ActionType;
using ItemAddType = Aion.GameServer.Services.Item.ItemPacketService.ItemAddType;
using ItemUpdateType = Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType;
using ItemUpdatePredicate = Aion.GameServer.Services.Item.ItemService.ItemUpdatePredicate;
using ForceType = Aion.GameServer.Skillengine.Model.Effect.ForceType;

namespace Aion.GameServer.Services.Event;

/// <summary>Java parity: services/event/Event (Neon). One active event instance: spawns, inventory-drop task, surveys, buffs (EventBuffHandler), quest start/maintain, hooks. AtomicBoolean started; Future->ScheduledTask inventoryDropTask; List&lt;Runnable&gt; onEventEndTasks; Java int[] count capture-hack->C# mutable captured local; synchronized(this)->lock; scheduleAtFixedRate->async delegate; forEachPlayer/forEachObject lambdas; TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;-><TeamMember<Player>>; ServerTime.atDate(...).toLocalDateTime->.DateTime; isAfter->>; ActionType/ItemAddType/ItemUpdateType/ItemUpdatePredicate/ForceType aliases. EventTemplate/EventBuffHandler/Spawn/DAO red-tolerated.</summary>
public class Event
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Event));
    private const string EFFECT_FORCE_TYPE_PREFIX = "[EVENT] ";

    private readonly EventTemplate eventTemplate;
    private readonly AtomicBoolean started = new AtomicBoolean();
    private EventBuffHandler eventBuffHandler;
    private ScheduledTask inventoryDropTask;
    private List<Runnable> onEventEndTasks;

    public Event(EventTemplate eventTemplate)
    {
        this.eventTemplate = eventTemplate;
    }

    public EventTemplate GetEventTemplate()
    {
        return eventTemplate;
    }

    public static ForceType GetOrCreateEffectForceType(string identifier)
    {
        return ForceType.GetInstance(EFFECT_FORCE_TYPE_PREFIX + identifier);
    }

    public static bool IsEventEffectForceType(ForceType forceType)
    {
        return forceType != null && forceType.GetName().StartsWith(EFFECT_FORCE_TYPE_PREFIX);
    }

    public void Start()
    {
        if (!started.CompareAndSet(false, true))
            return;
        if (eventTemplate.HasConfigProperties())
            Config.Load();
        if (eventTemplate.GetSpawns() != null && eventTemplate.GetSpawns().Size() > 0)
        {
            foreach (SpawnMap map in eventTemplate.GetSpawns().GetTemplates())
            {
                byte difficultId = 0;
                WorldMap worldMap = World.World.GetInstance().GetWorldMap(map.GetMapId());
                foreach (Spawn spawn in map.GetSpawns())
                {
                    spawn.SetEventTemplate(eventTemplate);
                    if (spawn.IsCustom())
                        DespawnNonEventSpawns(spawn.GetNpcId(), worldMap);
                    if (difficultId == 0)
                        difficultId = spawn.GetDifficultId();
                }
                DataManager.SPAWNS_DATA.AddRegularSpawns(map);
                foreach (WorldMapInstance instance in worldMap)
                    SpawnEngine.SpawnEventSpawns(instance, difficultId, 0, eventTemplate);
            }
        }

        InventoryDrop inventoryDrop = eventTemplate.GetInventoryDrop();
        if (inventoryDropTask == null && inventoryDrop != null)
        {
            inventoryDropTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
            {
                World.World.GetInstance().ForEachPlayer(player =>
                {
                    if (player.GetLevel() >= inventoryDrop.GetStartLevel())
                        // TODO: check the exact type in retail
                        ItemService.AddItem(player, inventoryDrop.GetItemId(), inventoryDrop.GetCount(), true,
                            new ItemUpdatePredicate(ItemAddType.ITEM_COLLECT, ItemUpdateType.INC_CASH_ITEM));
                });
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(inventoryDrop.GetInterval() * 60000));
        }

        if (eventTemplate.GetSurveys() != null)
        {
            foreach (string survey in eventTemplate.GetSurveys())
            {
                GuideTemplate template = DataManager.GUIDE_HTML_DATA.GetTemplateByTitle(survey);
                if (template != null)
                    template.SetActivated(true);
            }
        }

        if (eventTemplate.GetBuffs() != null)
            eventBuffHandler = new EventBuffHandler(eventTemplate.GetName(), eventTemplate.GetBuffs());

        World.World.GetInstance().ForEachPlayer(player =>
        { // simulate login on event start
            OnPlayerLogin(player);
            OnEnterMap(player);
        });

        log.LogInformation("Started event: " + eventTemplate.GetName());
    }

    public void Stop()
    {
        started.Set(false);
        if (eventTemplate.HasConfigProperties())
        {
            Config.Load();
        }
        if (eventTemplate.GetSpawns() != null && eventTemplate.GetSpawns().Size() > 0)
        {
            TemporarySpawnEngine.Unregister(eventTemplate);
            int count = 0; // Java int[] capture-hack -> C# mutable captured local
            foreach (SpawnMap map in eventTemplate.GetSpawns().GetTemplates())
            {
                World.World.GetInstance().GetWorldMap(map.GetMapId()).ForEachObject(o =>
                {
                    SpawnTemplate spawn = o.GetSpawn();
                    if (spawn != null && eventTemplate.Equals(spawn.GetEventTemplate()))
                    {
                        o.GetController().DeleteIfAliveOrCancelRespawn();
                        count++;
                    }
                });
            }
            count += RespawnService.CancelEventRespawns(eventTemplate);
            DataManager.SPAWNS_DATA.RemoveEventSpawnObjects(eventTemplate);
            log.LogInformation("Removed " + count + " event spawns (" + eventTemplate.GetName() + ")");
        }

        lock (this)
        {
            if (onEventEndTasks != null)
            {
                foreach (Runnable task in onEventEndTasks)
                {
                    try
                    {
                        task.Run();
                    }
                    catch (Exception e)
                    {
                        log.LogError(e, "Could not execute task on end of event " + GetEventTemplate().GetName());
                    }
                }
                onEventEndTasks = null;
            }
        }

        if (inventoryDropTask != null)
        {
            inventoryDropTask.Cancel(false);
            inventoryDropTask = null;
        }

        if (eventTemplate.GetSurveys() != null)
        {
            foreach (string survey in eventTemplate.GetSurveys())
            {
                GuideTemplate template = DataManager.GUIDE_HTML_DATA.GetTemplateByTitle(survey);
                if (template != null)
                    template.SetActivated(false);
            }
        }

        if (eventBuffHandler != null)
        {
            eventBuffHandler.OnEventStop();
            eventBuffHandler = null;
        }

        log.LogInformation("Stopped event: " + eventTemplate.GetName());
    }

    public ForceType GetEffectForceType()
    {
        return eventBuffHandler == null ? null : eventBuffHandler.GetEffectForceType();
    }

    public void OnTimeChanged(DateTimeOffset now)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnTimeChanged(now);
    }

    public void OnPlayerLogin(Player player)
    {
        StartOrMaintainQuests(player);
        if (eventTemplate.GetLoginMessage() != null && !(eventTemplate.GetLoginMessage().Length == 0))
            PacketSendUtility.SendMessage(player, eventTemplate.GetLoginMessage());
    }

    public void OnEnteredTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnEnteredTeam(player, team);
    }

    public void OnLeftTeam(Player player, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnLeftTeam(player, team);
    }

    public void OnEnterMap(Player player)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnEnterMap(player);
    }

    public void OnPveKill(Player killer, Npc victim)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnPveKill(killer, victim);
    }

    public void OnPvpKill(Player killer, Player victim)
    {
        if (eventBuffHandler != null)
            eventBuffHandler.OnPvpKill(killer, victim);
    }

    private void StartOrMaintainQuests(Player player)
    {
        foreach (int startableQuestId in eventTemplate.GetStartableQuests())
        {
            if (IsAllowedToStartEventQuest(player, startableQuestId))
            {
                QuestState qs = player.GetQuestStateList().GetQuestState(startableQuestId);
                if (qs == null)
                {
                    qs = new QuestState(startableQuestId, QuestStatus.START);
                    player.GetQuestStateList().AddQuest(startableQuestId, qs);
                    PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.ADD, qs));
                }
                else if (qs.GetStatus() != QuestStatus.START && qs.GetCompleteCount() > 0 && eventTemplate.GetStartDate() != null)
                {
                    DateTime completeTime = ServerTime.AtDate(qs.GetLastCompleteTime()).DateTime;
                    if (eventTemplate.GetStartDate() > completeTime)
                    { // quest was last completed on a previous event, reset & restart it
                        ActionType actionType = qs.GetStatus() == QuestStatus.COMPLETE ? ActionType.ADD : ActionType.UPDATE;
                        qs.SetStatus(QuestStatus.START);
                        qs.SetQuestVar(0);
                        qs.SetCompleteCount(0);
                        qs.SetRewardGroup(null);
                        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(actionType, qs));
                    }
                }
            }
        }
        foreach (int maintainableQuestId in eventTemplate.GetMaintainableQuests())
        {
            QuestState qs = player.GetQuestStateList().GetQuestState(maintainableQuestId);
            if (qs != null && qs.GetCompleteCount() > 0 && IsAllowedToStartEventQuest(player, maintainableQuestId))
            {
                DateTime completeTime = ServerTime.AtDate(qs.GetLastCompleteTime()).DateTime;
                if (eventTemplate.GetStartDate() > completeTime)
                { // quest was last completed on a previous event, reset it
                    qs.SetCompleteCount(0);
                }
            }
        }
    }

    private bool IsAllowedToStartEventQuest(Player player, int questId)
    {
        QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
        if (template.GetCategory() != QuestCategory.EVENT)
            return false;
        if (!QuestService.CheckStartConditions(player, questId, false, 0, true, true, false))
            return false;
        return true;
    }

    private void DespawnNonEventSpawns(int npcId, WorldMap worldMap)
    {
        foreach (WorldMapInstance worldMapInstance in worldMap)
        {
            foreach (Npc npc in worldMapInstance.GetNpcs(npcId))
            {
                if (npc.GetSpawn() != null && !npc.GetSpawn().IsEventSpawn() && !npc.GetController().HasScheduledTask(TaskId.DECAY))
                {
                    if (npc.GetController().Delete() && !RespawnService.HasRespawnTask(npc))
                        AddOnEventEndTask(new RespawnService.RespawnTask(npc));
                }
            }
        }
    }

    public bool AddOnEventEndTask(Runnable task)
    {
        if (!started.Get())
            return false;
        lock (this)
        {
            if (onEventEndTasks == null)
                onEventEndTasks = new List<Runnable>();
            onEventEndTasks.Add(task);
            return true;
        }
    }
}

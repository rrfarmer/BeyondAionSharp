using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.Quest;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Tests.Ai;
using Engine = Aion.GameServer.QuestEngine.QuestEngine;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class WheresRaePortTests
{
    private const int Quest = 2002;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Value<T>(object obj, string name) => (T)obj.GetType().GetField(name, Fields)!.GetValue(obj)!;

    [Fact]
    public void EntryAndKillTransitionsMatchFiftyFourExactJavaCasesIncludingPackedState()
    {
        using var s = new Scope();
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(PvpExpPortTests.Root(), "parity-artifacts/golden/formulas/WheresRae.93d5fa15b.json")));
        Assert.Equal("93d5fa15b126a8ccf1fba99e55b8844a7ae7fe14", data.RootElement.GetProperty("sourceJavaCommit").GetString());
        foreach (var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            s.Player.GetQuestStateList().DeleteQuest(Quest);
            string status = row.GetProperty("status").GetString()!;
            QuestState? state = status == "null" ? null : new(Quest, Enum.Parse<QuestStatus>(status));
            if (state != null) { state.SetQuestVar(row.GetProperty("value").GetInt32()); s.Player.GetQuestStateList().AddQuest(Quest, state); }
            int map = row.GetProperty("world").GetInt32(); s.Map(map); s.Connection.Sent.Clear();
            var env = new QuestEnv(BareNpc(row.GetProperty("npc").GetInt32()), s.Player, Quest);
            bool actual = row.GetProperty("op").GetString() == "entry" ? s.Handler.OnEnterWorldEvent(env) : s.Handler.OnKillEvent(env);
            Assert.Equal(row.GetProperty("accepted").GetBoolean(), actual);
            Assert.Equal(row.GetProperty("after").GetInt32(), state?.GetQuestVars().GetQuestVars() ?? 0);
            Assert.Equal(row.GetProperty("updates").GetInt32(), s.Connection.Sent.OfType<SM_QUEST_ACTION>().Count());
            s.Map(220010000);
        }
    }

    [Fact]
    public void RegistrationRetainsBothReachabilityFixesAndAddsTheEnterWorldHook()
    {
        using var s = new Scope();
        foreach (int id in new[] {203519,203534,203553,700045,203516,203538,790002,205020})
            Assert.Contains(Quest, s.Engine.GetQuestNpc(id).GetOnTalkEvent());
        foreach (int id in new[] {210377,210378}) Assert.Contains(Quest, s.Engine.GetQuestNpc(id).GetOnKillEvent());
        s.State.SetQuestVar(12); s.Map(320010000);
        s.Engine.OnEnterWorld(s.Player); Assert.Equal(99, s.State.GetQuestVars().GetQuestVars());
        s.Map(220010000); s.Engine.OnEnterWorld(s.Player); Assert.Equal(12, s.State.GetQuestVars().GetQuestVars());
    }

    [Theory]
    [InlineData(2, DialogAction.SETPRO3, 3, true)] [InlineData(2, DialogAction.SETPRO4, 2, false)] [InlineData(2, DialogAction.SETPRO6, 2, false)]
    [InlineData(10, DialogAction.SETPRO3, 10, false)] [InlineData(10, DialogAction.SETPRO4, 11, true)] [InlineData(10, DialogAction.SETPRO6, 10, false)]
    [InlineData(13, DialogAction.SETPRO3, 13, false)] [InlineData(13, DialogAction.SETPRO4, 13, false)] [InlineData(13, DialogAction.SETPRO6, 14, true)]
    public void VerdandiRequiresTheExactDialogForEachStepAndClosesWithPageZero(int step, int action, int after, bool accepted)
    {
        using var s = new Scope(); s.State.SetQuestVar(step); var npc = s.H.Spawn(790002);
        Assert.Equal(accepted, s.Dialog(npc, action)); Assert.Equal(after, s.State.GetQuestVars().GetQuestVars());
        var packets = s.Connection.Sent.OfType<SM_DIALOG_WINDOW>().ToArray(); Assert.Equal(accepted ? 1 : 0, packets.Length);
        if (accepted) { Assert.Equal(0, Value<int>(packets[0], "dialogPageId")); Assert.Equal(0, Value<int>(packets[0], "questId")); }
    }

    [Theory]
    [InlineData(2,1693)] [InlineData(10,2034)] [InlineData(11,2375)] [InlineData(12,2462)] [InlineData(13,2716)]
    public void VerdandiStillOpensTheExistingDialogPages(int step, int page)
    {
        using var s = new Scope(); s.State.SetQuestVar(step);
        Assert.True(s.Dialog(BareNpc(790002), DialogAction.QUEST_SELECT));
        Assert.Equal(page, Value<int>(Assert.Single(s.Connection.Sent.OfType<SM_DIALOG_WINDOW>()), "dialogPageId"));
    }

    [Fact]
    public void AnExistingInstanceMarkerCannotCreateAnotherAtaxiarInstance()
    {
        using var s = new Scope(); s.State.SetQuestVar(99);
        Assert.False(s.Dialog(BareNpc(790002), DialogAction.SETPRO5)); Assert.Empty(s.Connection.Sent);
        Assert.Equal(99, s.State.GetQuestVars().GetQuestVars());
    }

    [Theory]
    [InlineData("waiting",14)] [InlineData("other-step",13)] [InlineData("reward",15)] [InlineData("complete",15)]
    public void RaeDespawnResetsOnlyAnActiveUnfinishedConversationAfterFiveMinutesAndOneSecond(string state, int expected)
    {
        using var s = new Scope(); s.State.SetQuestVar(14); var ribbit = s.H.Spawn(203538);
        Assert.True(s.Dialog(ribbit, DialogAction.USE_OBJECT)); Assert.Equal(15, s.State.GetQuestVarById(0));
        var rae = Assert.Single(s.Player.GetPosition().GetWorldMapInstance().GetNpcs(203553));
        Assert.Equal(0, Value<int>(Assert.Single(s.Connection.Sent.OfType<SM_DIALOG_WINDOW>()), "dialogPageId"));
        if (state == "other-step") s.State.SetQuestVar(13);
        if (state == "reward") s.State.SetStatus(QuestStatus.REWARD);
        if (state == "complete") s.State.SetStatus(QuestStatus.COMPLETE);
        s.H.Clock.Advance(TimeSpan.FromMilliseconds(300_000)); Assert.False(rae.IsSpawned());
        Assert.Equal(state == "other-step" ? 13 : 15, s.State.GetQuestVarById(0));
        s.H.Clock.Advance(TimeSpan.FromMilliseconds(999)); Assert.Equal(state == "other-step" ? 13 : 15, s.State.GetQuestVarById(0));
        s.H.Clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.Equal(expected, s.State.GetQuestVarById(0)); Assert.Empty(s.H.Clock.Faults);
    }

    [Fact]
    public void TalkingToRaeSetsRewardDeletesHerAndPreventsThePendingReset()
    {
        using var s = new Scope(); s.State.SetQuestVar(14);
        Assert.True(s.Dialog(s.H.Spawn(203538), DialogAction.USE_OBJECT));
        var rae = Assert.Single(s.Player.GetPosition().GetWorldMapInstance().GetNpcs(203553));
        Assert.True(s.Dialog(rae, DialogAction.SETPRO7)); Assert.Equal(QuestStatus.REWARD, s.State.GetStatus()); Assert.False(rae.IsSpawned());
        Assert.Equal(0, Value<int>(Assert.Single(s.Connection.Sent.OfType<SM_DIALOG_WINDOW>()), "dialogPageId"));
        s.H.Clock.Advance(TimeSpan.FromMilliseconds(301_000)); Assert.Equal(15, s.State.GetQuestVarById(0)); Assert.Equal(QuestStatus.REWARD, s.State.GetStatus());
        Assert.Empty(s.H.Clock.Faults);
    }

    [Theory]
    [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)] [InlineData(4195)]
    public void HagenStartsReturnFlightOnlyForTheExactWholeQuestValue99(int step)
    {
        using var s = new Scope(); s.State.SetQuestVar(step);
        Assert.False(s.Dialog(BareNpc(205020), DialogAction.QUEST_SELECT)); Assert.Empty(s.Connection.Sent);
    }

    [Fact]
    public void ReturnFlightChanges99To13WhenTheFlightPathEnds()
    {
        // Upstream 30612bc3d dropped the 40 s timer: the landing event carries the step and the teleport now.
        using var s = new Scope(); s.State.SetQuestVar(99);
        Assert.True(s.Dialog(BareNpc(205020), DialogAction.QUEST_SELECT)); Assert.Single(s.Connection.Sent.OfType<SM_EMOTION>());
        Assert.True(s.Player.IsUsingFlightTransporterOrWindstream());
        s.H.Clock.Advance(TimeSpan.FromMinutes(5)); Assert.Equal(99, s.State.GetQuestVars().GetQuestVars());
        s.Connection.Sent.Clear();
        Assert.False(s.Handler.OnFlightPathEndEvent(new QuestEnv(null, s.Player, Quest), 3001)); // another path's landing is ignored
        Assert.Equal(99, s.State.GetQuestVars().GetQuestVars());
        Assert.True(s.Handler.OnFlightPathEndEvent(new QuestEnv(null, s.Player, Quest), 4001));
        Assert.Equal(13, s.State.GetQuestVars().GetQuestVars());
        int update = s.Connection.Sent.FindIndex(p => p is SM_QUEST_ACTION);
        // Use a real same-map landing to exercise the callback without booting a second instance.
        int teleport = s.Connection.Sent.FindIndex(p => p is SM_CHANNEL_INFO);
        Assert.True(update >= 0 && teleport > update, string.Join(",", s.Connection.Sent.Select(p => p.GetType().Name)) + " faults=" + string.Join(";",s.H.Clock.Faults));
        Assert.Equal(940.15f, s.Player.GetX()); Assert.Equal(2295.64f, s.Player.GetY()); Assert.Equal(265.7f, s.Player.GetZ());
        Assert.Equal((byte)43, s.Player.GetHeading()); Assert.Empty(s.H.Clock.Faults);
    }

    [Fact]
    public void ProductionSpawnLoaderCarriesFiveMeterWalkRangeAndRetainsRibbitsPositionAndRespawn()
    {
        using var file = File.OpenRead(Path.Combine(PvpExpPortTests.Root(), "game-server/data/static_data/spawns/Npcs/220010000_Ishalgen.xml"));
        var spawns = (SpawnsData)new XmlSerializer(typeof(SpawnsData)).Deserialize(file)!; spawns.AfterUnmarshal(null);
        var group = Assert.Single(spawns.GetSpawnsForNpc(220010000,203538)); var spot = Assert.Single(group.GetSpawnTemplates());
        Assert.Equal(5, spot.GetRandomWalkRange()); Assert.Equal(295, group.GetRespawnTime());
        Assert.Equal(940.402f, spot.GetX()); Assert.Equal(2291.84f, spot.GetY()); Assert.Equal(265.465f, spot.GetZ()); Assert.Equal((byte)77, spot.GetHeading());
    }

    private static Npc BareNpc(int id)
    {
        var npc = (Npc)RuntimeHelpers.GetUninitializedObject(typeof(Npc));
        typeof(VisibleObject).GetField("_objectTemplate", Fields)!.SetValue(npc,new NpcTemplate {npcId=id}); return npc;
    }
    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        public List<AionServerPacket> Sent = new();
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }
    private sealed class Scope : IDisposable
    {
        public readonly BossAiHarness H = BossAiHarness.For(220010000).WithWorldSize(4096).Build();
        public readonly Engine Engine = Engine.GetInstance();
        public readonly Player Player;
        public readonly QuestState State = new(Quest,QuestStatus.START);
        public readonly _2002WheresRae Handler;
        public readonly Connection Connection;
        private readonly Aion.GameServer.World.WorldPosition originalPosition;
        private readonly FieldInfo source = typeof(DatabaseFactory).GetField("_dataSource",BindingFlags.Static|BindingFlags.NonPublic)!;
        private readonly object? oldSource;
        private readonly Dictionary<int,AbstractQuestHandler> handlers;
        private readonly AbstractQuestHandler? oldHandler;
        private readonly Dictionary<int,QuestNpc> npcs;
        private readonly Dictionary<int,QuestNpc?> oldNpcs;
        private readonly List<int> entered,completed;
        private readonly int[] oldEntered,oldCompleted;
        private readonly Dictionary<Race,List<int>> levels;
        private readonly Dictionary<Race,List<int>> oldLevels;
        public Scope()
        {
            oldSource=source.GetValue(null);source.SetValue(null,null);
            var element=XDocument.Load(Path.Combine(PvpExpPortTests.Root(),"game-server/data/static_data/quest_data/quest_data.xml")).Root!.Elements("quest").Single(q=>(int?)q.Attribute("id")==Quest);
            using var reader=new XElement("quests",new XElement(element)).CreateReader();var quests=(QuestsData)new XmlSerializer(typeof(QuestsData)).Deserialize(reader)!;quests.AfterUnmarshal(null!);
            typeof(StaticData).GetProperty(nameof(StaticData.Quests))!.SetValue(H.Data.StaticData,quests);
            handlers=Value<Dictionary<int,AbstractQuestHandler>>(Engine,"questHandlers");oldHandler=handlers.GetValueOrDefault(Quest);handlers.Remove(Quest);
            npcs=Value<Dictionary<int,QuestNpc>>(Engine,"questNpcs");oldNpcs=new[]{203519,203534,203553,700045,203516,203538,790002,205020,210377,210378}.ToDictionary(id=>id,id=>npcs.GetValueOrDefault(id));foreach(int id in oldNpcs.Keys)npcs.Remove(id);
            entered=Value<List<int>>(Engine,"questOnEnterWorld");oldEntered=entered.ToArray();entered.Clear();
            completed=Value<List<int>>(Engine,"questOnCompleted");oldCompleted=completed.ToArray();
            levels=Value<Dictionary<Race,List<int>>>(Engine,"questOnLevelUp");oldLevels=levels.ToDictionary(k=>k.Key,v=>v.Value);levels.Clear();foreach(var e in oldLevels)levels[e.Key]=e.Value.ToList();
            Handler=new _2002WheresRae();handlers[Quest]=Handler;Handler.Register();
            Player=H.SpawnPlayer(race:Race.ASMODIANS); originalPosition=Player.GetPosition(); Player.GetQuestStateList().AddQuest(Quest,State);
            Connection=new Connection();Connection.SetAccount(Player.GetAccount());Player.SetClientConnection(Connection);Assert.True(Connection.SetActivePlayer(Player));
            Player.SetMotions(new Aion.GameServer.Model.GameObjects.Players.Motion.MotionList(Player));Player.SetFlyController(new Aion.GameServer.Controllers.FlyController(Player));Connection.Sent.Clear();
        }
        public void Map(int id) => Player.SetPosition(id==220010000 ? originalPosition : new Aion.GameServer.World.WorldPosition(id));
        public bool Dialog(Npc npc,int action) {Connection.Sent.Clear();return Handler.OnDialogEvent(new QuestEnv(npc,Player,Quest,action));}
        public void Dispose()
        {
            if(oldHandler==null)handlers.Remove(Quest);else handlers[Quest]=oldHandler;
            foreach(var e in oldNpcs){if(e.Value==null)npcs.Remove(e.Key);else npcs[e.Key]=e.Value;}
            entered.Clear();entered.AddRange(oldEntered);completed.Clear();completed.AddRange(oldCompleted);levels.Clear();foreach(var e in oldLevels)levels[e.Key]=e.Value;
            source.SetValue(null,oldSource);H.Dispose();
        }
    }
}

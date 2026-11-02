using System.Reflection;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Handlers.PlayerCommands;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class DecomposeCommandPortTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OfflineOrDeadPlayersCannotStartTheCommand(bool dead)
    {
        using var fixture = new Fixture(online: dead);
        if (dead)
            typeof(CreatureLifeStats).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(fixture.Player.GetLifeStats(), 0);
        fixture.Execute("2");
        Assert.Equal(0, fixture.Action.Checks);
        Assert.False(fixture.Player.GetController().HasTask(TaskId.SKILL_USE));
    }

    [Fact]
    public void ItemUseIsNotifiedOnceBeforeTheBatchAndCompletionWaitsForTheCastingDelay()
    {
        using var fixture = new Fixture();
        var observer = new ItemUseProbe();
        fixture.Player.GetObserveController().AddObserver(observer);
        fixture.Execute("2");
        Assert.Equal(1, observer.Uses);
        Assert.Equal(0, fixture.Action.Acts);
        fixture.Advance(10);
        Assert.Equal(1, fixture.Action.Acts);
        Assert.Equal(0, fixture.Action.Completed);
        fixture.Advance(2200);
        Assert.Equal(2, fixture.Action.Acts);
        Assert.Equal(2, fixture.Action.Completed);
        Assert.Equal(1, observer.Uses);
        Assert.False(fixture.Player.GetController().HasTask(TaskId.SKILL_USE));
        Assert.Contains(fixture.Messages, message => message.Contains("Decomposing finished: Processed 2x"));
    }

    [Fact]
    public void ReplenishingTheStackDoesNotClampTheRequestedTotalToTheInitialInventoryCount()
    {
        using var fixture = new Fixture();
        fixture.Item.SetItemCount(1);
        fixture.Execute("3");
        fixture.Advance(1010);
        Assert.Equal(1, fixture.Action.Completed);
        fixture.Item.SetItemCount(2);
        fixture.Advance(2300);
        Assert.Equal(3, fixture.Action.Completed);
        Assert.Contains(fixture.Messages, message => message.Contains("Decomposing finished: Processed 3x"));
    }

    [Fact]
    public void LaterUseOfTheSameItemAbortsTheBatchAndExcludesTheInFlightItemFromItsCount()
    {
        using var fixture = new Fixture();
        fixture.Execute("5");
        fixture.Advance(10);
        fixture.Player.GetObserveController().NotifyItemuseObservers(fixture.Item);
        Assert.False(fixture.Player.GetController().HasTask(TaskId.SKILL_USE));
        Assert.Contains(fixture.Messages, message => message.Contains("Decomposing aborted: Processed 0x"));
        fixture.Advance(3000);
        Assert.Equal(1, fixture.Action.Acts);
    }

    [Fact]
    public void MovementAfterOneCompletedItemReportsOneAndRemovesTheObserverOnce()
    {
        using var fixture = new Fixture();
        fixture.Execute("5");
        fixture.Advance(1110);
        Assert.Equal(1, fixture.Action.Completed);
        fixture.Player.GetObserveController().NotifyMoveObservers();
        Assert.False(fixture.Player.GetController().HasTask(TaskId.SKILL_USE));
        Assert.Single(fixture.Messages, message => message.Contains("Decomposing aborted: Processed 1x"));
        fixture.Player.GetObserveController().NotifyItemuseObservers(fixture.Item);
        Assert.Single(fixture.Messages, message => message.Contains("Decomposing aborted:"));
    }

    [Fact]
    public void FailedPerItemValidationUsesTheNormalAbortPath()
    {
        using var fixture = new Fixture();
        fixture.Execute("5");
        fixture.Action.Allowed = false;
        fixture.Advance(10);
        Assert.Equal(0, fixture.Action.Acts);
        Assert.False(fixture.Player.GetController().HasTask(TaskId.SKILL_USE));
        fixture.Player.GetObserveController().NotifyItemuseObservers(fixture.Item);
        Assert.Single(fixture.Messages, message => message.Contains("Decomposing aborted: Processed 0x"));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData(null, 5)]
    public void ZeroCountAndDefaultAllFinishWithTheActualProcessedCount(string? count, int expected)
    {
        using var fixture = new Fixture();
        fixture.Execute(count);
        fixture.Advance(6000);
        Assert.Equal(expected, fixture.Action.Completed);
        Assert.Contains(fixture.Messages, message => message.Contains($"Decomposing finished: Processed {expected}x"));
    }

    private sealed class ItemUseProbe() : ActionObserver(ObserverType.ITEMUSE)
    {
        public int Uses;
        public override void Itemused(Item item) => Uses++;
    }

    /// <summary>Only the per-item reward action is replaced: its asynchronous completion keeps Java's casting-delay contract.</summary>
    private sealed class ActionProbe : DecomposeAction
    {
        public int Checks;
        public int Acts;
        public int Completed;
        public bool Allowed = true;
        public override bool CanAct(Player player, Item parentItem, Item targetItem, params object[] args)
        {
            Checks++;
            return Allowed;
        }
        public override void Act(Player player, Item parentItem, Item targetItem, params object[] args)
        {
            Acts++;
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                parentItem.SetItemCount(parentItem.GetItemCount() - 1);
                Completed++;
                return ValueTask.CompletedTask;
            }, TimeSpan.FromMilliseconds(parentItem.GetItemTemplate().GetCastingDelay()));
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly BossAiHarness harness = BossAiHarness.For().Build();
        private readonly Connection connection = new();
        public Player Player { get; }
        public Item Item { get; }
        public ActionProbe Action { get; } = new();
        public IEnumerable<string> Messages => connection.Sent.OfType<SM_MESSAGE>().Select(packet =>
            (string)typeof(SM_MESSAGE).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(packet)!);
        public Fixture(bool online = true)
        {
            Player = harness.SpawnPlayer();
            typeof(PlayerCommonData).GetField("level", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player.GetCommonData(), 1);
            if (online)
            {
                connection.SetAccount(Player.GetAccount());
                Player.SetClientConnection(connection);
                Assert.True(connection.SetActivePlayer(Player));
            }
            var template = new ItemTemplate { itemId = 199999991, castingDelay = 1000,
                RestrictXml = "1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1",
                actions = new ItemActions { itemActions = new List<AbstractItemAction> { Action } } };
            var rewardTemplate = new ItemTemplate { itemId = 199999992, RestrictXml = "1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1" };
            var data = new ItemData { its = new List<ItemTemplate> { template, rewardTemplate } };
            data.AfterUnmarshal(null!);
            typeof(StaticData).GetProperty(nameof(StaticData.ItemDataDh))!.SetValue(harness.Data.StaticData, data);
            // Upstream d254ea172 makes the command require a real decomposable entry (it reads the item's branches to
            // exclude selectable boxes), so the bare holder this used to inject would now refuse every item.
            var decomposables = new DecomposableItemsData
            {
                decomposableItems = new List<DecomposableItem>
                {
                    new()
                    {
                        itemId = template.itemId,
                        sets = new List<DecomposableSet>
                        {
                            new() { items = new List<DecomposedItem> { new() { itemId = rewardTemplate.itemId } } },
                        },
                    },
                },
            };
            decomposables.AfterUnmarshal(data, null);
            typeof(StaticData).GetProperty(nameof(StaticData.DecomposableItemsDataDh))!
                .SetValue(harness.Data.StaticData, decomposables);
            Item = new Item(991, template, 5, false, 0);
            var storage = (ItemStorage)typeof(Storage).GetField("itemStorage", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Player.GetInventory())!;
            Assert.True(storage.PutItem(Item));
        }
        public void Execute(string? count) => new Decompose().Execute(Player,
            count == null ? new[] { Item.GetItemId().ToString() } : new[] { Item.GetItemId().ToString(), count });
        public void Advance(int millis) => harness.Clock.Advance(TimeSpan.FromMilliseconds(millis));
        public void Dispose() => harness.Dispose();
    }

    private sealed class Connection() : AionConnection("127.0.0.1")
    {
        private readonly Queue<AionServerPacket> queue = new();
        public List<AionServerPacket> Sent { get; } = new();
        protected override Queue<AionServerPacket> GetSendMsgQueue() => queue;
        protected override void EnqueuePacket(AionServerPacket packet, bool closing) => Sent.Add(packet);
    }
}

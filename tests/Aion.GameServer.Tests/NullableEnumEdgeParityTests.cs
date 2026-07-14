using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class NullableEnumEdgeParityTests : IDisposable
{
    private const int UnmappedNpcId = 920015;
    private readonly DataManager? previousDataManager = DataManager.GetRegisteredInstance();

    [Theory]
    [InlineData(DialogAction.GIVEUP_CRAFT_EXPERT, false)]
    [InlineData(DialogAction.GIVEUP_CRAFT_MASTER, true)]
    public void AdvertisedRelinquishActionWithNoProfessionMappingIsIgnored(int action, bool master)
    {
        Npc npc = BuildUnmappedCraftNpc();

        Assert.True(npc.GetObjectTemplate().SupportsAction(action));
        Assert.False(DialogService.RelinquishCraftStatusForNpc(null!, npc, master));
    }

    [Fact]
    public void WellFormedAuctionMailWithUnknownResultIdIsIgnored()
    {
        var letter = new Letter(
            1,
            2,
            null!,
            0,
            "999,auction",
            "unused,1011",
            "$$HS_AUCTION_MAIL",
            DateTime.UtcNow,
            true,
            LetterType.NORMAL);

        Assert.False(HousingAuctionMailNotification.Handle(null!, letter));
    }

    private static Npc BuildUnmappedCraftNpc()
    {
        EnsureInfrastructure();
        var spawn = new SpawnTemplate(new SpawnGroup(1, UnmappedNpcId, 0, null), new SpawnSpotTemplate());
        var template = new NpcTemplate
        {
            npcId = UnmappedNpcId,
            level = 1,
            rank = NpcRank.DISCIPLINED,
            rating = NpcRating.NORMAL,
            statsTemplate = new StatsTemplate { MaxHp = 100 },
            talkInfo = new TalkInfo
            {
                HasDialog = true,
                FuncDialogIdsRaw = $"{DialogAction.GIVEUP_CRAFT_EXPERT} {DialogAction.GIVEUP_CRAFT_MASTER}",
            },
        };
        return new Npc(new NpcController(), spawn, template);
    }

    private static void EnsureInfrastructure()
    {
        try
        {
            _ = IDFactory.GetInstance();
        }
        catch (InvalidOperationException)
        {
            IDFactory.RegisterInstance(new IDFactory());
        }

        if (DataManager.GetRegisteredInstance()?.StaticData.NpcSkillDataDh is not null)
            return;

        var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));
        typeof(StaticData).GetProperty(nameof(StaticData.NpcSkillDataDh))!
            .SetValue(staticData, new NpcSkillData());
        ConstructorInfo constructor = typeof(DataManager).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(StaticData)],
            modifiers: null)!;
        DataManager.RegisterInstance((DataManager)constructor.Invoke([staticData]));
    }

    public void Dispose()
    {
        DataManager.RestoreInstance(previousDataManager);
    }
}

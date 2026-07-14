using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests;

public sealed class SystemMailRepositoryPlanTests
{
	[Fact]
	public void StoreLetter_UsesJavaMailDaoInsertSqlAndParameterOrder()
	{
		var mail = new PlayerMail(
			9001,
			4701,
			"Beyond Aion",
			"Bonus Pack",
			"Body",
			IsUnread: true,
			9101,
			186000242,
			0,
			1,
			new DateTime(2026, 5, 26, 9, 0, 0, DateTimeKind.Utc));

		var plan = SystemMailRepositoryPlan.StoreLetter(mail);

		Assert.Equal("com.aionemu.gameserver.dao.MailDAO.storeLetter/saveLetter", plan.JavaArtifact);
		Assert.Equal(SystemMailRepositoryPlan.StoreLetterSql, plan.Sql);
		Assert.Equal(
			[
				"mail_unique_id",
				"mail_recipient_id",
				"sender_name",
				"mail_title",
				"mail_message",
				"unread",
				"attached_item_id",
				"attached_kinah_count",
				"express",
				"recieved_time_epoch_millis",
			],
			plan.Parameters.Select(parameter => parameter.Name).ToArray());
		Assert.Equal([9001, 4701, "Beyond Aion", "Bonus Pack"], plan.Parameters.Take(4).Select(parameter => parameter.Value).ToArray());
		Assert.Equal(1_779_786_000_000L, plan.Parameters[^1].Value);
		Assert.Contains("FROM_UNIXTIME(? / 1000.0)", plan.Sql, StringComparison.Ordinal);
	}

	[Fact]
	public void StoreAttachedItem_UsesJavaInventoryInsertSqlOwnerAndSoulBoundParameterShape()
	{
		var item = new InventoryItem
		{
			ObjectId = 9101,
			ItemId = 186000242,
			Count = 15,
			Color = null,
			Creator = null,
			OwnerId = 9999,
			IsEquipped = false,
			IsSoulBound = true,
			Slot = 0,
			Location = 127,
			TuneCount = -1,
		};

		var plan = SystemMailRepositoryPlan.StoreAttachedItem(item, recipientObjectId: 4701);

		Assert.Equal("com.aionemu.gameserver.dao.InventoryDAO.store/insertItems", plan.JavaArtifact);
		Assert.Equal(SystemMailRepositoryPlan.StoreAttachedItemSql, plan.Sql);
		Assert.Equal(28, plan.Parameters.Count);
		Assert.Equal("item_unique_id", plan.Parameters[0].Name);
		Assert.Equal(9101, plan.Parameters[0].Value);
		Assert.Equal("item_owner", plan.Parameters[8].Name);
		Assert.Equal(4701, plan.Parameters[8].Value);
		Assert.Equal("is_soul_bound", plan.Parameters[10].Name);
		Assert.Equal(1, plan.Parameters[10].Value);
		Assert.Equal("rnd_plume_bonus", plan.Parameters[^1].Name);
	}

	[Fact]
	public void UpdateOfflineMailboxCounter_UsesJavaMailDaoSqlAndNameParameter()
	{
		var plan = SystemMailRepositoryPlan.UpdateOfflineMailboxCounter("Mailreward", mailboxLetters: 5);

		Assert.Equal("com.aionemu.gameserver.dao.MailDAO.updateOfflineMailCounter", plan.JavaArtifact);
		Assert.Equal(SystemMailRepositoryPlan.UpdateOfflineMailboxCounterSql, plan.Sql);
		Assert.Equal(["mailbox_letters", "name"], plan.Parameters.Select(parameter => parameter.Name).ToArray());
		Assert.Equal([5, "Mailreward"], plan.Parameters.Select(parameter => parameter.Value).ToArray());
	}
}

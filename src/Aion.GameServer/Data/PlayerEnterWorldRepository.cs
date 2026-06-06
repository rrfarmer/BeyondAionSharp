using System.Globalization;
using System.Text;
using Aion.Commons.Database;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ToyPet;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Aion.GameServer.Data;

public interface IPlayerEnterWorldRepository
{
	Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> InsertPlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerQuestAsync(int playerObjectId, int questId, CancellationToken cancellationToken = default);

	Task<bool> UpdatePlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default);

	Task<PlayerNpcFactionsSnapshot> LoadPlayerNpcFactionsAsync(
		int playerObjectId,
		NpcFactionTable npcFactions,
		int currentEpochSeconds = 0,
		CancellationToken cancellationToken = default);

	Task<bool> UpdatePlayerNpcFactionAsync(int playerObjectId, PlayerNpcFactionState factionState, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerRecipeAsync(int playerObjectId, int recipeId, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerEmotionAsync(int playerObjectId, int emotionId, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerTitleAsync(int playerObjectId, int titleId, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerMotionAsync(int playerObjectId, int motionId, CancellationToken cancellationToken = default);

	Task<bool> DeleteInventoryItemAsync(int itemOwnerId, int itemObjectId, CancellationToken cancellationToken = default);

	Task<bool> SaveItemUseSourceMutationAsync(
		int playerObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveCraftLearnActionMutationAsync(
		int playerObjectId,
		int recipeId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveEmotionLearnActionMutationAsync(
		int playerObjectId,
		PlayerEmotion emotion,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveTitleAddActionMutationAsync(
		int playerObjectId,
		PlayerTitle title,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveSkillLearnActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerSkill> skills,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveInventoryExpansionMutationAsync(
		int playerObjectId,
		int itemExpands,
		int warehouseBonusExpands,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveDyeItemActionMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveAnimationAddActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerMotion> motions,
		IReadOnlyList<int> deactivatedMotionIds,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveCosmeticItemActionMutationAsync(
		int playerObjectId,
		CharacterAppearance appearance,
		int deletedItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveDecomposeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveAssemblyItemActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedPartItems,
		IReadOnlyList<int> deletedPartObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default);

	Task<bool> SaveInventoryRewardMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default);

	Task<bool> UpdateAccountPassportRewardedAsync(int accountId, PlayerPassport passport, CancellationToken cancellationToken = default);

	Task<bool> SaveExpExtractActionMutationAsync(
		int playerObjectId,
		long newExp,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default);

	Task<bool> SaveApExtractActionMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		int deletedTargetItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemPurificationMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> materialItemUpdates,
		IReadOnlyList<int> deletedMaterialItemObjectIds,
		InventoryItem? baseItemUpdate,
		int? deletedBaseItemObjectId,
		IReadOnlyList<InventoryItem> updatedTargetItems,
		IReadOnlyList<InventoryItem> addedTargetItems,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemRemodelMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem kinahItemUpdate,
		InventoryItem? extractItemUpdate,
		int? deletedExtractItemObjectId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> SavePlayerMacroAsync(int playerObjectId, PlayerMacro macro, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerMacroAsync(int playerObjectId, int macroId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, long>> LoadPlayerHouseObjectCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> SavePlayerPortalCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, PlayerPortalCooldown> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default);

	Task<bool> SavePlayerCraftCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, long> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default);

	Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<PlayerOwnedPet>> LoadPlayerPetsAsync(int playerObjectId, CancellationToken cancellationToken = default);

	Task<bool> DeletePlayerPetAsync(int playerObjectId, int petObjectId, CancellationToken cancellationToken = default);

	Task<bool> UpdatePlayerPetNameAsync(int playerObjectId, int petObjectId, string petName, CancellationToken cancellationToken = default);

	Task<bool> SavePlayerPetDopingBagAsync(
		int playerObjectId,
		int petObjectId,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default);

	Task<bool> SavePlayerPetFeedStatusAsync(
		int playerObjectId,
		int petObjectId,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default);

	Task<bool> SavePlayerPetMoodDataAsync(
		int playerObjectId,
		int petObjectId,
		long moodStartedMillis,
		int shuggleCounter,
		long moodCooldownStartedMillis,
		long giftCooldownStartedMillis,
		DateTime? despawnTime,
		CancellationToken cancellationToken = default);

	Task<bool> SavePlayerPetFeedConsumeMutationAsync(
		int playerObjectId,
		int petObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> rewardItemUpdates,
		IReadOnlyList<InventoryItem> rewardItemAdds,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default);

	Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default);

	Task<bool> SavePeriodicPlayerGeneralAsync(Player player, CancellationToken cancellationToken = default);

	Task<bool> SavePeriodicPlayerItemsAsync(Player player, CancellationToken cancellationToken = default);

	Task<bool> SaveItemChargeMutationAsync(
		int playerObjectId,
		InventoryItem chargedItem,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemChargeAllMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemChargeBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		CancellationToken cancellationToken = default);

	Task<bool> SaveIdianPolishMutationAsync(
		int playerObjectId,
		InventoryItem? targetItem,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveIdianPolishBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> exhaustedItemUpdates,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemChargeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveStigmaChargeMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveManastoneRemovalMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int slot,
		int category,
		InventoryItem kinahItemUpdate,
		CancellationToken cancellationToken = default);

	Task<bool> SaveManastoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		ItemStoneSocket? addedStone,
		int addedCategory,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SaveEnchantItemMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SaveGodstoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemAmplificationMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? materialItemUpdate,
		int? deletedMaterialItemObjectId,
		InventoryItem? toolItemUpdate,
		int? deletedToolItemObjectId,
		CancellationToken cancellationToken = default);

	Task<bool> SaveInventoryItemSlotAsync(
		int playerObjectId,
		int itemObjectId,
		long newSlot,
		CancellationToken cancellationToken = default);

	Task<bool> SaveInventoryItemPackCountAsync(
		int playerObjectId,
		int itemObjectId,
		int newPackCount,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemSplitMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem newItem,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemMergeMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem targetItem,
		CancellationToken cancellationToken = default);

	Task<bool> SaveItemCrossStorageMoveMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default);

	// Java parity: services/ExchangeService.performTrade -> InventoryDAO.store transfers item ownership atomically
	// when an item moves from one player's inventory to another's during a trade.
	Task<bool> TransferItemOwnershipAsync(
		int itemObjectId,
		int previousOwnerId,
		int newOwnerId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default);

	Task<bool> SavePrivateStorePurchaseMutationAsync(
		int buyerObjectId,
		int sellerObjectId,
		IReadOnlyList<InventoryItem> sellerUpdatedItems,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		IReadOnlyList<InventoryItem> buyerUpdatedItems,
		IReadOnlyList<InventoryItem> buyerAddedItems,
		InventoryItem? buyerKinahItem,
		InventoryItem? sellerKinahItem,
		bool sellerKinahWasCreated,
		CancellationToken cancellationToken = default);

	Task<bool> SaveNpcShopBuyMutationAsync(
		int playerObjectId,
		PlayerAbyssRank? abyssRank,
		IReadOnlyList<InventoryItem> requiredItemUpdates,
		IReadOnlyList<int> deletedRequiredItemObjectIds,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? kinahItem,
		CancellationToken cancellationToken = default);

	Task<bool> SaveNpcShopSellMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		InventoryItem kinahItem,
		bool kinahWasCreated,
		CancellationToken cancellationToken = default);

	Task<bool> SaveNpcShopApSellMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SaveNpcShopRepurchaseMutationAsync(
		int playerObjectId,
		InventoryItem? kinahItem,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		CancellationToken cancellationToken = default);

	Task<bool> SaveEquipmentMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> items,
		InventoryItem? kinahItem = null,
		CancellationToken cancellationToken = default);

	Task<bool> SavePowerShardUseMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> countUpdateItems,
		IReadOnlyList<InventoryItem> equipUpdateItems,
		IReadOnlyList<int> deletedItemObjectIds,
		CancellationToken cancellationToken = default);

	Task<bool> SavePlayerLogoutAsync(Player player, DateTime lastOnline, CancellationToken cancellationToken = default);
}

public sealed class EmptyPlayerEnterWorldRepository : IPlayerEnterWorldRepository
{
	public Player? LoadedPlayer { get; init; }

	public IReadOnlyList<PlayerOwnedPet> LoadedPlayerPets { get; init; } = Array.Empty<PlayerOwnedPet>();

	public bool MarkPlayerOnlineResult { get; init; }

	public bool DeletePlayerPetResult { get; init; } = true;

	public int DeletePlayerPetCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId)? DeletedPlayerPet { get; private set; }

	public bool UpdatePlayerPetNameResult { get; init; } = true;

	public int UpdatePlayerPetNameCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId, string PetName)? UpdatedPlayerPetName { get; private set; }

	public bool SavePlayerPetDopingBagResult { get; init; } = true;

	public int SavePlayerPetDopingBagCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId, IReadOnlyList<int> ItemIds)? SavedPlayerPetDopingBag { get; private set; }

	public bool SavePlayerPetFeedStatusResult { get; init; } = true;

	public int SavePlayerPetFeedStatusCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId, int HungryLevel, int FeedProgress, long ReuseTime)? SavedPlayerPetFeedStatus { get; private set; }

	public bool SavePlayerPetMoodDataResult { get; init; } = true;

	public int SavePlayerPetMoodDataCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId, long MoodStartedMillis, int ShuggleCounter, long MoodCooldownStartedMillis, long GiftCooldownStartedMillis, DateTime? DespawnTime)?
		SavedPlayerPetMoodData { get; private set; }

	public bool SavePlayerPetFeedConsumeMutationResult { get; init; } = true;

	public int SavePlayerPetFeedConsumeMutationCalls { get; private set; }

	public (int PlayerObjectId, int PetObjectId, InventoryItem? SourceItemUpdate, int? DeletedSourceItemObjectId, IReadOnlyList<InventoryItem> RewardItemUpdates, IReadOnlyList<InventoryItem> RewardItemAdds, int HungryLevel, int FeedProgress, long ReuseTime)?
		SavedPlayerPetFeedConsumeMutation { get; private set; }

	public bool SaveItemUseSourceMutationResult { get; init; } = true;

	public bool InsertPlayerQuestResult { get; init; } = true;

	public int InsertPlayerQuestCalls { get; private set; }

	public PlayerQuestState? InsertedPlayerQuestState { get; private set; }

	public int UpdatePlayerQuestCalls { get; private set; }

	public PlayerQuestState? UpdatedPlayerQuestState { get; private set; }

	public bool SaveInventoryExpansionMutationResult { get; init; } = true;

	public int SaveInventoryExpansionMutationCalls { get; private set; }

	public bool SaveDecomposeActionMutationResult { get; init; } = true;

	public int SaveDecomposeActionMutationCalls { get; private set; }

	public int SaveAssemblyItemActionMutationCalls { get; private set; }

	public int SaveInventoryRewardMutationCalls { get; private set; }

	public (int PlayerObjectId, InventoryItem[] UpdatedRewardItems, InventoryItem[] AddedRewardItems)? SavedInventoryRewardMutation { get; private set; }

	public bool SaveInventoryRewardMutationResult { get; init; } = true;

	public PlayerAbyssRank? ApExtractAbyssRank { get; private set; }

	public PlayerAbyssRank? ItemPurificationAbyssRank { get; private set; }

	public bool SaveItemPurificationMutationResult { get; init; } = true;

	public IReadOnlyList<InventoryItem> ItemPurificationMaterialItemUpdates { get; private set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<int> ItemPurificationDeletedMaterialItemObjectIds { get; private set; } = Array.Empty<int>();

	public InventoryItem? ItemPurificationBaseItemUpdate { get; private set; }

	public int? ItemPurificationDeletedBaseItemObjectId { get; private set; }

	public IReadOnlyList<InventoryItem> ItemPurificationUpdatedTargetItems { get; private set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> ItemPurificationAddedTargetItems { get; private set; } = Array.Empty<InventoryItem>();

	public int SaveItemPurificationMutationCalls { get; private set; }

	public PlayerAbyssRank? ChargePaymentAbyssRank { get; private set; }

	public PlayerAbyssRank? ChargeAllPaymentAbyssRank { get; private set; }

	public InventoryItem? ChargeAllPaymentKinahItem { get; private set; }

	public IReadOnlyList<InventoryItem> ChargeAllChargedItems { get; private set; } = Array.Empty<InventoryItem>();

	public bool SaveItemChargeMutationResult { get; init; } = true;

	public bool SaveItemChargeAllMutationResult { get; init; } = true;

	public int SaveItemChargeMutationCalls { get; private set; }

	public int SaveItemChargeAllMutationCalls { get; private set; }

	public Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(LoadedPlayer is { ObjectId: var objectId, AccountId: var playerAccountId }
			&& objectId == playerObjectId
			&& playerAccountId == accountId
			? LoadedPlayer
			: null);
	}

	public Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<InventoryItem>>(Array.Empty<InventoryItem>());
	}

	public Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<InventoryItem>>(Array.Empty<InventoryItem>());
	}

	public Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<InventoryItem>>(Array.Empty<InventoryItem>());
	}

	public Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerSkill>>(Array.Empty<PlayerSkill>());
	}

	public Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	public Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, PlayerItemCooldown>>(new Dictionary<int, PlayerItemCooldown>());
	}

	public Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerQuestState>>(Array.Empty<PlayerQuestState>());
	}

	public Task<bool> InsertPlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default)
	{
		InsertPlayerQuestCalls++;
		InsertedPlayerQuestState = questState;
		return Task.FromResult(InsertPlayerQuestResult);
	}

	public Task<bool> DeletePlayerQuestAsync(int playerObjectId, int questId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> UpdatePlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default)
	{
		UpdatePlayerQuestCalls++;
		UpdatedPlayerQuestState = questState;
		return Task.FromResult(true);
	}

	public Task<PlayerNpcFactionsSnapshot> LoadPlayerNpcFactionsAsync(
		int playerObjectId,
		NpcFactionTable npcFactions,
		int currentEpochSeconds = 0,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(PlayerNpcFactionsSnapshot.Empty);
	}

	public Task<bool> UpdatePlayerNpcFactionAsync(int playerObjectId, PlayerNpcFactionState factionState, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerTitle>>(Array.Empty<PlayerTitle>());
	}

	public Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMotion>>(Array.Empty<PlayerMotion>());
	}

	public Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerEmotion>>(Array.Empty<PlayerEmotion>());
	}

	public Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
	}

	public Task<bool> DeletePlayerRecipeAsync(int playerObjectId, int recipeId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeletePlayerEmotionAsync(int playerObjectId, int emotionId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeletePlayerTitleAsync(int playerObjectId, int titleId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeletePlayerMotionAsync(int playerObjectId, int motionId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeleteInventoryItemAsync(int itemOwnerId, int itemObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemUseSourceMutationAsync(
		int playerObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(SaveItemUseSourceMutationResult);
	}

	public Task<bool> SaveCraftLearnActionMutationAsync(
		int playerObjectId,
		int recipeId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveEmotionLearnActionMutationAsync(
		int playerObjectId,
		PlayerEmotion emotion,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveTitleAddActionMutationAsync(
		int playerObjectId,
		PlayerTitle title,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveSkillLearnActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerSkill> skills,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveInventoryExpansionMutationAsync(
		int playerObjectId,
		int itemExpands,
		int warehouseBonusExpands,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		SaveInventoryExpansionMutationCalls++;
		return Task.FromResult(SaveInventoryExpansionMutationResult);
	}

	public Task<bool> SaveDyeItemActionMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveAnimationAddActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerMotion> motions,
		IReadOnlyList<int> deactivatedMotionIds,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveCosmeticItemActionMutationAsync(
		int playerObjectId,
		CharacterAppearance appearance,
		int deletedItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveDecomposeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		SaveDecomposeActionMutationCalls++;
		return Task.FromResult(SaveDecomposeActionMutationResult);
	}

	public Task<bool> SaveAssemblyItemActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedPartItems,
		IReadOnlyList<int> deletedPartObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		SaveAssemblyItemActionMutationCalls++;
		return Task.FromResult(true);
	}

	public Task<bool> SaveInventoryRewardMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		SaveInventoryRewardMutationCalls++;
		SavedInventoryRewardMutation = (
			playerObjectId,
			updatedRewardItems.ToArray(),
			addedRewardItems.ToArray());
		return Task.FromResult(SaveInventoryRewardMutationResult);
	}

	public int UpdateAccountPassportRewardedCalls { get; private set; }

	public (int AccountId, PlayerPassport Passport)? UpdatedAccountPassportRewarded { get; private set; }

	public bool UpdateAccountPassportRewardedResult { get; init; } = true;

	public Task<bool> UpdateAccountPassportRewardedAsync(int accountId, PlayerPassport passport, CancellationToken cancellationToken = default)
	{
		UpdateAccountPassportRewardedCalls++;
		UpdatedAccountPassportRewarded = (accountId, passport);
		return Task.FromResult(UpdateAccountPassportRewardedResult);
	}

	public Task<bool> SaveExpExtractActionMutationAsync(
		int playerObjectId,
		long newExp,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveApExtractActionMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		int deletedTargetItemObjectId,
		CancellationToken cancellationToken = default)
	{
		ApExtractAbyssRank = abyssRank;
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemPurificationMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> materialItemUpdates,
		IReadOnlyList<int> deletedMaterialItemObjectIds,
		InventoryItem? baseItemUpdate,
		int? deletedBaseItemObjectId,
		IReadOnlyList<InventoryItem> updatedTargetItems,
		IReadOnlyList<InventoryItem> addedTargetItems,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		SaveItemPurificationMutationCalls++;
		ItemPurificationMaterialItemUpdates = materialItemUpdates;
		ItemPurificationDeletedMaterialItemObjectIds = deletedMaterialItemObjectIds;
		ItemPurificationBaseItemUpdate = baseItemUpdate;
		ItemPurificationDeletedBaseItemObjectId = deletedBaseItemObjectId;
		ItemPurificationUpdatedTargetItems = updatedTargetItems;
		ItemPurificationAddedTargetItems = addedTargetItems;
		ItemPurificationAbyssRank = abyssRank;
		return Task.FromResult(SaveItemPurificationMutationResult);
	}

	public Task<bool> SaveItemRemodelMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem kinahItemUpdate,
		InventoryItem? extractItemUpdate,
		int? deletedExtractItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMacro>>(Array.Empty<PlayerMacro>());
	}

	public Task<bool> SavePlayerMacroAsync(int playerObjectId, PlayerMacro macro, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> DeletePlayerMacroAsync(int playerObjectId, int macroId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerMail>>(Array.Empty<PlayerMail>());
	}

	public Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(int playerObjectId, string race, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(PlayerBrokerSettlementSummary.Empty);
	}

	public Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerHouse>>(Array.Empty<PlayerHouse>());
	}

	public Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	public Task<IReadOnlyDictionary<int, long>> LoadPlayerHouseObjectCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, long>>(new Dictionary<int, long>());
	}

	public Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyDictionary<int, PlayerPortalCooldown>>(new Dictionary<int, PlayerPortalCooldown>());
	}

	public IReadOnlyDictionary<int, PlayerPortalCooldown>? SavedPortalCooldowns { get; private set; }

	public Task<bool> SavePlayerPortalCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, PlayerPortalCooldown> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default)
	{
		SavedPortalCooldowns = cooldowns;
		return Task.FromResult(true);
	}

	public IReadOnlyDictionary<int, long>? SavedCraftCooldowns { get; private set; }

	public long? SavedCraftCooldownsNowMillis { get; private set; }

	public Task<bool> SavePlayerCraftCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, long> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default)
	{
		SavedCraftCooldowns = cooldowns;
		SavedCraftCooldownsNowMillis = nowMillis;
		return Task.FromResult(true);
	}

	public Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<PlayerLifeStats?>(null);
	}

	public Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerFriend>>(Array.Empty<PlayerFriend>());
	}

	public Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<PlayerBlockedUser>>(Array.Empty<PlayerBlockedUser>());
	}

	public Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(PlayerAbyssRank.Default());
	}

	public Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PlayerSettings());
	}

	public Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<PlayerBindPoint?>(null);
	}

	public Task<IReadOnlyList<PlayerOwnedPet>> LoadPlayerPetsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(LoadedPlayerPets);
	}

	public Task<bool> DeletePlayerPetAsync(int playerObjectId, int petObjectId, CancellationToken cancellationToken = default)
	{
		DeletePlayerPetCalls++;
		DeletedPlayerPet = (playerObjectId, petObjectId);
		return Task.FromResult(DeletePlayerPetResult);
	}

	public Task<bool> UpdatePlayerPetNameAsync(int playerObjectId, int petObjectId, string petName, CancellationToken cancellationToken = default)
	{
		UpdatePlayerPetNameCalls++;
		UpdatedPlayerPetName = (playerObjectId, petObjectId, petName);
		return Task.FromResult(UpdatePlayerPetNameResult);
	}

	public Task<bool> SavePlayerPetDopingBagAsync(
		int playerObjectId,
		int petObjectId,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default)
	{
		SavePlayerPetDopingBagCalls++;
		SavedPlayerPetDopingBag = (playerObjectId, petObjectId, itemIds.ToArray());
		return Task.FromResult(SavePlayerPetDopingBagResult);
	}

	public Task<bool> SavePlayerPetFeedStatusAsync(
		int playerObjectId,
		int petObjectId,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default)
	{
		SavePlayerPetFeedStatusCalls++;
		SavedPlayerPetFeedStatus = (playerObjectId, petObjectId, hungryLevel, feedProgress, reuseTime);
		return Task.FromResult(SavePlayerPetFeedStatusResult);
	}

	public Task<bool> SavePlayerPetMoodDataAsync(
		int playerObjectId,
		int petObjectId,
		long moodStartedMillis,
		int shuggleCounter,
		long moodCooldownStartedMillis,
		long giftCooldownStartedMillis,
		DateTime? despawnTime,
		CancellationToken cancellationToken = default)
	{
		SavePlayerPetMoodDataCalls++;
		SavedPlayerPetMoodData = (
			playerObjectId,
			petObjectId,
			moodStartedMillis,
			shuggleCounter,
			moodCooldownStartedMillis,
			giftCooldownStartedMillis,
			despawnTime);
		return Task.FromResult(SavePlayerPetMoodDataResult);
	}

	public Task<bool> SavePlayerPetFeedConsumeMutationAsync(
		int playerObjectId,
		int petObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> rewardItemUpdates,
		IReadOnlyList<InventoryItem> rewardItemAdds,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default)
	{
		SavePlayerPetFeedConsumeMutationCalls++;
		SavedPlayerPetFeedConsumeMutation = (
			playerObjectId,
			petObjectId,
			sourceItemUpdate,
			deletedSourceItemObjectId,
			rewardItemUpdates.ToArray(),
			rewardItemAdds.ToArray(),
			hungryLevel,
			feedProgress,
			reuseTime);
		return Task.FromResult(SavePlayerPetFeedConsumeMutationResult);
	}

	public Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(MarkPlayerOnlineResult);
	}

	public Task<bool> SaveItemChargeMutationAsync(
		int playerObjectId,
		InventoryItem chargedItem,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		SaveItemChargeMutationCalls++;
		ChargePaymentAbyssRank = abyssRank;
		return Task.FromResult(SaveItemChargeMutationResult);
	}

	public Task<bool> SaveItemChargeAllMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		SaveItemChargeAllMutationCalls++;
		ChargeAllChargedItems = chargedItems;
		ChargeAllPaymentKinahItem = kinahItem;
		ChargeAllPaymentAbyssRank = abyssRank;
		return Task.FromResult(SaveItemChargeAllMutationResult);
	}

	public Task<bool> SaveItemChargeBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveIdianPolishMutationAsync(
		int playerObjectId,
		InventoryItem? targetItem,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveIdianPolishBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> exhaustedItemUpdates,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemChargeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveStigmaChargeMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveManastoneRemovalMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int slot,
		int category,
		InventoryItem kinahItemUpdate,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveManastoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		ItemStoneSocket? addedStone,
		int addedCategory,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveEnchantItemMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveGodstoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemAmplificationMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? materialItemUpdate,
		int? deletedMaterialItemObjectId,
		InventoryItem? toolItemUpdate,
		int? deletedToolItemObjectId,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveInventoryItemSlotAsync(
		int playerObjectId,
		int itemObjectId,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> TransferItemOwnershipAsync(
		int itemObjectId,
		int previousOwnerId,
		int newOwnerId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public bool SavePrivateStorePurchaseMutationResult { get; init; } = true;

	public int SavePrivateStorePurchaseMutationCalls { get; private set; }

	public PrivateStorePurchasePersistenceCapture? PrivateStorePurchasePersistence { get; private set; }

	public Task<bool> SavePrivateStorePurchaseMutationAsync(
		int buyerObjectId,
		int sellerObjectId,
		IReadOnlyList<InventoryItem> sellerUpdatedItems,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		IReadOnlyList<InventoryItem> buyerUpdatedItems,
		IReadOnlyList<InventoryItem> buyerAddedItems,
		InventoryItem? buyerKinahItem,
		InventoryItem? sellerKinahItem,
		bool sellerKinahWasCreated,
		CancellationToken cancellationToken = default)
	{
		SavePrivateStorePurchaseMutationCalls++;
		PrivateStorePurchasePersistence = new PrivateStorePurchasePersistenceCapture(
			buyerObjectId,
			sellerObjectId,
			sellerUpdatedItems,
			sellerDeletedItemObjectIds,
			buyerUpdatedItems,
			buyerAddedItems,
			buyerKinahItem,
			sellerKinahItem,
			sellerKinahWasCreated);
		return Task.FromResult(SavePrivateStorePurchaseMutationResult);
	}

	public bool SaveNpcShopBuyMutationResult { get; init; } = true;

	public int SaveNpcShopBuyMutationCalls { get; private set; }

	public NpcShopBuyPersistenceCapture? NpcShopBuyPersistence { get; private set; }

	public Task<bool> SaveNpcShopBuyMutationAsync(
		int playerObjectId,
		PlayerAbyssRank? abyssRank,
		IReadOnlyList<InventoryItem> requiredItemUpdates,
		IReadOnlyList<int> deletedRequiredItemObjectIds,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? kinahItem,
		CancellationToken cancellationToken = default)
	{
		SaveNpcShopBuyMutationCalls++;
		NpcShopBuyPersistence = new NpcShopBuyPersistenceCapture(
			playerObjectId,
			abyssRank,
			requiredItemUpdates,
			deletedRequiredItemObjectIds,
			updatedItems,
			addedItems,
			kinahItem);
		return Task.FromResult(SaveNpcShopBuyMutationResult);
	}

	public bool SaveNpcShopSellMutationResult { get; init; } = true;

	public int SaveNpcShopSellMutationCalls { get; private set; }

	public NpcShopSellPersistenceCapture? NpcShopSellPersistence { get; private set; }

	public Task<bool> SaveNpcShopSellMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		InventoryItem kinahItem,
		bool kinahWasCreated,
		CancellationToken cancellationToken = default)
	{
		SaveNpcShopSellMutationCalls++;
		NpcShopSellPersistence = new NpcShopSellPersistenceCapture(
			playerObjectId,
			sellerItemUpdates,
			sellerDeletedItemObjectIds,
			kinahItem,
			kinahWasCreated);
		return Task.FromResult(SaveNpcShopSellMutationResult);
	}

	public bool SaveNpcShopApSellMutationResult { get; init; } = true;

	public int SaveNpcShopApSellMutationCalls { get; private set; }

	public NpcShopApSellPersistenceCapture? NpcShopApSellPersistence { get; private set; }

	public Task<bool> SaveNpcShopApSellMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		SaveNpcShopApSellMutationCalls++;
		NpcShopApSellPersistence = new NpcShopApSellPersistenceCapture(
			playerObjectId,
			abyssRank,
			sellerItemUpdates,
			sellerDeletedItemObjectIds);
		return Task.FromResult(SaveNpcShopApSellMutationResult);
	}

	public bool SaveNpcShopRepurchaseMutationResult { get; init; } = true;

	public int SaveNpcShopRepurchaseMutationCalls { get; private set; }

	public NpcShopRepurchasePersistenceCapture? NpcShopRepurchasePersistence { get; private set; }

	public Task<bool> SaveNpcShopRepurchaseMutationAsync(
		int playerObjectId,
		InventoryItem? kinahItem,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		CancellationToken cancellationToken = default)
	{
		SaveNpcShopRepurchaseMutationCalls++;
		NpcShopRepurchasePersistence = new NpcShopRepurchasePersistenceCapture(
			playerObjectId,
			kinahItem,
			updatedItems,
			addedItems);
		return Task.FromResult(SaveNpcShopRepurchaseMutationResult);
	}

	public Task<bool> SaveInventoryItemPackCountAsync(
		int playerObjectId,
		int itemObjectId,
		int newPackCount,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemSplitMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem newItem,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemMergeMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem targetItem,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveItemCrossStorageMoveMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SaveEquipmentMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> items,
		InventoryItem? kinahItem = null,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SavePowerShardUseMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> countUpdateItems,
		IReadOnlyList<InventoryItem> equipUpdateItems,
		IReadOnlyList<int> deletedItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		return Task.FromResult(true);
	}

	public Task<bool> SavePlayerLogoutAsync(Player player, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> SavePeriodicPlayerGeneralAsync(Player player, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}

	public Task<bool> SavePeriodicPlayerItemsAsync(Player player, CancellationToken cancellationToken = default)
	{
		return Task.FromResult(false);
	}
}

public sealed record PrivateStorePurchasePersistenceCapture(
	int BuyerObjectId,
	int SellerObjectId,
	IReadOnlyList<InventoryItem> SellerUpdatedItems,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	IReadOnlyList<InventoryItem> BuyerUpdatedItems,
	IReadOnlyList<InventoryItem> BuyerAddedItems,
	InventoryItem? BuyerKinahItem,
	InventoryItem? SellerKinahItem,
	bool SellerKinahWasCreated);

public sealed record NpcShopBuyPersistenceCapture(
	int PlayerObjectId,
	PlayerAbyssRank? AbyssRank,
	IReadOnlyList<InventoryItem> RequiredItemUpdates,
	IReadOnlyList<int> DeletedRequiredItemObjectIds,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<InventoryItem> AddedItems,
	InventoryItem? KinahItem);

public sealed record NpcShopSellPersistenceCapture(
	int PlayerObjectId,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	InventoryItem KinahItem,
	bool KinahWasCreated);

public sealed record NpcShopApSellPersistenceCapture(
	int PlayerObjectId,
	PlayerAbyssRank AbyssRank,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<int> SellerDeletedItemObjectIds);

public sealed record NpcShopRepurchasePersistenceCapture(
	int PlayerObjectId,
	InventoryItem? KinahItem,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<InventoryItem> AddedItems);

internal sealed record ItemStonePersistenceRow(
	int ItemObjectId,
	int ItemId,
	int Slot,
	int Category,
	int PolishNumber,
	int PolishCharge,
	int ProcCount);

internal sealed record AccountPassportRestoreSnapshot(
	IReadOnlyList<PlayerPassport> Passports,
	int Stamps,
	DateTime? LastStamp);

public sealed class MySqlPlayerEnterWorldRepository : IPlayerEnterWorldRepository
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly ILogger<MySqlPlayerEnterWorldRepository> _logger;

	public MySqlPlayerEnterWorldRepository(
		GameServerRuntimeContext runtimeContext,
		ILogger<MySqlPlayerEnterWorldRepository> logger)
	{
		_runtimeContext = runtimeContext;
		_logger = logger;
	}

	public async Task<Player?> LoadPlayerAsync(int accountId, int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.loadPlayerCommonData, scoped to the authenticated account.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, account_id, name, player_class, race, gender, note, creation_date, exp, recoverexp, dp, reposte_energy, online, last_online,
					quest_expands, npc_expands, item_expands, wh_npc_expands, wh_bonus_expands, title_id, bonus_title_id,
					lm.legion_id, lm.`rank` AS legion_rank, l.level AS legion_level, l.name AS legion_name,
					le.emblem_id AS legion_emblem_id, le.emblem_type AS legion_emblem_type,
					le.color_a AS legion_emblem_color_a, le.color_r AS legion_emblem_color_r,
					le.color_g AS legion_emblem_color_g, le.color_b AS legion_emblem_color_b,
					world_id, x, y, z, heading,
					pa.face, pa.hair, pa.deco, pa.tattoo, pa.face_contour, pa.expression, pa.jaw_line,
					pa.skin_rgb, pa.hair_rgb, pa.eye_rgb, pa.lip_rgb, pa.face_shape, pa.forehead, pa.eye_height,
					pa.eye_space, pa.eye_width, pa.eye_size, pa.eye_shape, pa.eye_angle, pa.brow_height,
					pa.brow_angle, pa.brow_shape, pa.nose, pa.nose_bridge, pa.nose_width, pa.nose_tip,
					pa.cheek, pa.lip_height, pa.mouth_size, pa.lip_size, pa.smile, pa.lip_shape, pa.jaw_height,
					pa.chin_jut, pa.ear_shape, pa.head_size, pa.neck, pa.neck_length, pa.shoulders,
					pa.shoulder_size, pa.torso, pa.chest, pa.waist, pa.hips, pa.arm_thickness, pa.arm_length,
					pa.hand_size, pa.leg_thickness, pa.leg_length, pa.foot_size, pa.facial_rate, pa.voice, pa.height
				FROM players p
				LEFT JOIN player_appearance pa ON pa.player_id = p.id
				LEFT JOIN legion_members lm ON lm.player_id = p.id
				LEFT JOIN legions l ON l.id = lm.legion_id
				LEFT JOIN legion_emblems le ON le.legion_id = lm.legion_id
				WHERE p.id = ? AND p.account_id = ? AND (p.deletion_date IS NULL OR p.deletion_date > CURRENT_TIMESTAMP)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = accountId },
				});

			Player? player;
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				if (!await reader.ReadAsync(cancellationToken))
					return null;

				player = new Player
				{
					ObjectId = reader.GetInt32(reader.GetOrdinal("id")),
					AccountId = reader.GetInt32(reader.GetOrdinal("account_id")),
					Name = ReadString(reader, "name"),
					PlayerClass = ReadString(reader, "player_class"),
					Race = ReadString(reader, "race"),
					Gender = ReadString(reader, "gender"),
					Note = ReadString(reader, "note"),
					CreationDate = ReadDateTime(reader, "creation_date") ?? DateTime.UnixEpoch,
					LegionId = ReadInt(reader, "legion_id"),
					LegionRank = ReadString(reader, "legion_rank"), // Java parity: LegionMember rank (enum name); empty when no legion.
					LegionLevel = ReadInt(reader, "legion_level"),
					LegionName = ReadString(reader, "legion_name"),
					LegionEmblemId = (byte)ReadInt(reader, "legion_emblem_id"),
					LegionEmblemType = ToLegionEmblemTypeValue(ReadString(reader, "legion_emblem_type")),
					LegionEmblemColorA = (byte)ReadInt(reader, "legion_emblem_color_a"),
					LegionEmblemColorR = (byte)ReadInt(reader, "legion_emblem_color_r"),
					LegionEmblemColorG = (byte)ReadInt(reader, "legion_emblem_color_g"),
					LegionEmblemColorB = (byte)ReadInt(reader, "legion_emblem_color_b"),
					Appearance = ReadAppearance(reader),
					Exp = reader.GetInt64(reader.GetOrdinal("exp")),
					RecoverableExp = ReadLong(reader, "recoverexp"),
					Dp = ReadInt(reader, "dp"),
					ReposeEnergy = ReadLong(reader, "reposte_energy"),
					IsOnline = ReadBoolean(reader, "online"),
					LastOnline = ReadDateTime(reader, "last_online"),
					NpcExpands = ReadInt(reader, "npc_expands"),
					QuestExpands = ReadInt(reader, "quest_expands"),
					ItemExpands = ReadInt(reader, "item_expands"),
					WarehouseNpcExpands = ReadInt(reader, "wh_npc_expands"),
					WarehouseBonusExpands = ReadInt(reader, "wh_bonus_expands"),
					TitleId = ReadInt(reader, "title_id"),
					BonusTitleId = ReadInt(reader, "bonus_title_id"),
					Position = new WorldPosition(
						ReadInt(reader, "world_id"),
						reader.GetFloat(reader.GetOrdinal("x")),
						reader.GetFloat(reader.GetOrdinal("y")),
						reader.GetFloat(reader.GetOrdinal("z")),
						(byte)ReadInt(reader, "heading")),
				};
			}

			RestoreAccountPassportState(
				player,
				await LoadAccountPassportStateAsync(connection, accountId, cancellationToken));
			return player;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load player {PlayerObjectId} for enter-world", playerObjectId);
			return null;
		}
	}

	internal static void RestoreAccountPassportState(Player player, AccountPassportRestoreSnapshot snapshot)
	{
		player.Passports = snapshot.Passports;
		player.PassportStamps = snapshot.Stamps;
		player.LastPassportStamp = snapshot.LastStamp;
	}

	private static async Task<AccountPassportRestoreSnapshot> LoadAccountPassportStateAsync(
		MySqlConnection connection,
		int accountId,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/AccountPassportsDAO.loadPassport(Account).
		var passports = new List<PlayerPassport>();
		await using (var command = connection.CreateCommand())
		{
			command.CommandText = """
				SELECT passport_id, rewarded, arrive_date
				FROM account_passports
				WHERE account_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = accountId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				passports.Add(new PlayerPassport(
					ReadInt(reader, "passport_id"),
					ReadBoolean(reader, "rewarded"),
					ReadDateTime(reader, "arrive_date") ?? DateTime.UnixEpoch));
			}
		}

		await using (var command = connection.CreateCommand())
		{
			command.CommandText = """
				SELECT stamps, last_stamp
				FROM account_stamps
				WHERE account_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = accountId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (await reader.ReadAsync(cancellationToken))
			{
				return new AccountPassportRestoreSnapshot(
					passports,
					ReadInt(reader, "stamps"),
					ReadDateTime(reader, "last_stamp"));
			}
		}

		await using (var insertCommand = connection.CreateCommand())
		{
			insertCommand.CommandText = """
				INSERT INTO account_stamps (account_id, stamps, last_stamp)
				VALUES (?, ?, ?)
				""";
			insertCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = accountId },
					new MySqlParameter { Value = 0 },
					new MySqlParameter { Value = DBNull.Value },
				});
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		return new AccountPassportRestoreSnapshot(passports, Stamps: 0, LastStamp: null);
	}

	public async Task<bool> MarkPlayerOnlineAsync(int playerObjectId, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerDAO.onlinePlayer + PlayerDAO.storeLastOnlineTime.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var onlineCommand = connection.CreateCommand();
			onlineCommand.CommandText = "UPDATE players SET online = ? WHERE id = ?";
			onlineCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = true },
					new MySqlParameter { Value = playerObjectId },
				});
			var onlineRows = await onlineCommand.ExecuteNonQueryAsync(cancellationToken);
			if (onlineRows <= 0)
				return false;

			await using var lastOnlineCommand = connection.CreateCommand();
			lastOnlineCommand.CommandText = "UPDATE players SET last_online = ? WHERE id = ?";
			lastOnlineCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = lastOnline },
					new MySqlParameter { Value = playerObjectId },
				});
			return await lastOnlineCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not mark player {PlayerObjectId} online", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerLogoutAsync(Player player, DateTime lastOnline, CancellationToken cancellationToken = default)
	{
		// Java parity: services/player/PlayerLeaveWorldService.leaveWorld -> PlayerService.storePlayer,
		// PlayerDAO.storeLastOnlineTime, then PlayerDAO.onlinePlayer(false).
		// Java PlayerService.storePlayer also calls InventoryDAO.store(player), which flushes
		// dirty storage/equipment item rows via InventoryDAO.UPDATE_QUERY before the final
		// online/offline flags are written. C# now tracks item-level dirty/deleted state for
		// the currently modeled player-owned storages, but still uses conservative snapshot
		// updates for current rows while flushing tracked deletes explicitly on logout.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			if (player.LifeStats != null)
				await SavePlayerLifeStatsAsync(connection, player.ObjectId, player.LifeStats, cancellationToken);
			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			await SavePlayerSkillCooldownsAsync(connection, player.ObjectId, player.SkillCooldowns, nowMillis, cancellationToken);
			await SavePlayerItemCooldownsAsync(connection, player.ObjectId, player.ItemCooldowns, nowMillis, cancellationToken);
			await SavePlayerPortalCooldownsAsync(connection, player.ObjectId, player.PortalCooldowns, nowMillis, cancellationToken);
			// Java parity: PlayerService.storePlayer saves portal, craft, then house-object cooldowns.
			await SavePlayerCraftCooldownsAsync(player.ObjectId, player.CraftCooldowns, nowMillis, cancellationToken);
			await SavePlayerHouseObjectCooldownsAsync(connection, player.ObjectId, player.HouseObjectCooldowns, nowMillis, cancellationToken);
			await SavePlayerSettingsAsync(connection, player.ObjectId, player.Settings, cancellationToken);
			var dirtyItems = player.GetDirtyItemsToUpdate();
			if (!await DeleteInventoryItemSnapshotAsync(
				connection,
				dirtyItems.Where(item => item.PersistentState == InventoryItemPersistentState.Deleted).ToArray(),
				cancellationToken))
			{
				return false;
			}
			if (!await SaveInventoryItemSnapshotAsync(
				connection,
				dirtyItems.Where(item => item.PersistentState != InventoryItemPersistentState.Deleted).ToArray(),
				cancellationToken))
			{
				return false;
			}
			player.MarkDirtyItemsPersisted();

			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE players
				SET exp = ?, recoverexp = ?, x = ?, y = ?, z = ?, heading = ?, world_id = ?,
					quest_expands = ?, npc_expands = ?, item_expands = ?, wh_npc_expands = ?, wh_bonus_expands = ?,
					note = ?, title_id = ?, bonus_title_id = ?, dp = ?, mailbox_letters = ?, reposte_energy = ?,
					last_online = ?, online = ?
				WHERE id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = player.Exp },
					new MySqlParameter { Value = player.RecoverableExp },
					new MySqlParameter { Value = player.Position.X },
					new MySqlParameter { Value = player.Position.Y },
					new MySqlParameter { Value = player.Position.Z },
					new MySqlParameter { Value = player.Position.Heading },
					new MySqlParameter { Value = player.Position.WorldId },
					new MySqlParameter { Value = player.QuestExpands },
					new MySqlParameter { Value = player.NpcExpands },
					new MySqlParameter { Value = player.ItemExpands },
					new MySqlParameter { Value = player.WarehouseNpcExpands },
					new MySqlParameter { Value = player.WarehouseBonusExpands },
					new MySqlParameter { Value = player.Note },
					new MySqlParameter { Value = player.TitleId },
					new MySqlParameter { Value = player.BonusTitleId },
					new MySqlParameter { Value = player.Dp },
					new MySqlParameter { Value = player.Mailbox.Count },
					new MySqlParameter { Value = player.ReposeEnergy },
					new MySqlParameter { Value = lastOnline },
					new MySqlParameter { Value = false },
					new MySqlParameter { Value = player.ObjectId },
				});

			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save logout state for player {PlayerObjectId}", player.ObjectId);
			return false;
		}
	}

	public async Task<bool> SavePeriodicPlayerGeneralAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerEnterWorldService.GeneralUpdateTask.run persists live player state
		// without changing online state. This covers the currently modeled C# general state,
		// including AbyssRankDAO.storeAbyssRank(player).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await SavePeriodicAbyssRankAsync(connection, player.ObjectId, player.AbyssRank, cancellationToken);
			await SavePeriodicPlayerSkillsAsync(connection, player.ObjectId, player.Skills, cancellationToken);
			await SavePeriodicPlayerQuestsAsync(connection, player.ObjectId, player.Quests, cancellationToken);
			if (player.LifeStats != null)
				await SavePlayerLifeStatsAsync(connection, player.ObjectId, player.LifeStats, cancellationToken);
			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			await SavePlayerSkillCooldownsAsync(connection, player.ObjectId, player.SkillCooldowns, nowMillis, cancellationToken);
			await SavePlayerItemCooldownsAsync(connection, player.ObjectId, player.ItemCooldowns, nowMillis, cancellationToken);
			await SavePlayerPortalCooldownsAsync(connection, player.ObjectId, player.PortalCooldowns, nowMillis, cancellationToken);
			await SavePlayerCraftCooldownsAsync(player.ObjectId, player.CraftCooldowns, nowMillis, cancellationToken);
			await SavePlayerHouseObjectCooldownsAsync(connection, player.ObjectId, player.HouseObjectCooldowns, nowMillis, cancellationToken);
			await SavePlayerSettingsAsync(connection, player.ObjectId, player.Settings, cancellationToken);

			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE players
				SET exp = ?, recoverexp = ?, x = ?, y = ?, z = ?, heading = ?, world_id = ?,
					quest_expands = ?, npc_expands = ?, item_expands = ?, wh_npc_expands = ?, wh_bonus_expands = ?,
					note = ?, title_id = ?, bonus_title_id = ?, dp = ?, mailbox_letters = ?, reposte_energy = ?
				WHERE id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = player.Exp },
					new MySqlParameter { Value = player.RecoverableExp },
					new MySqlParameter { Value = player.Position.X },
					new MySqlParameter { Value = player.Position.Y },
					new MySqlParameter { Value = player.Position.Z },
					new MySqlParameter { Value = player.Position.Heading },
					new MySqlParameter { Value = player.Position.WorldId },
					new MySqlParameter { Value = player.QuestExpands },
					new MySqlParameter { Value = player.NpcExpands },
					new MySqlParameter { Value = player.ItemExpands },
					new MySqlParameter { Value = player.WarehouseNpcExpands },
					new MySqlParameter { Value = player.WarehouseBonusExpands },
					new MySqlParameter { Value = player.Note },
					new MySqlParameter { Value = player.TitleId },
					new MySqlParameter { Value = player.BonusTitleId },
					new MySqlParameter { Value = player.Dp },
					new MySqlParameter { Value = player.Mailbox.Count },
					new MySqlParameter { Value = player.ReposeEnergy },
					new MySqlParameter { Value = player.ObjectId },
				});

			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			await SavePeriodicPlayerHousesAsync(connection, player.ObjectId, player.Houses, cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save periodic general state for player {PlayerObjectId}", player.ObjectId);
			return false;
		}
	}

	public async Task<bool> SavePeriodicPlayerItemsAsync(Player player, CancellationToken cancellationToken = default)
	{
		// Java parity: PlayerEnterWorldService.ItemUpdateTask.run calls InventoryDAO.store(player)
		// followed by ItemStoneListDAO.save(player). C# flushes tracked dirty/deleted item rows
		// and snapshots the modeled player-owned item_stones rows from live inventory state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			var dirtyItems = player.GetDirtyItemsToUpdate();
			if (!await DeleteInventoryItemSnapshotAsync(
				connection,
				dirtyItems.Where(item => item.PersistentState == InventoryItemPersistentState.Deleted).ToArray(),
				cancellationToken))
			{
				return false;
			}
			if (!await SaveInventoryItemSnapshotAsync(
				connection,
				dirtyItems.Where(item => item.PersistentState != InventoryItemPersistentState.Deleted).ToArray(),
				cancellationToken))
			{
				return false;
			}
			if (!await SaveInventoryItemStonesSnapshotAsync(
				connection,
				GetPlayerItemStoneSnapshotItems(player),
				cancellationToken))
			{
				return false;
			}
			player.MarkDirtyItemsPersisted();
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save periodic item state for player {PlayerObjectId}", player.ObjectId);
			return false;
		}
	}

	internal static IReadOnlyList<InventoryItem> GetPlayerItemStoneSnapshotItems(Player player)
	{
		// Java parity: Player.getAllItems feeds ItemStoneListDAO.save(player). The currently
		// modeled C# storages are cube/equipment, regular warehouse, and account warehouse.
		var items = new List<InventoryItem>();
		items.AddRange(player.InventoryItems);
		items.AddRange(player.WarehouseItems);
		items.AddRange(player.AccountWarehouseItems);
		return items
			.Where(item => item.PersistentState is not InventoryItemPersistentState.Deleted and not InventoryItemPersistentState.NoAction)
			.GroupBy(item => item.ObjectId)
			.Select(group => group.First())
			.ToArray();
	}

	private static async Task<bool> SaveInventoryItemSnapshotAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		foreach (var item in items)
		{
			if (!await SaveInventoryItemFullStateAsync(connection, item, cancellationToken))
				return false;
		}

		return true;
	}

	private static async Task<bool> SaveInventoryItemStonesSnapshotAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		foreach (var item in items)
			await ReplaceInventoryItemStonesAsync(connection, transaction: null, item, cancellationToken);

		return true;
	}

	private static async Task<bool> DeleteInventoryItemSnapshotAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		foreach (var item in items)
		{
			if (!await DeleteInventoryItemAsync(connection, null, item.OwnerId, item.ObjectId, cancellationToken))
				return false;
		}

		return true;
	}

	public async Task<bool> SaveItemChargeMutationAsync(
		int playerObjectId,
		InventoryItem chargedItem,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemChargeService.chargeItem persists ChargeInfo and payment mutations before sending item updates.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await using var chargeCommand = connection.CreateCommand();
			chargeCommand.Transaction = transaction;
			chargeCommand.CommandText = "UPDATE inventory SET charge = ? WHERE item_unique_id = ? AND item_owner = ?";
			chargeCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = chargedItem.Charge },
					new MySqlParameter { Value = chargedItem.ObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			var chargeRows = await chargeCommand.ExecuteNonQueryAsync(cancellationToken);
			if (chargeRows <= 0)
				return false;

			if (kinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;
			if (abyssRank != null)
				await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item charge mutation for player {PlayerObjectId} item {ItemObjectId}", playerObjectId, chargedItem.ObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemChargeAllMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? kinahItem,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemChargeService.startChargingEquippedItems processes one payment before mutating the filtered equipped items.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var chargedItem in chargedItems)
			{
				await using var chargeCommand = connection.CreateCommand();
				chargeCommand.Transaction = transaction;
				chargeCommand.CommandText = "UPDATE inventory SET charge = ? WHERE item_unique_id = ? AND item_owner = ?";
				chargeCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = chargedItem.Charge },
						new MySqlParameter { Value = chargedItem.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await chargeCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (kinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;
			if (abyssRank != null)
				await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save charge-all mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemChargeBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/items/ChargeInfo.updateChargePoints marks the item/equipment persistent after observer burn.
		if (chargedItems.Count == 0)
			return true;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var chargedItem in chargedItems)
			{
				await using var chargeCommand = connection.CreateCommand();
				chargeCommand.Transaction = transaction;
				chargeCommand.CommandText = "UPDATE inventory SET charge = ? WHERE item_unique_id = ? AND item_owner = ?";
				chargeCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = chargedItem.Charge },
						new MySqlParameter { Value = chargedItem.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await chargeCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item charge burn mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveIdianPolishMutationAsync(
		int playerObjectId,
		InventoryItem? targetItem,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/templates/item/actions/PolishAction + dao/ItemStoneListDAO.storeIdianStones.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (targetItem != null)
			{
				if (!await InventoryItemExistsAsync(connection, transaction, playerObjectId, targetItem.ObjectId, cancellationToken))
					return false;
				await SaveIdianStoneAsync(connection, transaction, targetItem, cancellationToken);
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save idian polish mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveIdianPolishBurnMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> exhaustedItemUpdates,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/items/IdianStone.decreasePolishCharge stores only exhausted idian deletions immediately.
		if (exhaustedItemUpdates.Count == 0)
			return true;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in exhaustedItemUpdates)
			{
				if (!await InventoryItemExistsAsync(connection, transaction, playerObjectId, item.ObjectId, cancellationToken))
					return false;

				await SaveIdianStoneAsync(connection, transaction, item, cancellationToken);
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save idian polish burn mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemChargeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> chargedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/templates/item/actions/ChargeAction consumes the charge item, then ItemChargeService.chargeItems mutates equipped item charge.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var chargedItem in chargedItems)
			{
				await using var chargeCommand = connection.CreateCommand();
				chargeCommand.Transaction = transaction;
				chargeCommand.CommandText = "UPDATE inventory SET charge = ? WHERE item_unique_id = ? AND item_owner = ?";
				chargeCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = chargedItem.Charge },
						new MySqlParameter { Value = chargedItem.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await chargeCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save charge action mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveStigmaChargeMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/StigmaService.chargeStigma persists charge-stone consume plus target enchant/delete.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (targetItemUpdate != null)
			{
				await using var targetCommand = connection.CreateCommand();
				targetCommand.Transaction = transaction;
				targetCommand.CommandText = "UPDATE inventory SET item_count = ?, enchant = ? WHERE item_unique_id = ? AND item_owner = ?";
				targetCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = targetItemUpdate.Count },
						new MySqlParameter { Value = targetItemUpdate.Enchant },
						new MySqlParameter { Value = targetItemUpdate.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await targetCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (deletedTargetItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedTargetItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& deletedSourceItemObjectId != deletedTargetItemObjectId
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save stigma charge mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveManastoneRemovalMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int slot,
		int category,
		InventoryItem kinahItemUpdate,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemSocketService.removeManastone -> dao/ItemStoneListDAO.store* delete plus Storage.tryDecreaseKinah.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await InventoryItemExistsAsync(connection, transaction, playerObjectId, itemObjectId, cancellationToken))
				return false;

			await using (var stoneCommand = connection.CreateCommand())
			{
				stoneCommand.Transaction = transaction;
				stoneCommand.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ? AND slot = ? AND category = ?";
				stoneCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = itemObjectId },
						new MySqlParameter { Value = slot },
						new MySqlParameter { Value = category },
					});
				await stoneCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItemUpdate, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save manastone removal mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveManastoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		ItemStoneSocket? addedStone,
		int addedCategory,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.socketManastoneAct persists updateSupplements, ItemSocketService.addManaStone, and source consume.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await InventoryItemExistsAsync(connection, transaction, playerObjectId, targetItemUpdate.ObjectId, cancellationToken))
				return false;

			if (!await SaveInventoryItemTuneCountAsync(connection, transaction, playerObjectId, targetItemUpdate, cancellationToken))
				return false;

			if (addedStone != null)
				await SaveManastoneAsync(connection, transaction, targetItemUpdate.ObjectId, addedStone, addedCategory, cancellationToken);

			foreach (var supplementItemUpdate in supplementItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, supplementItemUpdate, cancellationToken))
					return false;
			}

			foreach (var deletedSupplementItemObjectId in deletedSupplementItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSupplementItemObjectId, cancellationToken))
					return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save manastone socket mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveEnchantItemMutationAsync(
		int playerObjectId,
		InventoryItem? targetItemUpdate,
		int? deletedTargetItemObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> supplementItemUpdates,
		IReadOnlyList<int> deletedSupplementItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.enchantItemAct persists target enchant/amplification/tune, supplements, source, and possible target destruction.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (targetItemUpdate != null && !await SaveInventoryItemEnchantStateAsync(connection, transaction, playerObjectId, targetItemUpdate, cancellationToken))
				return false;

			if (deletedTargetItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedTargetItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			foreach (var supplementItemUpdate in supplementItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, supplementItemUpdate, cancellationToken))
					return false;
			}

			foreach (var deletedSupplementItemObjectId in deletedSupplementItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSupplementItemObjectId, cancellationToken))
					return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& deletedSourceItemObjectId != deletedTargetItemObjectId
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save enchant item mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveGodstoneSocketMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/item/ItemSocketService.socketGodstone delayed completion persists Item.addGodStone plus source consume.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await InventoryItemExistsAsync(connection, transaction, playerObjectId, targetItemUpdate.ObjectId, cancellationToken))
				return false;

			await SaveGodstoneAsync(connection, transaction, targetItemUpdate, cancellationToken);

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save godstone socket mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemAmplificationMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? materialItemUpdate,
		int? deletedMaterialItemObjectId,
		InventoryItem? toolItemUpdate,
		int? deletedToolItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/EnchantService.amplifyItem persists target is_amplified plus material/tool consume.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await using (var targetCommand = connection.CreateCommand())
			{
				targetCommand.Transaction = transaction;
				targetCommand.CommandText = "UPDATE inventory SET is_amplified = ? WHERE item_unique_id = ? AND item_owner = ?";
				targetCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = targetItemUpdate.IsAmplified },
						new MySqlParameter { Value = targetItemUpdate.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await targetCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (materialItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, materialItemUpdate, cancellationToken))
				return false;

			if (deletedMaterialItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedMaterialItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			if (toolItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, toolItemUpdate, cancellationToken))
				return false;

			if (deletedToolItemObjectId.HasValue
				&& deletedToolItemObjectId != deletedMaterialItemObjectId
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedToolItemObjectId.Value, cancellationToken))
			{
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item amplification mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveInventoryItemSlotAsync(
		int playerObjectId,
		int itemObjectId,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.store updates slot after ItemMoveService.moveInSameStorage.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE inventory SET slot = ? WHERE item_unique_id = ? AND item_owner = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = newSlot },
					new MySqlParameter { Value = itemObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save inventory slot update for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> TransferItemOwnershipAsync(
		int itemObjectId,
		int previousOwnerId,
		int newOwnerId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExchangeService.performTrade -> InventoryDAO.store moves the item row to the new owner.
		// The previous owner is matched in the WHERE clause so a stale transfer cannot overwrite an unrelated row.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE inventory SET item_owner = ?, item_location = ?, slot = ?, is_equipped = 0 WHERE item_unique_id = ? AND item_owner = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = newOwnerId },
					new MySqlParameter { Value = newLocation },
					new MySqlParameter { Value = newSlot },
					new MySqlParameter { Value = itemObjectId },
					new MySqlParameter { Value = previousOwnerId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not transfer item {ItemObjectId} ownership from {PreviousOwnerId} to {NewOwnerId}", itemObjectId, previousOwnerId, newOwnerId);
			return false;
		}
	}

	public async Task<bool> SavePrivateStorePurchaseMutationAsync(
		int buyerObjectId,
		int sellerObjectId,
		IReadOnlyList<InventoryItem> sellerUpdatedItems,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		IReadOnlyList<InventoryItem> buyerUpdatedItems,
		IReadOnlyList<InventoryItem> buyerAddedItems,
		InventoryItem? buyerKinahItem,
		InventoryItem? sellerKinahItem,
		bool sellerKinahWasCreated,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PrivateStoreService.sellStoreItem mutates seller inventory, buyer inventory,
		// and both kinah rows; InventoryDAO.store persists those dirty item rows.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in sellerUpdatedItems)
			{
				if (!await SavePrivateStoreSellerItemAsync(connection, transaction, sellerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var itemObjectId in sellerDeletedItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, sellerObjectId, itemObjectId, cancellationToken))
					return false;
			}

			foreach (var item in buyerUpdatedItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, buyerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in buyerAddedItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			if (buyerKinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, buyerObjectId, buyerKinahItem, cancellationToken))
				return false;

			if (sellerKinahItem != null)
			{
				if (sellerKinahWasCreated)
					await InsertInventoryItemAsync(connection, transaction, sellerKinahItem, cancellationToken);
				else if (!await SaveInventoryItemCountAsync(connection, transaction, sellerObjectId, sellerKinahItem, cancellationToken))
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not save private-store purchase mutation for buyer {BuyerObjectId} and seller {SellerObjectId}",
				buyerObjectId,
				sellerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveNpcShopBuyMutationAsync(
		int playerObjectId,
		PlayerAbyssRank? abyssRank,
		IReadOnlyList<InventoryItem> requiredItemUpdates,
		IReadOnlyList<int> deletedRequiredItemObjectIds,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? kinahItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: TradeService.performBuyTransaction decreases AP/kinah/required items, then
		// ItemService.addItem mutates Storage; AbyssRankDAO/InventoryDAO persist dirty rows.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (abyssRank != null)
				await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			if (kinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;

			foreach (var item in requiredItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var itemObjectId in deletedRequiredItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, itemObjectId, cancellationToken))
					return false;
			}

			foreach (var item in updatedItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save NPC shop buy mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveNpcShopSellMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		InventoryItem kinahItem,
		bool kinahWasCreated,
		CancellationToken cancellationToken = default)
	{
		// Java parity: TradeService.performSellToShop deletes/decreases the sold item,
		// then Storage.increaseKinah persists the seller's Kinah row through InventoryDAO.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in sellerItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var itemObjectId in sellerDeletedItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, itemObjectId, cancellationToken))
					return false;
			}

			if (kinahWasCreated)
				await InsertInventoryItemAsync(connection, transaction, kinahItem, cancellationToken);
			else if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save NPC shop sell mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveNpcShopApSellMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		IReadOnlyList<InventoryItem> sellerItemUpdates,
		IReadOnlyList<int> sellerDeletedItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: TradeService.performSellForAPToShop decreases inventory and
		// AbyssPointsService.addAp persists the player's Abyss rank state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			foreach (var item in sellerItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var itemObjectId in sellerDeletedItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, itemObjectId, cancellationToken))
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save NPC shop AP sell mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveNpcShopRepurchaseMutationAsync(
		int playerObjectId,
		InventoryItem? kinahItem,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: RepurchaseService.repurchaseFromShop decreases Kinah through
		// Storage.tryDecreaseKinah, restores the item through ItemService.addItem, and
		// keeps the repurchase set as runtime-only state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (kinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;

			foreach (var item in updatedItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save NPC shop repurchase mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveInventoryItemPackCountAsync(
		int playerObjectId,
		int itemObjectId,
		int newPackCount,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.store updates pack_count after CM_UNWRAP_ITEM.runImpl sets item.setPackCount(-packCount).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE inventory SET pack_count = ? WHERE item_unique_id = ? AND item_owner = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = newPackCount },
					new MySqlParameter { Value = itemObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save pack count update for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemSplitMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem newItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemSplitService.splitItem — decreases source item count and inserts the new split item atomically.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItem, cancellationToken))
				return false;

			await InsertInventoryItemAsync(connection, transaction, newItem, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item split mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemMergeMutationAsync(
		int playerObjectId,
		InventoryItem sourceItem,
		InventoryItem targetItem,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemSplitService.mergeStacks — decreases source count and increases target count atomically.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItem, cancellationToken))
				return false;
			if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, targetItem, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item merge mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemCrossStorageMoveMutationAsync(
		int playerObjectId,
		int itemObjectId,
		int newLocation,
		long newSlot,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.store updates item_location and slot after ItemMoveService.moveItem cross-storage.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE inventory SET item_location = ?, slot = ? WHERE item_unique_id = ? AND item_owner = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = newLocation },
					new MySqlParameter { Value = newSlot },
					new MySqlParameter { Value = itemObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save cross-storage item move for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveEquipmentMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> items,
		InventoryItem? kinahItem = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.store updated equipped flag and equipment slot after Equipment equip/unequip/switch.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in items)
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = "UPDATE inventory SET is_equipped = ?, is_soul_bound = ?, slot = ? WHERE item_unique_id = ? AND item_owner = ?";
				command.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = item.IsEquipped },
						new MySqlParameter { Value = item.IsSoulBound },
						new MySqlParameter { Value = item.Slot },
						new MySqlParameter { Value = item.ObjectId },
						new MySqlParameter { Value = playerObjectId },
					});
				if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (kinahItem != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItem, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save equipment mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePowerShardUseMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> countUpdateItems,
		IReadOnlyList<InventoryItem> equipUpdateItems,
		IReadOnlyList<int> deletedItemObjectIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/gameobjects/player/Equipment.decreaseEquippedItemCount + usePowerShard persistence.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in countUpdateItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var deletedObjectId in deletedItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedObjectId, cancellationToken))
					return false;
			}

			foreach (var item in equipUpdateItems)
			{
				if (!await SaveInventoryItemEquipmentStateAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save power shard use mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	private static async Task SavePlayerSettingsAsync(
		MySqlConnection connection,
		int playerObjectId,
		PlayerSettings settings,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerSettingsDAO.saveSettings.
		if (settings.UiSettings != null)
			await ReplacePlayerSettingAsync(connection, playerObjectId, 0, settings.UiSettings, cancellationToken);
		if (settings.Shortcuts != null)
			await ReplacePlayerSettingAsync(connection, playerObjectId, 1, settings.Shortcuts, cancellationToken);
		if (settings.HouseBuddies != null)
			await ReplacePlayerSettingAsync(connection, playerObjectId, 2, settings.HouseBuddies, cancellationToken);

		await ReplacePlayerSettingAsync(connection, playerObjectId, -1, settings.Display, cancellationToken);
		await ReplacePlayerSettingAsync(connection, playerObjectId, -2, settings.Deny, cancellationToken);
	}

	private static async Task SavePlayerAppearanceAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		CharacterAppearance appearance,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerAppearanceDAO.store.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			REPLACE INTO player_appearance (
				player_id, face, hair, deco, tattoo, face_contour, expression, jaw_line, skin_rgb, hair_rgb, lip_rgb, eye_rgb,
				face_shape, forehead, eye_height, eye_space, eye_width, eye_size, eye_shape, eye_angle,
				brow_height, brow_angle, brow_shape, nose, nose_bridge, nose_width, nose_tip, cheek, lip_height, mouth_size,
				lip_size, smile, lip_shape, jaw_height, chin_jut, ear_shape, head_size, neck, neck_length, shoulders,
				shoulder_size, torso, chest, waist, hips, arm_thickness, arm_length, hand_size, leg_thickness, leg_length,
				foot_size, facial_rate, voice, height)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
				?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = appearance.Face },
				new MySqlParameter { Value = appearance.Hair },
				new MySqlParameter { Value = appearance.Deco },
				new MySqlParameter { Value = appearance.Tattoo },
				new MySqlParameter { Value = appearance.FaceContour },
				new MySqlParameter { Value = appearance.Expression },
				new MySqlParameter { Value = appearance.JawLine },
				new MySqlParameter { Value = appearance.SkinRgb },
				new MySqlParameter { Value = appearance.HairRgb },
				new MySqlParameter { Value = appearance.LipRgb },
				new MySqlParameter { Value = appearance.EyeRgb },
				new MySqlParameter { Value = appearance.FaceShape },
				new MySqlParameter { Value = appearance.Forehead },
				new MySqlParameter { Value = appearance.EyeHeight },
				new MySqlParameter { Value = appearance.EyeSpace },
				new MySqlParameter { Value = appearance.EyeWidth },
				new MySqlParameter { Value = appearance.EyeSize },
				new MySqlParameter { Value = appearance.EyeShape },
				new MySqlParameter { Value = appearance.EyeAngle },
				new MySqlParameter { Value = appearance.BrowHeight },
				new MySqlParameter { Value = appearance.BrowAngle },
				new MySqlParameter { Value = appearance.BrowShape },
				new MySqlParameter { Value = appearance.Nose },
				new MySqlParameter { Value = appearance.NoseBridge },
				new MySqlParameter { Value = appearance.NoseWidth },
				new MySqlParameter { Value = appearance.NoseTip },
				new MySqlParameter { Value = appearance.Cheek },
				new MySqlParameter { Value = appearance.LipHeight },
				new MySqlParameter { Value = appearance.MouthSize },
				new MySqlParameter { Value = appearance.LipSize },
				new MySqlParameter { Value = appearance.Smile },
				new MySqlParameter { Value = appearance.LipShape },
				new MySqlParameter { Value = appearance.JawHeight },
				new MySqlParameter { Value = appearance.ChinJut },
				new MySqlParameter { Value = appearance.EarShape },
				new MySqlParameter { Value = appearance.HeadSize },
				new MySqlParameter { Value = appearance.Neck },
				new MySqlParameter { Value = appearance.NeckLength },
				new MySqlParameter { Value = appearance.Shoulders },
				new MySqlParameter { Value = appearance.ShoulderSize },
				new MySqlParameter { Value = appearance.Torso },
				new MySqlParameter { Value = appearance.Chest },
				new MySqlParameter { Value = appearance.Waist },
				new MySqlParameter { Value = appearance.Hips },
				new MySqlParameter { Value = appearance.ArmThickness },
				new MySqlParameter { Value = appearance.ArmLength },
				new MySqlParameter { Value = appearance.HandSize },
				new MySqlParameter { Value = appearance.LegThickness },
				new MySqlParameter { Value = appearance.LegLength },
				new MySqlParameter { Value = appearance.FootSize },
				new MySqlParameter { Value = appearance.FacialRate },
				new MySqlParameter { Value = appearance.Voice },
				new MySqlParameter { Value = appearance.Height },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task ReplacePlayerSettingAsync(
		MySqlConnection connection,
		int playerObjectId,
		int settingsType,
		object settingsValue,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = "REPLACE INTO player_settings VALUES (?, ?, ?)";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = settingsType },
				new MySqlParameter { Value = settingsValue },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task<bool> SaveInventoryItemCountAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: model/items/storage/Storage.decreaseKinah update path.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_count = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SavePrivateStoreSellerItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int sellerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_count = ?, pack_count = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.PackCount },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = sellerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemFullStateAsync(
		MySqlConnection connection,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/InventoryDAO.UPDATE_QUERY used by InventoryDAO.store(player).
		await using var command = connection.CreateCommand();
		command.CommandText = """
			UPDATE inventory
			SET item_count = ?, item_color = ?, color_expires = ?, item_creator = ?, expire_time = ?, activation_count = ?,
				item_owner = ?, is_equipped = ?, is_soul_bound = ?, slot = ?, item_location = ?, enchant = ?, enchant_bonus = ?,
				item_skin = ?, fusioned_item = ?, optional_socket = ?, optional_fusion_socket = ?, charge = ?, tune_count = ?,
				rnd_bonus = ?, fusion_rnd_bonus = ?, tempering = ?, pack_count = ?, is_amplified = ?, buff_skill = ?, rnd_plume_bonus = ?
			WHERE item_unique_id = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : DBNull.Value },
				new MySqlParameter { Value = item.ColorExpires },
				new MySqlParameter { Value = item.Creator ?? (object)DBNull.Value },
				new MySqlParameter { Value = item.ExpireTime },
				new MySqlParameter { Value = item.ActivationCount },
				new MySqlParameter { Value = item.OwnerId },
				new MySqlParameter { Value = item.IsEquipped },
				new MySqlParameter { Value = item.IsSoulBound },
				new MySqlParameter { Value = item.Slot },
				new MySqlParameter { Value = item.Location },
				new MySqlParameter { Value = item.Enchant },
				new MySqlParameter { Value = item.EnchantBonus },
				new MySqlParameter { Value = item.ItemSkin },
				new MySqlParameter { Value = item.FusionedItem },
				new MySqlParameter { Value = item.OptionalSocket },
				new MySqlParameter { Value = item.OptionalFusionSocket },
				new MySqlParameter { Value = item.Charge },
				new MySqlParameter { Value = item.TuneCount },
				new MySqlParameter { Value = item.RandomBonus },
				new MySqlParameter { Value = item.FusionRandomBonus },
				new MySqlParameter { Value = item.Tempering },
				new MySqlParameter { Value = item.PackCount },
				new MySqlParameter { Value = item.IsAmplified },
				new MySqlParameter { Value = item.BuffSkill },
				new MySqlParameter { Value = item.RandomPlumeBonus },
				new MySqlParameter { Value = item.ObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemTuneCountAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: Item.removeRemainingTuningCountIfPossible marks inventory item state dirty.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET tune_count = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.TuneCount },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemEnchantStateAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: services/EnchantService.setEnchantLevel plus InventoryDAO.store updated item fields.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			UPDATE inventory
			SET item_count = ?, enchant = ?, is_amplified = ?, tune_count = ?, buff_skill = ?
			WHERE item_unique_id = ? AND item_owner = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.Enchant },
				new MySqlParameter { Value = item.IsAmplified },
				new MySqlParameter { Value = item.TuneCount },
				new MySqlParameter { Value = item.BuffSkill },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemEquipmentStateAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET is_equipped = ?, is_soul_bound = ?, slot = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.IsEquipped },
				new MySqlParameter { Value = item.IsSoulBound },
				new MySqlParameter { Value = item.Slot },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemDyeStateAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: DyeAction.dyeItem marks item color/colorExpireTime dirty.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "UPDATE inventory SET item_color = ?, color_expires = ? WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : DBNull.Value },
				new MySqlParameter { Value = item.ColorExpires },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> SaveInventoryItemRemodelStateAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		// Java parity: ItemRemodelService.remodelItem marks item_skin and transferred item_color dirty.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			UPDATE inventory
			SET item_skin = ?, item_color = ?, color_expires = ?
			WHERE item_unique_id = ? AND item_owner = ?
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.ItemSkin },
				new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : DBNull.Value },
				new MySqlParameter { Value = item.ColorExpires },
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task<bool> InventoryItemExistsAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT COUNT(*) FROM inventory WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = itemObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
	}

	private static async Task SaveIdianStoneAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await DeleteIdianStoneAsync(connection, transaction, item.ObjectId, cancellationToken);
		if (item.IdianStone == null)
			return;

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES (?, ?, 0, 3, ?, ?, 0)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = item.IdianStone.ItemId },
				new MySqlParameter { Value = item.IdianStone.PolishNumber },
				new MySqlParameter { Value = item.IdianStone.PolishCharge },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task SaveGodstoneAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using (var deleteCommand = connection.CreateCommand())
		{
			deleteCommand.Transaction = transaction;
			deleteCommand.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ? AND slot = 0 AND category = 1";
			deleteCommand.Parameters.Add(new MySqlParameter { Value = item.ObjectId });
			await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		if (item.Godstone == null)
			return;

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES (?, ?, 0, 1, 0, 0, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = item.Godstone.ItemId },
				new MySqlParameter { Value = item.Godstone.ProcCount },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task SaveManastoneAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int itemObjectId,
		ItemStoneSocket addedStone,
		int category,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES (?, ?, ?, ?, 0, 0, 0)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = itemObjectId },
				new MySqlParameter { Value = addedStone.ItemId },
				new MySqlParameter { Value = addedStone.Slot },
				new MySqlParameter { Value = category },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task DeleteIdianStoneAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ? AND slot = 0 AND category = 3";
		command.Parameters.Add(new MySqlParameter { Value = itemObjectId });
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task InsertInventoryItemAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO inventory (
				item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
				item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin,
				fusioned_item, optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus,
				tempering, pack_count, is_amplified, buff_skill, rnd_plume_bonus
			)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = item.ObjectId },
				new MySqlParameter { Value = item.ItemId },
				new MySqlParameter { Value = item.Count },
				new MySqlParameter { Value = item.Color.HasValue ? item.Color.Value : DBNull.Value },
				new MySqlParameter { Value = item.ColorExpires },
				new MySqlParameter { Value = item.Creator ?? (object)DBNull.Value },
				new MySqlParameter { Value = item.ExpireTime },
				new MySqlParameter { Value = item.ActivationCount },
				new MySqlParameter { Value = item.OwnerId },
				new MySqlParameter { Value = item.IsEquipped },
				new MySqlParameter { Value = item.IsSoulBound },
				new MySqlParameter { Value = item.Slot },
				new MySqlParameter { Value = item.Location },
				new MySqlParameter { Value = item.Enchant },
				new MySqlParameter { Value = item.EnchantBonus },
				new MySqlParameter { Value = item.ItemSkin },
				new MySqlParameter { Value = item.FusionedItem },
				new MySqlParameter { Value = item.OptionalSocket },
				new MySqlParameter { Value = item.OptionalFusionSocket },
				new MySqlParameter { Value = item.Charge },
				new MySqlParameter { Value = item.TuneCount },
				new MySqlParameter { Value = item.RandomBonus },
				new MySqlParameter { Value = item.FusionRandomBonus },
				new MySqlParameter { Value = item.Tempering },
				new MySqlParameter { Value = item.PackCount },
				new MySqlParameter { Value = item.IsAmplified },
				new MySqlParameter { Value = item.BuffSkill },
				new MySqlParameter { Value = item.RandomPlumeBonus },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
		await InsertInventoryItemStonesAsync(connection, transaction, item, cancellationToken);
	}

	private static async Task ReplaceInventoryItemStonesAsync(
		MySqlConnection connection,
		MySqlTransaction? transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		await using (var deleteCommand = connection.CreateCommand())
		{
			deleteCommand.Transaction = transaction;
			deleteCommand.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ?";
			deleteCommand.Parameters.Add(new MySqlParameter { Value = item.ObjectId });
			await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await InsertInventoryItemStonesAsync(connection, transaction, item, cancellationToken);
	}

	internal static IReadOnlyList<ItemStonePersistenceRow> BuildItemStonePersistenceRows(InventoryItem item)
	{
		// Java parity: dao/ItemStoneListDAO.ItemStoneType ordinal mapping.
		var rows = new List<ItemStonePersistenceRow>();
		foreach (var stone in item.ManaStones)
			rows.Add(new ItemStonePersistenceRow(item.ObjectId, stone.ItemId, stone.Slot, Category: 0, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		if (item.Godstone != null)
			rows.Add(new ItemStonePersistenceRow(item.ObjectId, item.Godstone.ItemId, Slot: 0, Category: 1, PolishNumber: 0, PolishCharge: 0, ProcCount: item.Godstone.ProcCount));
		foreach (var stone in item.FusionStones)
			rows.Add(new ItemStonePersistenceRow(item.ObjectId, stone.ItemId, stone.Slot, Category: 2, PolishNumber: 0, PolishCharge: 0, ProcCount: 0));
		if (item.IdianStone != null)
		{
			rows.Add(new ItemStonePersistenceRow(
				item.ObjectId,
				item.IdianStone.ItemId,
				Slot: 0,
				Category: 3,
				PolishNumber: item.IdianStone.PolishNumber,
				PolishCharge: item.IdianStone.PolishCharge,
				ProcCount: 0));
		}

		return rows;
	}

	private static async Task InsertInventoryItemStonesAsync(
		MySqlConnection connection,
		MySqlTransaction? transaction,
		InventoryItem item,
		CancellationToken cancellationToken)
	{
		var rows = BuildItemStonePersistenceRows(item);
		if (rows.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO item_stones (item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count)
			VALUES (?, ?, ?, ?, ?, ?, ?)
			""";
		foreach (var row in rows)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = row.ItemObjectId },
					new MySqlParameter { Value = row.ItemId },
					new MySqlParameter { Value = row.Slot },
					new MySqlParameter { Value = row.Category },
					new MySqlParameter { Value = row.PolishNumber },
					new MySqlParameter { Value = row.PolishCharge },
					new MySqlParameter { Value = row.ProcCount },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task<bool> DeleteInventoryItemAsync(
		MySqlConnection connection,
		MySqlTransaction? transaction,
		int playerObjectId,
		int itemObjectId,
		CancellationToken cancellationToken)
	{
		await using (var stoneCommand = connection.CreateCommand())
		{
			stoneCommand.Transaction = transaction;
			stoneCommand.CommandText = "DELETE FROM item_stones WHERE item_unique_id = ?";
			stoneCommand.Parameters.Add(new MySqlParameter { Value = itemObjectId });
			await stoneCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "DELETE FROM inventory WHERE item_unique_id = ? AND item_owner = ?";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = itemObjectId },
				new MySqlParameter { Value = playerObjectId },
			});
		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static async Task SaveAbyssRankAsync(
		MySqlConnection connection,
		MySqlTransaction transaction,
		int playerObjectId,
		PlayerAbyssRank rank,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/AbyssRankDAO.storeAbyssRank.
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = """
			INSERT INTO abyss_rank (
				player_id, daily_ap, weekly_ap, ap, `rank`, max_rank, rank_pos, old_rank_pos,
				daily_kill, weekly_kill, all_kill, last_kill, last_ap, last_update, rank_ap,
				daily_gp, weekly_gp, gp, last_gp)
			VALUES (?, ?, ?, ?, ?, ?, ?, 0, ?, ?, ?, ?, ?, ?, 0, ?, ?, ?, ?)
			ON DUPLICATE KEY UPDATE
				daily_ap = VALUES(daily_ap),
				weekly_ap = VALUES(weekly_ap),
				ap = VALUES(ap),
				`rank` = VALUES(`rank`),
				max_rank = VALUES(max_rank),
				rank_pos = VALUES(rank_pos),
				daily_kill = VALUES(daily_kill),
				weekly_kill = VALUES(weekly_kill),
				all_kill = VALUES(all_kill),
				last_kill = VALUES(last_kill),
				last_ap = VALUES(last_ap),
				last_update = VALUES(last_update),
				daily_gp = VALUES(daily_gp),
				weekly_gp = VALUES(weekly_gp),
				gp = VALUES(gp),
				last_gp = VALUES(last_gp)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = rank.DailyAp },
				new MySqlParameter { Value = rank.WeeklyAp },
				new MySqlParameter { Value = rank.Ap },
				new MySqlParameter { Value = rank.Rank },
				new MySqlParameter { Value = rank.MaxRank },
				new MySqlParameter { Value = rank.RankingListPosition },
				new MySqlParameter { Value = rank.DailyKill },
				new MySqlParameter { Value = rank.WeeklyKill },
				new MySqlParameter { Value = rank.AllKill },
				new MySqlParameter { Value = rank.LastKill },
				new MySqlParameter { Value = rank.LastAp },
				new MySqlParameter { Value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				new MySqlParameter { Value = rank.DailyGp },
				new MySqlParameter { Value = rank.WeeklyGp },
				new MySqlParameter { Value = rank.Gp },
				new MySqlParameter { Value = rank.LastGp },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task SavePeriodicAbyssRankAsync(
		MySqlConnection connection,
		int playerObjectId,
		PlayerAbyssRank rank,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/AbyssRankDAO.storeAbyssRank insert/update columns; ranking list positions
		// are maintained by AbyssRankDAO.updateRankingLists, not periodic player saves.
		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO abyss_rank (
				player_id, daily_ap, weekly_ap, ap, `rank`, daily_kill, weekly_kill, all_kill,
				max_rank, last_kill, last_ap, last_update, daily_gp, weekly_gp, gp, last_gp)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
			ON DUPLICATE KEY UPDATE
				daily_ap = VALUES(daily_ap),
				weekly_ap = VALUES(weekly_ap),
				ap = VALUES(ap),
				`rank` = VALUES(`rank`),
				daily_kill = VALUES(daily_kill),
				weekly_kill = VALUES(weekly_kill),
				all_kill = VALUES(all_kill),
				max_rank = VALUES(max_rank),
				last_kill = VALUES(last_kill),
				last_ap = VALUES(last_ap),
				last_update = VALUES(last_update),
				daily_gp = VALUES(daily_gp),
				weekly_gp = VALUES(weekly_gp),
				gp = VALUES(gp),
				last_gp = VALUES(last_gp)
			""";
		command.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = rank.DailyAp },
				new MySqlParameter { Value = rank.WeeklyAp },
				new MySqlParameter { Value = rank.Ap },
				new MySqlParameter { Value = rank.Rank },
				new MySqlParameter { Value = rank.DailyKill },
				new MySqlParameter { Value = rank.WeeklyKill },
				new MySqlParameter { Value = rank.AllKill },
				new MySqlParameter { Value = rank.MaxRank },
				new MySqlParameter { Value = rank.LastKill },
				new MySqlParameter { Value = rank.LastAp },
				new MySqlParameter { Value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
				new MySqlParameter { Value = rank.DailyGp },
				new MySqlParameter { Value = rank.WeeklyGp },
				new MySqlParameter { Value = rank.Gp },
				new MySqlParameter { Value = rank.LastGp },
			});
		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task SavePeriodicPlayerSkillsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyList<PlayerSkill> skills,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerSkillListDAO.storeSkills persists deleted/current skills during
		// GeneralUpdateTask. C# snapshots the currently modeled live skill list.
		await using (var deleteCommand = connection.CreateCommand())
		{
			deleteCommand.CommandText = "DELETE FROM player_skills WHERE player_id = ?";
			deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
			await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		if (skills.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.CommandText = "REPLACE INTO player_skills (player_id, skill_id, skill_level) VALUES (?, ?, ?)";
		foreach (var skill in skills)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = skill.SkillId },
					new MySqlParameter { Value = skill.SkillLevel },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task SavePeriodicPlayerQuestsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyList<PlayerQuestState> quests,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerQuestListDAO.store(player) persists deleted/current quest state
		// during GeneralUpdateTask. C# snapshots the currently modeled live quest list.
		await using (var deleteCommand = connection.CreateCommand())
		{
			deleteCommand.CommandText = "DELETE FROM player_quests WHERE player_id = ?";
			deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
			await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		if (quests.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO player_quests
				(player_id, quest_id, status, quest_vars, flags, complete_count, next_repeat_time, reward, complete_time)
			VALUES
				(?, ?, ?, ?, ?, ?, ?, ?, ?)
			""";
		foreach (var quest in quests)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = quest.QuestId },
					new MySqlParameter { Value = quest.Status },
					new MySqlParameter { Value = quest.QuestVars },
					new MySqlParameter { Value = quest.Flags },
					new MySqlParameter { Value = quest.CompleteCount },
					new MySqlParameter { Value = quest.NextRepeatTime?.DateTime ?? (object)DBNull.Value },
					new MySqlParameter { Value = quest.RewardGroup.HasValue ? quest.RewardGroup.Value : DBNull.Value },
					new MySqlParameter { Value = quest.CompleteTime?.DateTime ?? (object)DBNull.Value },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task SavePeriodicPlayerHousesAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyList<PlayerHouse> houses,
		CancellationToken cancellationToken)
	{
		// Java parity: PlayerEnterWorldService.GeneralUpdateTask.run calls House.save,
		// which delegates to HousesDAO.storeHouse for NEW/UPDATE_REQUIRED house rows.
		// C# does not yet model house persistent state, so periodic saves snapshot loaded
		// player-owned rows through the existing Java houses table shape.
		if (houses.Count == 0)
			return;

		await using var command = connection.CreateCommand();
		command.CommandText = """
			INSERT INTO houses (id, address, building_id, player_id, acquire_time, settings, next_pay, sign_notice)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?)
			ON DUPLICATE KEY UPDATE
				address = VALUES(address),
				building_id = VALUES(building_id),
				player_id = VALUES(player_id),
				acquire_time = VALUES(acquire_time),
				settings = VALUES(settings),
				next_pay = VALUES(next_pay),
				sign_notice = VALUES(sign_notice)
			""";
		foreach (var house in houses)
		{
			command.Parameters.Clear();
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = house.ObjectId },
					new MySqlParameter { Value = house.AddressId },
					new MySqlParameter { Value = house.BuildingId },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = house.AcquiredTime.HasValue ? house.AcquiredTime.Value : DBNull.Value },
					new MySqlParameter { Value = PlayerHouse.CreateSettings(house.DoorState, house.ShowOwnerName) },
					new MySqlParameter { Value = house.NextPay.HasValue ? house.NextPay.Value : DBNull.Value },
					new MySqlParameter { Value = string.IsNullOrEmpty(house.SignNotice) ? DBNull.Value : house.SignNotice },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static CharacterAppearance ReadAppearance(MySqlDataReader reader)
	{
		// Java parity: dao/PlayerAppearanceDAO.loadPlayerAppearance.
		return new CharacterAppearance
		{
			Face = ReadInt(reader, "face"),
			Hair = ReadInt(reader, "hair"),
			Deco = ReadInt(reader, "deco"),
			Tattoo = ReadInt(reader, "tattoo"),
			FaceContour = ReadInt(reader, "face_contour"),
			Expression = ReadInt(reader, "expression"),
			JawLine = ReadInt(reader, "jaw_line"),
			SkinRgb = ReadInt(reader, "skin_rgb"),
			HairRgb = ReadInt(reader, "hair_rgb"),
			EyeRgb = ReadInt(reader, "eye_rgb"),
			LipRgb = ReadInt(reader, "lip_rgb"),
			FaceShape = ReadInt(reader, "face_shape"),
			Forehead = ReadInt(reader, "forehead"),
			EyeHeight = ReadInt(reader, "eye_height"),
			EyeSpace = ReadInt(reader, "eye_space"),
			EyeWidth = ReadInt(reader, "eye_width"),
			EyeSize = ReadInt(reader, "eye_size"),
			EyeShape = ReadInt(reader, "eye_shape"),
			EyeAngle = ReadInt(reader, "eye_angle"),
			BrowHeight = ReadInt(reader, "brow_height"),
			BrowAngle = ReadInt(reader, "brow_angle"),
			BrowShape = ReadInt(reader, "brow_shape"),
			Nose = ReadInt(reader, "nose"),
			NoseBridge = ReadInt(reader, "nose_bridge"),
			NoseWidth = ReadInt(reader, "nose_width"),
			NoseTip = ReadInt(reader, "nose_tip"),
			Cheek = ReadInt(reader, "cheek"),
			LipHeight = ReadInt(reader, "lip_height"),
			MouthSize = ReadInt(reader, "mouth_size"),
			LipSize = ReadInt(reader, "lip_size"),
			Smile = ReadInt(reader, "smile"),
			LipShape = ReadInt(reader, "lip_shape"),
			JawHeight = ReadInt(reader, "jaw_height"),
			ChinJut = ReadInt(reader, "chin_jut"),
			EarShape = ReadInt(reader, "ear_shape"),
			HeadSize = ReadInt(reader, "head_size"),
			Neck = ReadInt(reader, "neck"),
			NeckLength = ReadInt(reader, "neck_length"),
			Shoulders = ReadInt(reader, "shoulders"),
			ShoulderSize = ReadInt(reader, "shoulder_size"),
			Torso = ReadInt(reader, "torso"),
			Chest = ReadInt(reader, "chest"),
			Waist = ReadInt(reader, "waist"),
			Hips = ReadInt(reader, "hips"),
			ArmThickness = ReadInt(reader, "arm_thickness"),
			ArmLength = ReadInt(reader, "arm_length"),
			HandSize = ReadInt(reader, "hand_size"),
			LegThickness = ReadInt(reader, "leg_thickness"),
			LegLength = ReadInt(reader, "leg_length"),
			FootSize = ReadInt(reader, "foot_size"),
			FacialRate = ReadInt(reader, "facial_rate"),
			Voice = ReadInt(reader, "voice"),
			Height = ReadFloat(reader, "height"),
		};
	}

	private static async Task SavePlayerLifeStatsAsync(MySqlConnection connection, int playerObjectId, PlayerLifeStats lifeStats, CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerLifeStatsDAO.updatePlayerLifeStat, with insert fallback matching loadPlayerLifeStat.
		await using var updateCommand = connection.CreateCommand();
		updateCommand.CommandText = "UPDATE player_life_stats SET hp = ?, mp = ?, fp = ? WHERE player_id = ?";
		updateCommand.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = lifeStats.CurrentHp },
				new MySqlParameter { Value = lifeStats.CurrentMp },
				new MySqlParameter { Value = lifeStats.CurrentFp },
				new MySqlParameter { Value = playerObjectId },
			});
		if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0)
			return;

		await using var insertCommand = connection.CreateCommand();
		insertCommand.CommandText = "INSERT INTO player_life_stats (player_id, hp, mp, fp) VALUES (?, ?, ?, ?)";
		insertCommand.Parameters.AddRange(
			new[]
			{
				new MySqlParameter { Value = playerObjectId },
				new MySqlParameter { Value = lifeStats.CurrentHp },
				new MySqlParameter { Value = lifeStats.CurrentMp },
				new MySqlParameter { Value = lifeStats.CurrentFp },
			});
		await insertCommand.ExecuteNonQueryAsync(cancellationToken);
	}

	private static async Task SavePlayerSkillCooldownsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyDictionary<int, long> cooldowns,
		long nowMillis,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PlayerCooldownsDAO.storePlayerCooldowns.
		await using var deleteCommand = connection.CreateCommand();
		deleteCommand.CommandText = "DELETE FROM player_cooldowns WHERE player_id = ?";
		deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
		await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

		var activeCooldowns = cooldowns
			.Where(entry => entry.Value - nowMillis > 28_000)
			.ToArray();
		if (activeCooldowns.Length == 0)
			return;

		await using var insertCommand = connection.CreateCommand();
		insertCommand.CommandText = "INSERT INTO player_cooldowns (player_id, cooldown_id, reuse_delay) VALUES (?, ?, ?)";
		foreach (var (cooldownId, reuseDelay) in activeCooldowns)
		{
			insertCommand.Parameters.Clear();
			insertCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = cooldownId },
					new MySqlParameter { Value = reuseDelay },
				});
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task SavePlayerItemCooldownsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyDictionary<int, PlayerItemCooldown> cooldowns,
		long nowMillis,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/ItemCooldownsDAO.storeItemCooldowns.
		await using var deleteCommand = connection.CreateCommand();
		deleteCommand.CommandText = "DELETE FROM item_cooldowns WHERE player_id = ?";
		deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
		await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

		var activeCooldowns = cooldowns
			.Where(entry => entry.Value.ReuseTimeMillis - nowMillis > 30_000)
			.ToArray();
		if (activeCooldowns.Length == 0)
			return;

		await using var insertCommand = connection.CreateCommand();
		insertCommand.CommandText = "INSERT INTO item_cooldowns (player_id, delay_id, use_delay, reuse_time) VALUES (?, ?, ?, ?)";
		foreach (var entry in activeCooldowns)
		{
			insertCommand.Parameters.Clear();
			insertCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = entry.Key },
					new MySqlParameter { Value = entry.Value.UseDelaySeconds },
					new MySqlParameter { Value = entry.Value.ReuseTimeMillis },
				});
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	private static async Task SavePlayerHouseObjectCooldownsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyDictionary<int, long> cooldowns,
		long nowMillis,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/HouseObjectCooldownsDAO.storeHouseObjectCooldowns.
		await using var deleteCommand = connection.CreateCommand();
		deleteCommand.CommandText = "DELETE FROM house_object_cooldowns WHERE player_id = ?";
		deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
		await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

		var activeCooldowns = cooldowns
			.Where(entry => entry.Value > nowMillis)
			.ToArray();
		if (activeCooldowns.Length == 0)
			return;

		await using var insertCommand = connection.CreateCommand();
		insertCommand.CommandText = "INSERT INTO house_object_cooldowns (player_id, object_id, reuse_time) VALUES (?, ?, ?)";
		foreach (var (objectId, reuseTime) in activeCooldowns)
		{
			insertCommand.Parameters.Clear();
			insertCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = objectId },
					new MySqlParameter { Value = reuseTime },
				});
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	public async Task<bool> SavePlayerPortalCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, PlayerPortalCooldown> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PortalCooldownsDAO.storePortalCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await SavePlayerPortalCooldownsAsync(
				connection,
				playerObjectId,
				cooldowns,
				nowMillis ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save portal cooldowns for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerCraftCooldownsAsync(
		int playerObjectId,
		IReadOnlyDictionary<int, long> cooldowns,
		long? nowMillis = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/CraftCooldownsDAO.storeCraftCooldowns deletes with one connection,
		// then opens one new connection per active insert and logs SQL errors without
		// propagating them.
		var effectiveNowMillis = nowMillis ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		await DeleteCraftCooldownsJavaStyleAsync(playerObjectId, cancellationToken);
		foreach (var (delayId, reuseTime) in cooldowns)
		{
			if (reuseTime < effectiveNowMillis)
				continue;

			await InsertCraftCooldownJavaStyleAsync(playerObjectId, delayId, reuseTime, cancellationToken);
		}

		return true;
	}

	private async Task DeleteCraftCooldownsJavaStyleAsync(
		int playerObjectId,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = CraftCooldownPersistencePlanService.JavaCraftCooldownDeleteSql;
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (MySqlException ex)
		{
			_logger.LogError(ex, "Couldn't delete craft cooldowns for player {PlayerObjectId}", playerObjectId);
		}
	}

	private async Task InsertCraftCooldownJavaStyleAsync(
		int playerObjectId,
		int delayId,
		long reuseTime,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = CraftCooldownPersistencePlanService.JavaCraftCooldownInsertSql;
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = delayId },
					new MySqlParameter { Value = reuseTime },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (MySqlException ex)
		{
			_logger.LogError(
				ex,
				"Couldn't store craft cooldown {DelayId} for player {PlayerObjectId}",
				delayId,
				playerObjectId);
		}
	}

	private static async Task SavePlayerPortalCooldownsAsync(
		MySqlConnection connection,
		int playerObjectId,
		IReadOnlyDictionary<int, PlayerPortalCooldown> cooldowns,
		long nowMillis,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/PortalCooldownsDAO.storePortalCooldowns deletes all rows, then inserts active cooldowns.
		await using var deleteCommand = connection.CreateCommand();
		deleteCommand.CommandText = "DELETE FROM portal_cooldowns WHERE player_id = ?";
		deleteCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
		await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

		var activeCooldowns = cooldowns
			.Where(entry => entry.Value.ReuseTimeMillis > nowMillis)
			.ToArray();
		if (activeCooldowns.Length == 0)
			return;

		await using var insertCommand = connection.CreateCommand();
		insertCommand.CommandText = "INSERT INTO portal_cooldowns (player_id, world_id, reuse_time, entry_count) VALUES (?, ?, ?, ?)";
		foreach (var entry in activeCooldowns)
		{
			insertCommand.Parameters.Clear();
			insertCommand.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = entry.Key },
					new MySqlParameter { Value = entry.Value.ReuseTimeMillis },
					new MySqlParameter { Value = entry.Value.EntryCount },
				});
			await insertCommand.ExecuteNonQueryAsync(cancellationToken);
		}
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadPlayerItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.loadStorage for StorageType.CUBE.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
					item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin, fusioned_item,
					optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus, tempering, pack_count,
					is_amplified, buff_skill, rnd_plume_bonus
				FROM inventory
				WHERE item_owner = ? AND item_location = 0
				ORDER BY item_location, slot, item_unique_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var items = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
					items.Add(ReadItem(reader));
			}

			await LoadItemStonesForItemsAsync(connection, items, cancellationToken);
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load inventory items for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<InventoryItem>();
		}
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadPlayerWarehouseItemsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.loadStorage for StorageType.REGULAR_WAREHOUSE.
		return await LoadStorageItemsAsync(
			ownerId: playerObjectId,
			location: 1,
			playerObjectId,
			"regular warehouse",
			cancellationToken);
	}

	public async Task<IReadOnlyList<InventoryItem>> LoadAccountWarehouseItemsAsync(int accountId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/AccountService.loadAccountWarehouse + InventoryDAO.loadStorage(accountId, ACCOUNT_WAREHOUSE).
		return await LoadStorageItemsAsync(
			ownerId: accountId,
			location: 2,
			accountId,
			"account warehouse",
			cancellationToken);
	}

	public async Task<IReadOnlyList<PlayerSkill>> LoadPlayerSkillsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerSkillListDAO.loadSkillList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT skill_id, skill_level FROM player_skills WHERE player_id = ? ORDER BY skill_id";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var skills = new List<PlayerSkill>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				skills.Add(
					new PlayerSkill
					{
						SkillId = ReadInt(reader, "skill_id"),
						SkillLevel = ReadInt(reader, "skill_level"),
						SkillType = 0,
					});
			}

			return skills;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load skill list for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerSkill>();
		}
	}

	public async Task<IReadOnlyDictionary<int, long>> LoadPlayerSkillCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerCooldownsDAO.loadPlayerCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT cooldown_id, reuse_delay FROM player_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, long>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseDelay = ReadLong(reader, "reuse_delay");
				if (reuseDelay <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "cooldown_id")] = reuseDelay;
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load skill cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, long>();
		}
	}

	public async Task<IReadOnlyDictionary<int, PlayerItemCooldown>> LoadPlayerItemCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/ItemCooldownsDAO.loadItemCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT delay_id, use_delay, reuse_time FROM item_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, PlayerItemCooldown>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "delay_id")] = new PlayerItemCooldown(
					reuseTime,
					ReadInt(reader, "use_delay"));
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load item cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, PlayerItemCooldown>();
		}
	}

	public async Task<IReadOnlyList<PlayerQuestState>> LoadPlayerQuestsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerQuestListDAO.load.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT quest_id, status, quest_vars, flags, complete_count, next_repeat_time, reward, complete_time
				FROM player_quests
				WHERE player_id = ?
				ORDER BY quest_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var quests = new List<PlayerQuestState>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				quests.Add(
					new PlayerQuestState(
						ReadInt(reader, "quest_id"),
						ReadString(reader, "status"),
						ReadInt(reader, "quest_vars"),
						ReadInt(reader, "flags"),
						ReadInt(reader, "complete_count"),
						ReadNullableInt(reader, "reward"),
						ReadDateTimeOffset(reader, "next_repeat_time"),
						ReadDateTimeOffset(reader, "complete_time")));
			}

			return quests;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load quests for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerQuestState>();
		}
	}

	public async Task<bool> DeletePlayerQuestAsync(int playerObjectId, int questId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerQuestListDAO.deleteQuest for QuestStateList deleted quest ids.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_quests WHERE player_id = ? AND quest_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = questId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) >= 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete quest {QuestId} for player {PlayerObjectId}", questId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> InsertPlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerQuestListDAO.addQuests inserts NEW quest states.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				INSERT INTO player_quests
					(player_id, quest_id, status, quest_vars, flags, complete_count, next_repeat_time, reward, complete_time)
				VALUES
					(?, ?, ?, ?, ?, ?, ?, ?, ?)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = questState.QuestId },
					new MySqlParameter { Value = questState.Status },
					new MySqlParameter { Value = questState.QuestVars },
					new MySqlParameter { Value = questState.Flags },
					new MySqlParameter { Value = questState.CompleteCount },
					new MySqlParameter { Value = questState.NextRepeatTime?.DateTime ?? (object)DBNull.Value },
					new MySqlParameter { Value = questState.RewardGroup.HasValue ? questState.RewardGroup.Value : DBNull.Value },
					new MySqlParameter { Value = questState.CompleteTime?.DateTime ?? (object)DBNull.Value },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not insert quest {QuestId} for player {PlayerObjectId}", questState.QuestId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> UpdatePlayerQuestAsync(int playerObjectId, PlayerQuestState questState, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerQuestListDAO.updateQuests.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE player_quests
				SET status = ?,
					quest_vars = ?,
					flags = ?,
					complete_count = ?,
					next_repeat_time = ?,
					reward = ?,
					complete_time = ?
				WHERE player_id = ? AND quest_id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = questState.Status },
					new MySqlParameter { Value = questState.QuestVars },
					new MySqlParameter { Value = questState.Flags },
					new MySqlParameter { Value = questState.CompleteCount },
					new MySqlParameter { Value = questState.NextRepeatTime?.DateTime ?? (object)DBNull.Value },
					new MySqlParameter { Value = questState.RewardGroup.HasValue ? questState.RewardGroup.Value : DBNull.Value },
					new MySqlParameter { Value = questState.CompleteTime?.DateTime ?? (object)DBNull.Value },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = questState.QuestId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) >= 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update quest {QuestId} for player {PlayerObjectId}", questState.QuestId, playerObjectId);
			return false;
		}
	}

	public async Task<PlayerNpcFactionsSnapshot> LoadPlayerNpcFactionsAsync(
		int playerObjectId,
		NpcFactionTable npcFactions,
		int currentEpochSeconds = 0,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerNpcFactionsDAO.loadNpcFactions plus NpcFaction constructor mentor lookup.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT faction_id, active, time, state, quest_id
				FROM player_npc_factions
				WHERE player_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var factions = new List<PlayerNpcFactionState>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var factionId = ReadInt(reader, "faction_id");
				var template = npcFactions.GetNpcFactionById(factionId);
				if (template == null)
					throw new InvalidOperationException($"Missing NPC faction template {factionId}.");

				factions.Add(
					new PlayerNpcFactionState(
						factionId,
						reader.GetBoolean("active"),
						template.IsMentor,
						ReadInt(reader, "time"),
						ParseNpcFactionQuestState(ReadString(reader, "state")),
						ReadInt(reader, "quest_id")));
			}

			return new PlayerNpcFactionsSnapshot(factions, currentEpochSeconds);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not restore NPC faction data for player {PlayerObjectId}", playerObjectId);
			return PlayerNpcFactionsSnapshot.Empty;
		}
	}

	private static PlayerNpcFactionQuestState ParseNpcFactionQuestState(string state)
	{
		return state switch
		{
			"NOTING" => PlayerNpcFactionQuestState.Noting,
			"START" => PlayerNpcFactionQuestState.Start,
			"COMPLETE" => PlayerNpcFactionQuestState.Complete,
			_ => throw new InvalidOperationException($"Unknown NPC faction quest state '{state}'."),
		};
	}

	public async Task<bool> UpdatePlayerNpcFactionAsync(int playerObjectId, PlayerNpcFactionState factionState, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerNpcFactionsDAO.updateNpcFaction.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE player_npc_factions
				SET active = ?,
					time = ?,
					state = ?,
					quest_id = ?
				WHERE player_id = ? AND faction_id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = factionState.IsActive },
					new MySqlParameter { Value = factionState.TimeEpochSeconds },
					new MySqlParameter { Value = ToNpcFactionQuestStateValue(factionState.State) },
					new MySqlParameter { Value = factionState.QuestId },
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = factionState.FactionId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) >= 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update NPC faction {FactionId} for player {PlayerObjectId}", factionState.FactionId, playerObjectId);
			return false;
		}
	}

	private static string ToNpcFactionQuestStateValue(PlayerNpcFactionQuestState state)
	{
		return state switch
		{
			PlayerNpcFactionQuestState.Noting => "NOTING",
			PlayerNpcFactionQuestState.Start => "START",
			PlayerNpcFactionQuestState.Complete => "COMPLETE",
			_ => throw new InvalidOperationException($"Unknown NPC faction quest state '{state}'."),
		};
	}

	public async Task<IReadOnlyList<PlayerTitle>> LoadPlayerTitlesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerTitleListDAO.loadTitleList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT title_id, remaining
				FROM player_titles
				WHERE player_id = ?
				ORDER BY title_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var titles = new List<PlayerTitle>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				titles.Add(new PlayerTitle(ReadInt(reader, "title_id"), ReadInt(reader, "remaining")));

			return titles;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load titles for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerTitle>();
		}
	}

	public async Task<IReadOnlyList<PlayerMotion>> LoadPlayerMotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MotionDAO.loadMotionList.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT motion_id, active, time
				FROM player_motions
				WHERE player_id = ?
				ORDER BY motion_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var motions = new List<PlayerMotion>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				motions.Add(
					new PlayerMotion(
						ReadInt(reader, "motion_id"),
						ReadInt(reader, "time"),
						ReadBoolean(reader, "active")));
			}

			return motions;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load motions for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMotion>();
		}
	}

	public async Task<IReadOnlyList<PlayerEmotion>> LoadPlayerEmotionsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerEmotionListDAO.loadEmotions.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT emotion, remaining
				FROM player_emotions
				WHERE player_id = ?
				ORDER BY emotion
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var emotions = new List<PlayerEmotion>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				emotions.Add(new PlayerEmotion(ReadInt(reader, "emotion"), ReadInt(reader, "remaining")));

			return emotions;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load emotions for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerEmotion>();
		}
	}

	public async Task<IReadOnlyList<int>> LoadPlayerRecipesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRecipesDAO.load.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT recipe_id
				FROM player_recipes
				WHERE player_id = ?
				ORDER BY recipe_id
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var recipes = new List<int>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				recipes.Add(ReadInt(reader, "recipe_id"));

			return recipes;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load recipes for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<int>();
		}
	}

	public async Task<bool> DeletePlayerRecipeAsync(int playerObjectId, int recipeId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerRecipesDAO.delRecipe.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_recipes WHERE player_id = ? AND recipe_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = recipeId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) >= 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete recipe {RecipeId} for player {PlayerObjectId}", recipeId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> DeletePlayerEmotionAsync(int playerObjectId, int emotionId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerEmotionListDAO.deleteEmotion.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_emotions WHERE player_id = ? AND emotion = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = emotionId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete emotion {EmotionId} for player {PlayerObjectId}", emotionId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> DeletePlayerTitleAsync(int playerObjectId, int titleId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerTitleListDAO.removeTitle.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_titles WHERE player_id = ? AND title_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = titleId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete title {TitleId} for player {PlayerObjectId}", titleId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> DeletePlayerMotionAsync(int playerObjectId, int motionId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MotionDAO.deleteMotion.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_motions WHERE player_id = ? AND motion_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = motionId },
				});
			await command.ExecuteNonQueryAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete motion {MotionId} for player {PlayerObjectId}", motionId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> DeleteInventoryItemAsync(int itemOwnerId, int itemObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/InventoryDAO.store deleted Item, including item_stones cleanup.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
			var deleted = await DeleteInventoryItemAsync(connection, transaction, itemOwnerId, itemObjectId, cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return deleted;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete inventory item {ItemObjectId} for owner {ItemOwnerId}", itemObjectId, itemOwnerId);
			return false;
		}
	}

	public async Task<bool> SaveItemUseSourceMutationAsync(
		int playerObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: model/templates/item/actions/ToyPetSpawnAction.act -> inventory.decreaseByObjectId.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item-use source mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveCraftLearnActionMutationAsync(
		int playerObjectId,
		int recipeId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CraftLearnAction.act -> PlayerRecipesDAO.addRecipe + inventory.decreaseByObjectId.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = "INSERT INTO player_recipes (player_id, recipe_id) VALUES (?, ?)";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = recipeId },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save craft-learn action for player {PlayerObjectId} and recipe {RecipeId}", playerObjectId, recipeId);
			return false;
		}
	}

	public async Task<bool> SaveEmotionLearnActionMutationAsync(
		int playerObjectId,
		PlayerEmotion emotion,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: EmotionLearnAction.act -> PlayerEmotionListDAO.insertEmotion + inventory.delete/decrease.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = "INSERT INTO player_emotions (player_id, emotion, remaining) VALUES (?, ?, ?)";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = emotion.Id },
					new MySqlParameter { Value = emotion.ExpireTimeSeconds },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save emotion-learn action for player {PlayerObjectId} and emotion {EmotionId}", playerObjectId, emotion.Id);
			return false;
		}
	}

	public async Task<bool> SaveTitleAddActionMutationAsync(
		int playerObjectId,
		PlayerTitle title,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: TitleAddAction.act -> PlayerTitleListDAO.storeTitles + inventory.delete/decrease.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = "INSERT INTO player_titles (player_id, title_id, remaining) VALUES (?, ?, ?)";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = title.Id },
					new MySqlParameter { Value = title.ExpireTimeSeconds },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save title-add action for player {PlayerObjectId} and title {TitleId}", playerObjectId, title.Id);
			return false;
		}
	}

	public async Task<bool> SaveSkillLearnActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerSkill> skills,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: SkillLearnAction.act -> PlayerSkillListDAO.storeSkills plus inventory.delete/decrease.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var skill in skills)
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = "REPLACE INTO player_skills (player_id, skill_id, skill_level) VALUES (?, ?, ?)";
				command.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = skill.SkillId },
						new MySqlParameter { Value = skill.SkillLevel },
					});
				if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save skill-learn action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveInventoryExpansionMutationAsync(
		int playerObjectId,
		int itemExpands,
		int warehouseBonusExpands,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExpandInventoryAction.act -> inventory.decreaseByObjectId followed by
		// CubeExpandService.itemExpand or WarehouseService.expand(player, false).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = "UPDATE players SET item_expands = ?, wh_bonus_expands = ? WHERE id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = itemExpands },
					new MySqlParameter { Value = warehouseBonusExpands },
					new MySqlParameter { Value = playerObjectId },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save inventory expansion action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveDyeItemActionMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: DyeAction.dyeItem -> inventory.decreaseByObjectId + item color persistence.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await SaveInventoryItemDyeStateAsync(connection, transaction, playerObjectId, targetItemUpdate, cancellationToken))
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save dye item action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveAnimationAddActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<PlayerMotion> motions,
		IReadOnlyList<int> deactivatedMotionIds,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: AnimationAddAction.run -> MotionDAO.storeMotion plus inventory.decreaseItemCount.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var motionId in deactivatedMotionIds)
			{
				await using var updateCommand = connection.CreateCommand();
				updateCommand.Transaction = transaction;
				updateCommand.CommandText = "UPDATE player_motions SET active = ? WHERE player_id = ? AND motion_id = ?";
				updateCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = false },
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = motionId },
					});
				await updateCommand.ExecuteNonQueryAsync(cancellationToken);
			}

			foreach (var motion in motions)
			{
				await using var insertCommand = connection.CreateCommand();
				insertCommand.Transaction = transaction;
				insertCommand.CommandText = "REPLACE INTO player_motions (player_id, motion_id, active, time) VALUES (?, ?, ?, ?)";
				insertCommand.Parameters.AddRange(
					new[]
					{
						new MySqlParameter { Value = playerObjectId },
						new MySqlParameter { Value = motion.Id },
						new MySqlParameter { Value = motion.IsActive },
						new MySqlParameter { Value = motion.ExpireTimeSeconds },
					});
				if (await insertCommand.ExecuteNonQueryAsync(cancellationToken) <= 0)
					return false;
			}

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save animation-add action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveCosmeticItemActionMutationAsync(
		int playerObjectId,
		CharacterAppearance appearance,
		int deletedItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: CosmeticItemAction.act -> PlayerAppearanceDAO.store + Inventory.delete(targetItem).
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await SavePlayerAppearanceAsync(connection, transaction, playerObjectId, appearance, cancellationToken);
			if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedItemObjectId, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save cosmetic item action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveDecomposeActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedItems,
		IReadOnlyList<InventoryItem> addedItems,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: DecomposeAction/CM_SELECT_DECOMPOSABLE source consume plus ItemService.addItem rewards.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			foreach (var item in updatedItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save decompose action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveAssemblyItemActionMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedPartItems,
		IReadOnlyList<int> deletedPartObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: AssemblyItemAction part consumption plus ItemService.addItem reward mutation.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in updatedPartItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var deletedPartObjectId in deletedPartObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedPartObjectId, cancellationToken))
					return false;
			}

			foreach (var item in updatedRewardItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedRewardItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save assembly item action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveInventoryRewardMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemService.addItem persists reward stack updates and inserted cube rows.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in updatedRewardItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			foreach (var item in addedRewardItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save inventory reward mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> UpdateAccountPassportRewardedAsync(int accountId, PlayerPassport passport, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/AccountPassportsDAO.updatePassport for PersistentState.UPDATE_REQUIRED.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE account_passports
				SET rewarded = ?
				WHERE account_id = ? AND passport_id = ? AND arrive_date = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = passport.Rewarded ? 1 : 0 },
					new MySqlParameter { Value = accountId },
					new MySqlParameter { Value = passport.PassportId },
					new MySqlParameter { Value = passport.ArriveDate },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Could not update account passport {PassportId} for account {AccountId}",
				passport.PassportId,
				accountId);
			return false;
		}
	}

	public async Task<bool> SaveExpExtractActionMutationAsync(
		int playerObjectId,
		long newExp,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ExpExtractAction stores PlayerCommonData.exp, consumes source item, then ItemService.addItem reward.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			await using var expCommand = connection.CreateCommand();
			expCommand.Transaction = transaction;
			expCommand.CommandText = "UPDATE players SET exp = ? WHERE id = ?";
			expCommand.Parameters.Add(new MySqlParameter { Value = newExp });
			expCommand.Parameters.Add(new MySqlParameter { Value = playerObjectId });
			await expCommand.ExecuteNonQueryAsync(cancellationToken);

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			foreach (var item in updatedRewardItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedRewardItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save experience extraction action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveApExtractActionMutationAsync(
		int playerObjectId,
		PlayerAbyssRank abyssRank,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		int deletedTargetItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ApExtractAction inventory.delete(target), inventory.decreaseByObjectId(source), AbyssRankDAO.storeAbyssRank.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedTargetItemObjectId, cancellationToken))
				return false;

			if (sourceItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
				return false;

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
				return false;

			await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save AP extraction action for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemPurificationMutationAsync(
		int playerObjectId,
		IReadOnlyList<InventoryItem> materialItemUpdates,
		IReadOnlyList<int> deletedMaterialItemObjectIds,
		InventoryItem? baseItemUpdate,
		int? deletedBaseItemObjectId,
		IReadOnlyList<InventoryItem> updatedTargetItems,
		IReadOnlyList<InventoryItem> addedTargetItems,
		PlayerAbyssRank? abyssRank,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemPurificationService.decreaseMaterials consumes materials/AP/base item,
		// then upgradeItem adds the target item. This keeps the C# DB write set in one transaction.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			foreach (var item in materialItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var deletedObjectId in deletedMaterialItemObjectIds)
			{
				if (!await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedObjectId, cancellationToken))
					return false;
			}

			if (baseItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, baseItemUpdate, cancellationToken))
				return false;

			if (deletedBaseItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedBaseItemObjectId.Value, cancellationToken))
				return false;

			foreach (var item in updatedTargetItems)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, item, cancellationToken))
					return false;
			}

			foreach (var item in addedTargetItems)
				await InsertInventoryItemAsync(connection, transaction, item, cancellationToken);

			if (abyssRank != null)
				await SaveAbyssRankAsync(connection, transaction, playerObjectId, abyssRank, cancellationToken);

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item purification mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<bool> SaveItemRemodelMutationAsync(
		int playerObjectId,
		InventoryItem targetItemUpdate,
		InventoryItem kinahItemUpdate,
		InventoryItem? extractItemUpdate,
		int? deletedExtractItemObjectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ItemRemodelService.remodelItem target skin/color update, Kinah payment, and extract consumption.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (!await SaveInventoryItemRemodelStateAsync(connection, transaction, playerObjectId, targetItemUpdate, cancellationToken))
				return false;

			if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, kinahItemUpdate, cancellationToken))
				return false;

			if (extractItemUpdate != null && !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, extractItemUpdate, cancellationToken))
				return false;

			if (deletedExtractItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedExtractItemObjectId.Value, cancellationToken))
				return false;

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save item remodel mutation for player {PlayerObjectId}", playerObjectId);
			return false;
		}
	}

	public async Task<IReadOnlyList<PlayerMacro>> LoadPlayerMacrosAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerMacrosDAO.loadMacros.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT `order`, `macro` FROM player_macrosses WHERE player_id = ? ORDER BY `order`";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var macros = new List<PlayerMacro>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				macros.Add(new PlayerMacro(ReadInt(reader, "order"), ReadString(reader, "macro")));
			return macros;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load macros for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMacro>();
		}
	}

	public async Task<bool> SavePlayerMacroAsync(int playerObjectId, PlayerMacro macro, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerMacrosDAO.addMacro/updateMacro.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				INSERT INTO player_macrosses (player_id, `order`, macro)
				VALUES (?, ?, ?)
				ON DUPLICATE KEY UPDATE macro = VALUES(macro)
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = macro.Id },
					new MySqlParameter { Value = macro.Xml },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save macro {MacroId} for player {PlayerObjectId}", macro.Id, playerObjectId);
			return false;
		}
	}

	public async Task<bool> DeletePlayerMacroAsync(int playerObjectId, int macroId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerMacrosDAO.deleteMacro.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_macrosses WHERE player_id = ? AND `order` = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = macroId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) >= 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete macro {MacroId} for player {PlayerObjectId}", macroId, playerObjectId);
			return false;
		}
	}

	public async Task<IReadOnlyList<PlayerMail>> LoadPlayerMailboxAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/MailDAO.loadPlayerMailbox.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					m.mail_unique_id, m.mail_recipient_id, m.sender_name, m.mail_title, m.mail_message, m.unread,
					m.attached_item_id, COALESCE(i.item_id, 0) AS attached_item_template_id,
					m.attached_kinah_count, m.express, m.recieved_time,
					i.item_unique_id, i.item_id, i.item_count, i.item_color, i.color_expires, i.item_creator, i.expire_time, i.activation_count,
					i.item_owner, i.is_equipped, i.is_soul_bound, i.slot, i.item_location, i.enchant, i.enchant_bonus, i.item_skin, i.fusioned_item,
					i.optional_socket, i.optional_fusion_socket, i.charge, i.tune_count, i.rnd_bonus, i.fusion_rnd_bonus, i.tempering, i.pack_count,
					i.is_amplified, i.buff_skill, i.rnd_plume_bonus
				FROM mail m
				LEFT JOIN inventory i ON i.item_unique_id = m.attached_item_id AND i.item_owner = m.mail_recipient_id AND i.item_location = 127
				WHERE m.mail_recipient_id = ?
				ORDER BY m.recieved_time
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var mailbox = new List<PlayerMail>();
			var attachedItems = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					InventoryItem? attachedItem = null;
					if (!reader.IsDBNull(reader.GetOrdinal("item_unique_id")))
					{
						attachedItem = ReadItem(reader);
						attachedItems.Add(attachedItem);
					}

					mailbox.Add(
						new PlayerMail(
							ReadInt(reader, "mail_unique_id"),
							ReadInt(reader, "mail_recipient_id"),
							ReadString(reader, "sender_name"),
							ReadString(reader, "mail_title"),
							ReadString(reader, "mail_message"),
							ReadBoolean(reader, "unread"),
							ReadInt(reader, "attached_item_id"),
							attachedItem?.ItemId ?? ReadInt(reader, "attached_item_template_id"),
							ReadLong(reader, "attached_kinah_count"),
							ReadInt(reader, "express"),
							ReadDateTime(reader, "recieved_time") ?? DateTime.MinValue,
							attachedItem));
				}
			}

			await LoadItemStonesForItemsAsync(connection, attachedItems, cancellationToken);
			return mailbox;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load mailbox for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerMail>();
		}
	}

	public async Task<PlayerBrokerSettlementSummary> LoadBrokerSettlementsAsync(
		int playerObjectId,
		string race,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/BrokerService.onPlayerLogin.
		var brokerRace = GetBrokerRace(race);
		if (brokerRace == null)
			return PlayerBrokerSettlementSummary.Empty;

		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT COUNT(*) AS settled_count,
					COALESCE(SUM(CASE WHEN is_sold THEN price * item_count ELSE 0 END), 0) AS earned_kinah
				FROM broker
				WHERE seller_id = ? AND broker_race = ? AND is_settled = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = playerObjectId },
					new MySqlParameter { Value = brokerRace },
					new MySqlParameter { Value = true },
				});

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return PlayerBrokerSettlementSummary.Empty;

			return new PlayerBrokerSettlementSummary(
				ReadInt(reader, "settled_count"),
				ReadLong(reader, "earned_kinah"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load broker settlement summary for player {PlayerObjectId}", playerObjectId);
			return PlayerBrokerSettlementSummary.Empty;
		}
	}

	public async Task<IReadOnlyList<PlayerHouse>> LoadPlayerHousesAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: services/HousingService.findPlayerHouses with HousesDAO.loadHouses startup state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, address, building_id, acquire_time, next_pay, settings, sign_notice
				FROM houses
				WHERE player_id = ?
				ORDER BY acquire_time, address
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var houses = new List<PlayerHouse>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
				{
					houses.Add(
						new PlayerHouse(
							ReadInt(reader, "id"),
							ReadInt(reader, "address"),
							ReadInt(reader, "building_id"),
							ReadDateTime(reader, "acquire_time"),
							ReadDateTime(reader, "next_pay"),
							IsInactive: false,
							PlayerHouse.GetDoorStateFromSettings(ReadInt(reader, "settings")),
							PlayerHouse.GetShowOwnerNameFromSettings(ReadInt(reader, "settings")),
							ReadString(reader, "sign_notice")));
				}
			}
			houses = await AttachTownLevelsAsync(connection, houses, cancellationToken);
			houses = await AttachHouseScriptsAsync(connection, houses, cancellationToken);

			var studio = houses.FirstOrDefault(IsStudioAddress);
			if (studio != null)
				return [studio];

			var ordered = houses
				.OrderBy(house => house.AcquiredTime ?? DateTime.MinValue)
				.ThenBy(house => house.AddressId)
				.ToArray();
			for (var i = 0; i < ordered.Length; i++)
				ordered[i] = ordered[i] with { IsInactive = i != 0 };
			return ordered;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load houses for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerHouse>();
		}
	}

	private async Task<List<PlayerHouse>> AttachHouseScriptsAsync(
		MySqlConnection connection,
		List<PlayerHouse> houses,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/HouseScriptsDAO.getPlayerScripts restores house_scripts rows ordered by date_added.
		if (houses.Count == 0)
			return houses;

		try
		{
			await using var command = connection.CreateCommand();
			var placeholders = new string[houses.Count];
			for (var i = 0; i < houses.Count; i++)
			{
				var parameterName = $"@house{i}";
				placeholders[i] = parameterName;
				command.Parameters.Add(new MySqlParameter(parameterName, houses[i].ObjectId));
			}

			command.CommandText = $"""
				SELECT house_id, script_id, script
				FROM house_scripts
				WHERE house_id IN ({string.Join(", ", placeholders)})
				ORDER BY house_id, date_added
				""";

			var rows = new List<HouseScriptRestoreRow>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				rows.Add(
					new HouseScriptRestoreRow(
						ReadInt(reader, "house_id"),
						ReadInt(reader, "script_id"),
						ReadString(reader, "script")));
			}

			RestoreHouseScripts(houses, rows);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not restore house scripts for player houses");
		}

		return houses;
	}

	internal static void RestoreHouseScripts(
		IReadOnlyList<PlayerHouse> houses,
		IEnumerable<HouseScriptRestoreRow> rows)
	{
		var housesByObjectId = houses.ToDictionary(house => house.ObjectId);
		foreach (var row in rows)
		{
			if (housesByObjectId.TryGetValue(row.HouseObjectId, out var house))
				house.Scripts.RestoreFromXml(row.ScriptId, row.ScriptXml);
		}
	}

	internal readonly record struct HouseScriptRestoreRow(int HouseObjectId, int ScriptId, string ScriptXml);

	private async Task<List<PlayerHouse>> AttachTownLevelsAsync(
		MySqlConnection connection,
		List<PlayerHouse> houses,
		CancellationToken cancellationToken)
	{
		// Java parity: model/house/House.getTownLevel -> TownService.getTownById(address.townId).getLevel().
		var housingTemplates = _runtimeContext.DataManager?.StaticData.HousingTemplates;
		if (housingTemplates == null || houses.Count == 0)
			return houses;

		var townIds = houses
			.Select(house => housingTemplates.GetAddress(house.AddressId)?.TownId ?? 0)
			.Where(townId => townId > 0)
			.Distinct()
			.ToArray();
		if (townIds.Length == 0)
			return houses;

		var townLevels = await LoadTownLevelsAsync(connection, townIds, cancellationToken);
		return houses
			.Select(
				house =>
				{
					var townId = housingTemplates.GetAddress(house.AddressId)?.TownId ?? 0;
					if (townId == 0)
						return house with { TownLevel = 0 };

					// Java parity: TownService seeds known towns at level 1 when the towns table starts empty.
					return house with { TownLevel = townLevels.GetValueOrDefault(townId, 1) };
				})
			.ToList();
	}

	private async Task<IReadOnlyDictionary<int, int>> LoadTownLevelsAsync(
		MySqlConnection connection,
		IReadOnlyList<int> townIds,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var command = connection.CreateCommand();
			var placeholders = new string[townIds.Count];
			for (var i = 0; i < townIds.Count; i++)
			{
				var parameterName = $"@town{i}";
				placeholders[i] = parameterName;
				command.Parameters.Add(new MySqlParameter(parameterName, townIds[i]));
			}

			command.CommandText = $"""
				SELECT id, level
				FROM towns
				WHERE id IN ({string.Join(", ", placeholders)})
				""";

			var townLevels = new Dictionary<int, int>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				townLevels[ReadInt(reader, "id")] = ReadInt(reader, "level");
			return townLevels;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Could not load housing town levels");
			return new Dictionary<int, int>();
		}
	}

	public async Task<IReadOnlyDictionary<int, long>> LoadPlayerCraftCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/CraftCooldownsDAO.loadCraftCooldowns through model/gameobjects/player/Cooldowns.put.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT delay_id, reuse_time FROM craft_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, long>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "delay_id")] = reuseTime;
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load craft cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, long>();
		}
	}

	public async Task<IReadOnlyDictionary<int, long>> LoadPlayerHouseObjectCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/HouseObjectCooldownsDAO.loadHouseObjectCooldowns through model/gameobjects/player/Cooldowns.put.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT object_id, reuse_time FROM house_object_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, long>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				cooldowns[ReadInt(reader, "object_id")] = reuseTime;
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load house object cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, long>();
		}
	}

	public async Task<IReadOnlyDictionary<int, PlayerPortalCooldown>> LoadPlayerPortalCooldownsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PortalCooldownsDAO.loadPortalCooldowns.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT world_id, reuse_time, entry_count FROM portal_cooldowns WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var cooldowns = new Dictionary<int, PlayerPortalCooldown>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var reuseTime = ReadLong(reader, "reuse_time");
				if (reuseTime <= nowMillis)
					continue;

				var worldId = ReadInt(reader, "world_id");
				cooldowns[worldId] = new PlayerPortalCooldown(
					worldId,
					reuseTime,
					ReadInt(reader, "entry_count"));
			}

			return cooldowns;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load portal cooldowns for player {PlayerObjectId}", playerObjectId);
			return new Dictionary<int, PlayerPortalCooldown>();
		}
	}

	public async Task<PlayerLifeStats?> LoadPlayerLifeStatsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerLifeStatsDAO.loadPlayerLifeStat.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT hp, mp, fp FROM player_life_stats WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new PlayerLifeStats(
				ReadInt(reader, "hp"),
				ReadInt(reader, "mp"),
				ReadInt(reader, "fp"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load life stats for player {PlayerObjectId}", playerObjectId);
			return null;
		}
	}

	public async Task<IReadOnlyList<PlayerFriend>> LoadPlayerFriendsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/FriendListDAO.load plus PlayerService.getOrLoadPlayerCommonData.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					f.friend, f.memo,
					p.name, p.exp, p.player_class, p.gender, p.world_id, p.last_online, p.note, p.online,
					(
						SELECT h.address
						FROM houses h
						WHERE h.player_id = p.id
						ORDER BY CASE WHEN h.address IN (2001, 3001) THEN 0 ELSE 1 END, h.acquire_time, h.address
						LIMIT 1
					) AS house_address,
					(
						SELECT h.settings
						FROM houses h
						WHERE h.player_id = p.id
						ORDER BY CASE WHEN h.address IN (2001, 3001) THEN 0 ELSE 1 END, h.acquire_time, h.address
						LIMIT 1
					) AS house_settings
				FROM friends f
				JOIN players p ON p.id = f.friend
				WHERE f.player = ?
				ORDER BY f.friend
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var friends = new List<PlayerFriend>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var houseAddressId = ReadInt(reader, "house_address");
				friends.Add(
					new PlayerFriend(
						ReadInt(reader, "friend"),
						ReadString(reader, "name"),
						ReadLong(reader, "exp"),
						ReadString(reader, "player_class"),
						ReadString(reader, "gender"),
						ReadInt(reader, "world_id"),
						ReadDateTime(reader, "last_online"),
						ReadString(reader, "note"),
						ReadString(reader, "memo"),
						ReadBoolean(reader, "online"),
						houseAddressId,
						houseAddressId == 0
							? (byte)0
							: PlayerHouse.GetDoorStateFromSettings(ReadInt(reader, "house_settings"))));
			}

			return friends;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load friends for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerFriend>();
		}
	}

	public async Task<IReadOnlyList<PlayerBlockedUser>> LoadPlayerBlockedUsersAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/BlockListDAO.load plus PlayerService.getPlayerName.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT b.blocked_player, b.reason, p.name
				FROM blocks b
				JOIN players p ON p.id = b.blocked_player
				WHERE b.player = ?
				ORDER BY b.blocked_player
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var blockedUsers = new List<PlayerBlockedUser>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				blockedUsers.Add(
					new PlayerBlockedUser(
						ReadInt(reader, "blocked_player"),
						ReadString(reader, "name"),
						ReadString(reader, "reason")));
			}

			return blockedUsers;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load block list for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerBlockedUser>();
		}
	}

	public async Task<PlayerAbyssRank> LoadPlayerAbyssRankAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/AbyssRankDAO.loadAbyssRank.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT daily_ap, weekly_ap, ap, daily_gp, weekly_gp, gp, `rank`, daily_kill, weekly_kill,
					all_kill, max_rank, last_kill, last_ap, last_gp, rank_pos
				FROM abyss_rank
				WHERE player_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return PlayerAbyssRank.Default();

			return new PlayerAbyssRank(
				ReadInt(reader, "daily_ap"),
				ReadInt(reader, "weekly_ap"),
				ReadInt(reader, "ap"),
				ReadInt(reader, "daily_gp"),
				ReadInt(reader, "weekly_gp"),
				ReadInt(reader, "gp"),
				ReadInt(reader, "rank"),
				ReadInt(reader, "daily_kill"),
				ReadInt(reader, "weekly_kill"),
				ReadInt(reader, "all_kill"),
				ReadInt(reader, "max_rank"),
				ReadInt(reader, "last_kill"),
				ReadInt(reader, "last_ap"),
				ReadInt(reader, "last_gp"),
				ReadInt(reader, "rank_pos"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load abyss rank for player {PlayerObjectId}", playerObjectId);
			return PlayerAbyssRank.Default();
		}
	}

	public async Task<PlayerSettings> LoadPlayerSettingsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerSettingsDAO.loadSettings.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT settings_type, settings FROM player_settings WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			byte[]? uiSettings = null;
			byte[]? shortcuts = null;
			byte[]? houseBuddies = null;
			var display = 0;
			var deny = 0;
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				switch (ReadInt(reader, "settings_type"))
				{
					case 0:
						uiSettings = ReadBytes(reader, "settings");
						break;
					case 1:
						shortcuts = ReadBytes(reader, "settings");
						break;
					case 2:
						houseBuddies = ReadBytes(reader, "settings");
						break;
					case -1:
						display = ReadSettingsInt(reader, "settings");
						break;
					case -2:
						deny = ReadSettingsInt(reader, "settings");
						break;
				}
			}

			return new PlayerSettings
			{
				UiSettings = uiSettings,
				Shortcuts = shortcuts,
				HouseBuddies = houseBuddies,
				Display = display,
				Deny = deny,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load settings for player {PlayerObjectId}", playerObjectId);
			return new PlayerSettings();
		}
	}

	public async Task<PlayerBindPoint?> LoadPlayerBindPointAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerBindPointDAO.loadBindPoint.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT map_id, x, y, z, heading FROM player_bind_point WHERE player_id = ?";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			if (!await reader.ReadAsync(cancellationToken))
				return null;

			return new PlayerBindPoint(
				ReadInt(reader, "map_id"),
				reader.GetFloat(reader.GetOrdinal("x")),
				reader.GetFloat(reader.GetOrdinal("y")),
				reader.GetFloat(reader.GetOrdinal("z")),
				(byte)ReadInt(reader, "heading"));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load bind point for player {PlayerObjectId}", playerObjectId);
			return null;
		}
	}

	public async Task<IReadOnlyList<PlayerOwnedPet>> LoadPlayerPetsAsync(int playerObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.getPlayerPets feeds player PetList.loadPets during Player construction.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT id, player_id, template_id, decoration, name, despawn_time, expire_time,
					hungry_level, feed_progress, reuse_time, dopings, birthday, mood_started,
					counter, mood_cd_started, gift_cd_started
				FROM player_pets
				WHERE player_id = ?
				""";
			command.Parameters.Add(new MySqlParameter { Value = playerObjectId });

			var pets = new List<PlayerOwnedPet>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
			{
				var row = new PlayerPetRepositoryRow(
						PetObjectId: ReadInt(reader, "id"),
						TemplateId: ReadInt(reader, "template_id"),
						PlayerObjectId: ReadInt(reader, "player_id"),
						ExpireTime: ReadInt(reader, "expire_time"),
						Name: ReadString(reader, "name"),
						Decoration: ReadInt(reader, "decoration"),
						HungryLevel: ReadInt(reader, "hungry_level"),
						FeedProgressData: ReadInt(reader, "feed_progress"),
						ReuseTimeMillis: ReadLong(reader, "reuse_time"),
						Dopings: ReadNullableString(reader, "dopings"),
						Birthday: ReadDateTimeOffset(reader, "birthday"),
						MoodStartedMillis: ReadLong(reader, "mood_started"),
						ShuggleCounter: ReadInt(reader, "counter"),
						MoodCooldownStartedMillis: ReadLong(reader, "mood_cd_started"),
						GiftCooldownStartedMillis: ReadLong(reader, "gift_cd_started"),
						DespawnTime: ReadDateTimeOffset(reader, "despawn_time"));
				var template = _runtimeContext.DataManager?.StaticData.PetTemplates.GetPetTemplate(row.TemplateId);
				var projection = PlayerPetRowProjection.Project(
					row,
					new PlayerPetProjectionOptions(
						HasFoodFunction: template?.ContainsFunction(PetFunctionType.Food) == true,
						HasDopingFunction: template?.ContainsFunction(PetFunctionType.Doping) == true),
					() => DateTimeOffset.Now);
				pets.Add(new PlayerOwnedPet(
					projection.PetObjectId,
					projection.TemplateId,
					projection.Name,
					projection.Decoration,
					projection.PlayerObjectId,
					projection.Birthday,
					projection.ExpireTime,
					projection.FeedProgress?.GetDataForPacket() ?? 0,
					projection.Timing.RefeedTimeMillis,
					projection.DopingBag?.GetItems() ?? [],
					HungryLevel: projection.FeedProgress?.HungryLevel ?? PetHungryLevel.Hungry,
					DespawnTime: projection.DespawnTime,
					MoodStartedMillis: projection.Timing.StartMoodTimeMillis,
					ShuggleCounter: projection.Timing.ShuggleCounter,
					MoodCooldownStartedMillis: projection.Timing.MoodCooldownStartedMillis,
					GiftCooldownStartedMillis: projection.Timing.GiftCooldownStartedMillis));
			}

			return pets;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load pets for player {PlayerObjectId}", playerObjectId);
			return Array.Empty<PlayerOwnedPet>();
		}
	}

	public async Task<bool> DeletePlayerPetAsync(int playerObjectId, int petObjectId, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.removePlayerPet deletes player_pets by id after PetList.deletePet removes it from memory.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "DELETE FROM player_pets WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not delete pet {PetObjectId} for player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> UpdatePlayerPetNameAsync(int playerObjectId, int petObjectId, string petName, CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.updatePetName updates player_pets.name by pet object id.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE player_pets SET name = ? WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = petName },
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not update pet {PetObjectId} name for player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerPetDopingBagAsync(
		int playerObjectId,
		int petObjectId,
		IReadOnlyList<int> itemIds,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.saveDopingBag stores food, drink, then scroll slots as player_pets.dopings CSV.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE player_pets SET dopings = ? WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = string.Join(",", itemIds) },
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save doping bag for pet {PetObjectId} and player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerPetFeedStatusAsync(
		int playerObjectId,
		int petObjectId,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.saveFeedStatus stores delete-time pet feed status.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "UPDATE player_pets SET hungry_level = ?, feed_progress = ?, reuse_time = ? WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = hungryLevel },
					new MySqlParameter { Value = feedProgress },
					new MySqlParameter { Value = reuseTime },
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save feed status for pet {PetObjectId} and player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerPetMoodDataAsync(
		int playerObjectId,
		int petObjectId,
		long moodStartedMillis,
		int shuggleCounter,
		long moodCooldownStartedMillis,
		long giftCooldownStartedMillis,
		DateTime? despawnTime,
		CancellationToken cancellationToken = default)
	{
		// Java parity: dao/PlayerPetsDAO.savePetMoodData stores mood counters and despawn_time by pet id.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				UPDATE player_pets
				SET mood_started = ?, counter = ?, mood_cd_started = ?, gift_cd_started = ?, despawn_time = ?
				WHERE id = ? AND player_id = ?
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = moodStartedMillis },
					new MySqlParameter { Value = shuggleCounter },
					new MySqlParameter { Value = moodCooldownStartedMillis },
					new MySqlParameter { Value = giftCooldownStartedMillis },
					new MySqlParameter { Value = despawnTime ?? (object)DBNull.Value },
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save mood data for pet {PetObjectId} and player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	public async Task<bool> SavePlayerPetFeedConsumeMutationAsync(
		int playerObjectId,
		int petObjectId,
		InventoryItem? sourceItemUpdate,
		int? deletedSourceItemObjectId,
		IReadOnlyList<InventoryItem> rewardItemUpdates,
		IReadOnlyList<InventoryItem> rewardItemAdds,
		int hungryLevel,
		int feedProgress,
		long reuseTime,
		CancellationToken cancellationToken = default)
	{
		// Java parity: PetService.checkFeeding consumes one item, optionally grants ItemService.addItem reward, and stores pet feed/refeed state.
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

			if (sourceItemUpdate != null
				&& !await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, sourceItemUpdate, cancellationToken))
			{
				await transaction.RollbackAsync(cancellationToken);
				return false;
			}

			if (deletedSourceItemObjectId.HasValue
				&& !await DeleteInventoryItemAsync(connection, transaction, playerObjectId, deletedSourceItemObjectId.Value, cancellationToken))
			{
				await transaction.RollbackAsync(cancellationToken);
				return false;
			}

			foreach (var rewardItemUpdate in rewardItemUpdates)
			{
				if (!await SaveInventoryItemCountAsync(connection, transaction, playerObjectId, rewardItemUpdate, cancellationToken))
				{
					await transaction.RollbackAsync(cancellationToken);
					return false;
				}
			}

			foreach (var rewardItemAdd in rewardItemAdds)
				await InsertInventoryItemAsync(connection, transaction, rewardItemAdd, cancellationToken);

			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = "UPDATE player_pets SET hungry_level = ?, feed_progress = ?, reuse_time = ? WHERE id = ? AND player_id = ?";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = hungryLevel },
					new MySqlParameter { Value = feedProgress },
					new MySqlParameter { Value = reuseTime },
					new MySqlParameter { Value = petObjectId },
					new MySqlParameter { Value = playerObjectId },
				});
			if (await command.ExecuteNonQueryAsync(cancellationToken) <= 0)
			{
				await transaction.RollbackAsync(cancellationToken);
				return false;
			}

			await transaction.CommitAsync(cancellationToken);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not save pet feed consume mutation for pet {PetObjectId} and player {PlayerObjectId}", petObjectId, playerObjectId);
			return false;
		}
	}

	private async Task<IReadOnlyList<InventoryItem>> LoadStorageItemsAsync(
		int ownerId,
		int location,
		int logOwnerId,
		string storageName,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var connection = DatabaseFactory.GetConnection();
			await connection.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					item_unique_id, item_id, item_count, item_color, color_expires, item_creator, expire_time, activation_count,
					item_owner, is_equipped, is_soul_bound, slot, item_location, enchant, enchant_bonus, item_skin, fusioned_item,
					optional_socket, optional_fusion_socket, charge, tune_count, rnd_bonus, fusion_rnd_bonus, tempering, pack_count,
					is_amplified, buff_skill, rnd_plume_bonus
				FROM inventory
				WHERE item_owner = ? AND item_location = ?
				ORDER BY slot, item_unique_id
				""";
			command.Parameters.AddRange(
				new[]
				{
					new MySqlParameter { Value = ownerId },
					new MySqlParameter { Value = location },
				});

			var items = new List<InventoryItem>();
			await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
			{
				while (await reader.ReadAsync(cancellationToken))
					items.Add(ReadItem(reader));
			}

			await LoadItemStonesForItemsAsync(connection, items, cancellationToken);
			return items;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not load {StorageName} items for owner {OwnerId}", storageName, logOwnerId);
			return Array.Empty<InventoryItem>();
		}
	}

	private static async Task LoadItemStonesForItemsAsync(
		MySqlConnection connection,
		IReadOnlyList<InventoryItem> items,
		CancellationToken cancellationToken)
	{
		// Java parity: dao/ItemStoneListDAO.load(Collection<Item>) after InventoryDAO.loadStorage.
		if (items.Count == 0)
			return;

		var itemsByObjectId = items.ToDictionary(item => item.ObjectId);
		await using var command = connection.CreateCommand();
		var placeholders = new string[items.Count];
		for (var i = 0; i < items.Count; i++)
		{
			var parameterName = $"@item{i}";
			placeholders[i] = parameterName;
			command.Parameters.Add(new MySqlParameter(parameterName, items[i].ObjectId));
		}

		command.CommandText = $"""
			SELECT item_unique_id, item_id, slot, category, polishNumber, polishCharge, proc_count
			FROM item_stones
			WHERE item_unique_id IN ({string.Join(", ", placeholders)})
			ORDER BY item_unique_id, category, slot
			""";

		var manaStones = new Dictionary<int, List<ItemStoneSocket>>();
		var fusionStones = new Dictionary<int, List<ItemStoneSocket>>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			var itemObjectId = ReadInt(reader, "item_unique_id");
			if (!itemsByObjectId.TryGetValue(itemObjectId, out var item))
				continue;

			var itemId = ReadInt(reader, "item_id");
			var slot = ReadInt(reader, "slot");
			var stoneType = ReadInt(reader, "category");
			switch (stoneType)
			{
				case 0:
					AddStone(manaStones, itemObjectId, new ItemStoneSocket(itemId, slot));
					break;
				case 1:
					item.Godstone = new PlayerGodstone(itemId, ReadInt(reader, "proc_count"));
					break;
				case 2:
					AddStone(fusionStones, itemObjectId, new ItemStoneSocket(itemId, slot));
					break;
				case 3:
					item.IdianStone = new PlayerIdianStone(
						itemId,
						ReadInt(reader, "polishNumber"),
						ReadInt(reader, "polishCharge"));
					break;
			}
		}

		foreach (var item in items)
		{
			if (manaStones.TryGetValue(item.ObjectId, out var itemManaStones))
				item.ManaStones = itemManaStones.OrderBy(stone => stone.Slot).ToArray();
			if (fusionStones.TryGetValue(item.ObjectId, out var itemFusionStones))
				item.FusionStones = itemFusionStones.OrderBy(stone => stone.Slot).ToArray();
		}
	}

	private static void AddStone(Dictionary<int, List<ItemStoneSocket>> stonesByItem, int itemObjectId, ItemStoneSocket stone)
	{
		if (!stonesByItem.TryGetValue(itemObjectId, out var stones))
		{
			stones = [];
			stonesByItem[itemObjectId] = stones;
		}

		stones.Add(stone);
	}

	private static string ReadString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
	}

	private static DateTime? ReadDateTime(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
	}

	private static DateTimeOffset? ReadDateTimeOffset(MySqlDataReader reader, string column)
	{
		var value = ReadDateTime(reader, column);
		if (!value.HasValue)
			return null;

		// Java parity: java.sql.Timestamp represents an instant from the configured server/JDBC timezone.
		// The current repository has no server-timezone option, so unspecified MySQL DateTime values
		// are interpreted with the local offset and documented as a remaining verification risk.
		return value.Value.Kind == DateTimeKind.Unspecified
			? new DateTimeOffset(value.Value)
			: new DateTimeOffset(value.Value.ToUniversalTime(), TimeSpan.Zero);
	}

	private static InventoryItem ReadItem(MySqlDataReader reader)
	{
		// Java parity: dao/InventoryDAO.constructItem column mapping.
		return new InventoryItem
		{
			ObjectId = ReadInt(reader, "item_unique_id"),
			ItemId = ReadInt(reader, "item_id"),
			Count = reader.GetInt64(reader.GetOrdinal("item_count")),
			Color = ReadNullableInt(reader, "item_color"),
			ColorExpires = ReadInt(reader, "color_expires"),
			Creator = ReadNullableString(reader, "item_creator"),
			ExpireTime = ReadInt(reader, "expire_time"),
			ActivationCount = ReadInt(reader, "activation_count"),
			OwnerId = ReadInt(reader, "item_owner"),
			IsEquipped = ReadBoolean(reader, "is_equipped"),
			IsSoulBound = ReadBoolean(reader, "is_soul_bound"),
			Slot = reader.GetInt64(reader.GetOrdinal("slot")),
			Location = ReadInt(reader, "item_location"),
			Enchant = ReadInt(reader, "enchant"),
			EnchantBonus = ReadInt(reader, "enchant_bonus"),
			ItemSkin = ReadInt(reader, "item_skin"),
			FusionedItem = ReadInt(reader, "fusioned_item"),
			OptionalSocket = ReadInt(reader, "optional_socket"),
			OptionalFusionSocket = ReadInt(reader, "optional_fusion_socket"),
			Charge = ReadInt(reader, "charge"),
			TuneCount = ReadInt(reader, "tune_count"),
			RandomBonus = ReadInt(reader, "rnd_bonus"),
			FusionRandomBonus = ReadInt(reader, "fusion_rnd_bonus"),
			Tempering = ReadInt(reader, "tempering"),
			PackCount = ReadInt(reader, "pack_count"),
			IsAmplified = ReadBoolean(reader, "is_amplified"),
			BuffSkill = ReadInt(reader, "buff_skill"),
			RandomPlumeBonus = ReadInt(reader, "rnd_plume_bonus"),
		};
	}

	private static int ReadInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static long ReadLong(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal));
	}

	private static float ReadFloat(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? 0 : Convert.ToSingle(reader.GetValue(ordinal));
	}

	private static int? ReadNullableInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
	}

	private static string? ReadNullableString(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
	}

	private static byte[] ReadBytes(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return reader.IsDBNull(ordinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(ordinal);
	}

	private static bool ReadBoolean(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		return !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal)) != 0;
	}

	private static byte ToLegionEmblemTypeValue(string emblemType)
	{
		// Java parity: model/team/legion/LegionEmblemType values.
		return string.Equals(emblemType, "CUSTOM", StringComparison.OrdinalIgnoreCase) ? (byte)0x80 : (byte)0;
	}

	private static int ReadSettingsInt(MySqlDataReader reader, string column)
	{
		var ordinal = reader.GetOrdinal(column);
		if (reader.IsDBNull(ordinal))
			return 0;

		var value = reader.GetValue(ordinal);
		if (value is byte[] bytes)
		{
			if (bytes.Length == 0)
				return 0;

			var text = Encoding.UTF8.GetString(bytes).Trim('\0', ' ', '\t', '\r', '\n');
			if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
				return parsed;

			return bytes.Length >= sizeof(int) ? BitConverter.ToInt32(bytes, 0) : bytes[0];
		}

		return Convert.ToInt32(value, CultureInfo.InvariantCulture);
	}

	private static string? GetBrokerRace(string race)
	{
		return race switch
		{
			"ELYOS" => "ELYOS",
			"ASMODIANS" => "ASMODIAN",
			_ => null,
		};
	}

	private static bool IsStudioAddress(PlayerHouse house)
	{
		return house.AddressId is 2001 or 3001;
	}
}

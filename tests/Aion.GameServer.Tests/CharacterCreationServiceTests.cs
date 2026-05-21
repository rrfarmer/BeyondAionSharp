using System.Text;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class CharacterCreationServiceTests
{
	[Fact]
	public async Task CreateCharacter_OpenWindowRequestReturnsOpenWindowResponse()
	{
		var service = await CreateServiceAsync();

		var result = await service.CreateCharacterAsync(CreatePacket(type: 1), accountId: 1, accountName: "account", membership: 0);

		Assert.Equal(SmCreateCharacter.ResponseOpenCreationWindow, result.ResponseCode);
		Assert.Null(result.Character);
	}

	[Fact]
	public async Task CreateCharacter_StoresJavaShapedNewCharacterRecord()
	{
		var creationRepository = new CapturingCreationRepository { StoreResult = true };
		var service = await CreateServiceAsync(creationRepository: creationRepository);

		var result = await service.CreateCharacterAsync(
			CreatePacket(characterName: "character", type: 0),
			accountId: 100,
			accountName: "account",
			membership: 0);

		Assert.Equal(SmCreateCharacter.ResponseOk, result.ResponseCode);
		Assert.NotNull(result.Character);
		Assert.Equal("Character", result.Character.Name);
		Assert.Equal(1, result.Character.ObjectId);
		Assert.Equal(210010000, result.Character.MapId);
		Assert.Equal(1212.9423f, result.Character.X);
		Assert.Equal(1044.8516f, result.Character.Y);
		Assert.Equal(140.75568f, result.Character.Z);
		Assert.Equal(32, result.Character.Heading);
		Assert.Equal(3, result.Character.VisibleItems.Count);
		Assert.NotNull(creationRepository.StoredCharacter);
		Assert.Equal(100, creationRepository.StoredAccountId);
		Assert.Equal("ELYOS", creationRepository.StoredCharacter.Race);
		Assert.Equal("MALE", creationRepository.StoredCharacter.Gender);
		Assert.Equal("WARRIOR", creationRepository.StoredCharacter.PlayerClass);
		Assert.Equal(13, creationRepository.StoredCharacter.StartingItems.Count);
		Assert.Contains(creationRepository.StoredCharacter.StartingItems, item => item.ItemId == 100000094 && item.IsEquipped && item.EquipmentSlot == 1);
		Assert.Contains(creationRepository.StoredCharacter.StartingItems, item => item.ItemId == 110500003 && item.IsEquipped && item.EquipmentSlot == 8);
		Assert.Contains(creationRepository.StoredCharacter.StartingItems, item => item.ItemId == 113500001 && item.IsEquipped && item.EquipmentSlot == 4096);
		Assert.Contains(creationRepository.StoredCharacter.StartingSkills, skill => skill.SkillId == 37 && skill.SkillLevel > 0);
		Assert.Contains(creationRepository.StoredCharacter.StartingSkills, skill => skill.SkillId == 43 && skill.SkillLevel > 0);
	}

	[Fact]
	public async Task CreateCharacter_ValidatesNameRaceAndClassLikeJavaShell()
	{
		var creationRepository = new CapturingCreationRepository { NameUsed = true };
		var service = await CreateServiceAsync(creationRepository: creationRepository);

		var usedName = await service.CreateCharacterAsync(CreatePacket(characterName: "Taken"), 1, "account", 0);

		Assert.Equal(SmCreateCharacter.ResponseNameAlreadyUsed, usedName.ResponseCode);

		service = await CreateServiceAsync(
			selectionRepository: new FixedSelectionRepository(
			[
				new CharacterSelectionEntry { RaceId = 1 },
			]));

		var otherRace = await service.CreateCharacterAsync(CreatePacket(raceId: 0), 1, "account", 0);

		Assert.Equal(SmCreateCharacter.ResponseOtherRace, otherRace.ResponseCode);

		service = await CreateServiceAsync();

		var forbiddenClass = await service.CreateCharacterAsync(CreatePacket(classId: 1), 1, "account", 0);

		Assert.Equal(SmCreateCharacter.ResponseForbiddenClass, forbiddenClass.ResponseCode);
	}

	[Fact]
	public async Task CheckNickname_ValidatesJavaNameResponses()
	{
		var creationRepository = new CapturingCreationRepository { NameUsed = true };
		var service = await CreateServiceAsync(creationRepository: creationRepository);

		Assert.Equal(SmCreateCharacter.ResponseNameAlreadyUsed, await service.CheckNicknameAsync("taken"));
		Assert.Equal("Taken", creationRepository.LastCheckedName);

		service = await CreateServiceAsync(
			options: new GameServerOptions { Core = new GameServerCoreOptions { CharacterCreationMode = 2 } },
			creationRepository: new CapturingCreationRepository { NameUsed = true });

		Assert.Equal(SmCreateCharacter.ResponseNameReserved, await service.CheckNicknameAsync("reserved"));

		service = await CreateServiceAsync(
			options: new GameServerOptions { Names = new GameServerNameOptions { ForbiddenWords = ["Forbidden"] } });

		Assert.Equal(SmCreateCharacter.ResponseInvalidName, await service.CheckNicknameAsync("x"));
		Assert.Equal(SmCreateCharacter.ResponseForbiddenCharacterName, await service.CheckNicknameAsync("forbidden"));
		Assert.Equal(SmCreateCharacter.ResponseOk, await service.CheckNicknameAsync("candidate"));
	}

	[Fact]
	public async Task CreateCharacter_UsesMembershipSpecificCharacterLimit()
	{
		var existingCharacters = new FixedSelectionRepository(
		[
			new CharacterSelectionEntry { RaceId = 0 },
			new CharacterSelectionEntry { RaceId = 0 },
			new CharacterSelectionEntry { RaceId = 0 },
		]);
		var options = new GameServerOptions
		{
			Core = new GameServerCoreOptions { CharacterLimitCount = 2 },
			Membership = new GameServerMembershipOptions { CharacterAdditionalEnable = 1, CharacterAdditionalCount = 4 },
		};
		var service = await CreateServiceAsync(options: options, selectionRepository: existingCharacters);

		var regularResult = await service.CreateCharacterAsync(CreatePacket(), 1, "account", membership: 0);

		Assert.Equal(SmCreateCharacter.ResponseServerLimitExceeded, regularResult.ResponseCode);

		service = await CreateServiceAsync(options: options, selectionRepository: existingCharacters);

		var memberResult = await service.CreateCharacterAsync(CreatePacket(), 1, "account", membership: 1);

		Assert.Equal(SmCreateCharacter.ResponseOk, memberResult.ResponseCode);
	}

	private static async Task<CharacterCreationService> CreateServiceAsync(
		GameServerOptions? options = null,
		ICharacterSelectionRepository? selectionRepository = null,
		CapturingCreationRepository? creationRepository = null,
		IDFactory? idFactory = null)
	{
		var repoRoot = FindRepoRoot();
		using var temp = TempDirectory.Create();
		var dataManager = await DataManager.LoadAsync(repoRoot, cacheDirectory: temp.Path, validateWhenCacheChanges: false);
		var runtimeContext = new GameServerRuntimeContext();
		runtimeContext.SetDataManager(dataManager);
		return new CharacterCreationService(
			options ?? new GameServerOptions(),
			runtimeContext,
			selectionRepository ?? new FixedSelectionRepository([]),
			creationRepository ?? new CapturingCreationRepository { StoreResult = true },
			idFactory ?? new IDFactory(),
			NullLogger<CharacterCreationService>.Instance);
	}

	private static CmCreateCharacter CreatePacket(string characterName = "Character", int genderId = 0, int raceId = 0, int classId = 0, int type = 0)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteD(100);
		buffer.WriteS("account");
		WriteFixedS(buffer, characterName, 25);
		buffer.WriteD(genderId);
		buffer.WriteD(raceId);
		buffer.WriteD(classId);
		buffer.WriteD(7);
		buffer.WriteD(0x112233);
		buffer.WriteD(0x445566);
		buffer.WriteD(0x778899);
		buffer.WriteD(unchecked((int)0xAABBCC));
		for (var i = 1; i <= 52; i++)
			buffer.WriteC(i);
		buffer.WriteF(1.25f);
		buffer.WriteC(type);

		var packet = new CmCreateCharacter(151, new HashSet<GameConnectionState> { GameConnectionState.Authed });
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static void WriteFixedS(PacketBuffer buffer, string value, int fixedLength)
	{
		for (var i = 0; i < fixedLength; i++)
			buffer.WriteH(i < value.Length ? value[i] : 0);
		buffer.WriteH(0);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}

	private sealed class CapturingCreationRepository : ICharacterCreationRepository
	{
		public bool NameUsed { get; init; }

		public bool StoreResult { get; init; }

		public string LastCheckedName { get; private set; } = string.Empty;

		public int StoredAccountId { get; private set; }

		public NewCharacterRecord StoredCharacter { get; private set; } = null!;

		public Task<bool> IsNameUsedAsync(string name, CancellationToken cancellationToken = default)
		{
			LastCheckedName = name;
			return Task.FromResult(NameUsed);
		}

		public Task<bool> IsNameUsedOrReservedAsync(string? oldName, string newName, int reservationDays, CancellationToken cancellationToken = default)
		{
			LastCheckedName = newName;
			return Task.FromResult(NameUsed);
		}

		public Task<bool> StoreNewCharacterAsync(int accountId, NewCharacterRecord character, CancellationToken cancellationToken = default)
		{
			StoredAccountId = accountId;
			StoredCharacter = character;
			return Task.FromResult(StoreResult);
		}
	}

	private sealed class FixedSelectionRepository : ICharacterSelectionRepository
	{
		private readonly IReadOnlyList<CharacterSelectionEntry> _characters;

		public FixedSelectionRepository(IReadOnlyList<CharacterSelectionEntry> characters)
		{
			_characters = characters;
		}

		public Task<IReadOnlyList<CharacterSelectionEntry>> LoadCharactersAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_characters);
		}

		public Task<int> GetCharacterCountAsync(int accountId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(_characters.Count);
		}

		public Task<int> MarkCharacterForDeletionAsync(int accountId, int characterObjectId, TimeSpan deletionDelay, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(0);
		}

		public Task<bool> RestoreCharacterAsync(int accountId, int characterObjectId, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(false);
		}
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-character-create-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
	}
}

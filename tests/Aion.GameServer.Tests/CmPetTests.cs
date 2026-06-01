using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmPetTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaFunctionalPetOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmPet>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(22, buffer => buffer.WriteH((int)PetAction.Spawn)), GameConnectionState.InGame));

		Assert.Equal(22, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(22, buffer => buffer.WriteH((int)PetAction.Spawn)), GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_AdoptReadsJavaFieldOrder()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Adopt);
		buffer.WriteD(1001);
		buffer.WriteD(900001);
		buffer.WriteC(7);
		buffer.WriteD(8);
		buffer.WriteD(30001);
		buffer.WriteD(9);
		buffer.WriteD(10);
		buffer.WriteS("PortPet");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Adopt, packet.Action);
		Assert.Equal(1001, packet.EggObjectId);
		Assert.Equal(900001, packet.TemplateId);
		Assert.Equal(7, packet.Unknown2);
		Assert.Equal(8, packet.Unknown3);
		Assert.Equal(30001, packet.DecorationId);
		Assert.Equal(9, packet.Unknown5);
		Assert.Equal(10, packet.Unknown6);
		Assert.Equal("PortPet", packet.PetName);
	}

	[Theory]
	[InlineData(PetAction.Surrender)]
	[InlineData(PetAction.Spawn)]
	[InlineData(PetAction.Dismiss)]
	public void ReadFrom_TemplateOnlyActionsReadTemplateIdLikeJava(PetAction action)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)action);
		buffer.WriteD(900123);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(action, packet.Action);
		Assert.Equal(900123, packet.TemplateId);
	}

	[Theory]
	[InlineData(3)]
	[InlineData(4)]
	public void ReadFrom_FoodSpecialFunctionReadsActivationAndSkipsPaddingLikeJava(int actionType)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Food);
		buffer.WriteD(actionType);
		buffer.WriteD(1);
		buffer.WriteD(0);
		buffer.WriteD(0);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Food, packet.Action);
		Assert.Equal(actionType, packet.ActionType);
		Assert.Equal(1, packet.ActivateSpecialFunction);
		Assert.Equal(0, packet.ObjectId);
	}

	[Theory]
	[InlineData(0, 700001, 2, 0)]
	[InlineData(1, 700001, 2, 0)]
	[InlineData(2, 0, 2, 4)]
	[InlineData(3, 700001, 2, 0)]
	public void ReadFrom_FoodDopingReadsSubActionsLikeJava(int dopingAction, int expectedItemId, int expectedSlot1, int expectedSlot2)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Food);
		buffer.WriteD(2);
		buffer.WriteD(dopingAction);
		switch (dopingAction)
		{
			case 0:
				buffer.WriteD(expectedItemId);
				buffer.WriteD(expectedSlot1);
				break;
			case 1:
				buffer.WriteD(expectedSlot1);
				buffer.WriteD(expectedItemId);
				break;
			case 2:
				buffer.WriteD(expectedSlot1);
				buffer.WriteD(expectedSlot2);
				break;
			case 3:
				buffer.WriteD(expectedItemId);
				buffer.WriteD(expectedSlot1);
				break;
		}

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Food, packet.Action);
		Assert.Equal(2, packet.ActionType);
		Assert.Equal(dopingAction, packet.DopingAction);
		Assert.Equal(expectedItemId, packet.DopingItemId);
		Assert.Equal(expectedSlot1, packet.DopingSlot1);
		Assert.Equal(expectedSlot2, packet.DopingSlot2);
	}

	[Fact]
	public void ReadFrom_FoodFeedReadsObjectCountAndUnknownLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Food);
		buffer.WriteD(1);
		buffer.WriteD(500001);
		buffer.WriteD(12);
		buffer.WriteD(99);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Food, packet.Action);
		Assert.Equal(1, packet.ActionType);
		Assert.Equal(500001, packet.ObjectId);
		Assert.Equal(12, packet.Count);
		Assert.Equal(99, packet.Unknown2);
	}

	[Fact]
	public void ReadFrom_RenameReadsObjectIdAndNameLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Rename);
		buffer.WriteD(400001);
		buffer.WriteS("NewPet");

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Rename, packet.Action);
		Assert.Equal(400001, packet.ObjectId);
		Assert.Equal("NewPet", packet.PetName);
	}

	[Fact]
	public void ReadFrom_MoodReadsSubtypeAndEmotionLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH((int)PetAction.Mood);
		buffer.WriteD(3);
		buffer.WriteD(14);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetAction.Mood, packet.Action);
		Assert.Equal(3, packet.SubType);
		Assert.Equal(14, packet.EmotionId);
	}

	[Fact]
	public void ReadFrom_UnknownActionKeepsJavaDefaultFields()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteH(99);
		buffer.WriteD(1234);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(99, packet.ActionId);
		Assert.Equal(PetAction.Unknown, packet.Action);
		Assert.Equal(0, packet.TemplateId);
		Assert.Equal(0, packet.ObjectId);
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(0, false)]
	public void CreateDisabledAutoSellActivationComposition_MapsParsedActionTypeFourToActivationPlan(
		int activateSpecialFunction,
		bool expectedActivate)
	{
		var packet = ReadPacket(buffer =>
		{
			buffer.WriteH((int)PetAction.Food);
			buffer.WriteD(4);
			buffer.WriteD(activateSpecialFunction);
			buffer.WriteD(0);
			buffer.WriteD(0);
		});
		var context = new CmPetAutoSellActivationCompositionContext(
			PetPresent: true,
			PetHasMerchantFunction: true,
			PetObjectId: 8801,
			MasterObjectId: 1153,
			PetName: "Bibi");

		var composition = CmPetAutoSellActivationCompositionPlanService.CreateDisabledPlan(packet, context);

		Assert.Equal(CmPetAutoSellActivationCompositionPlanStatus.ActivationPlanCreated, composition.Status);
		Assert.False(composition.IsLive);
		Assert.Same(packet, composition.Packet);
		Assert.Same(context, composition.Context);
		Assert.Equal(expectedActivate, composition.ParsedActivationFlag);
		Assert.Contains("CM_PET.runImpl FOOD actionType 4", composition.JavaSource, StringComparison.Ordinal);
		var activation = Assert.IsType<PetAutoSellActivationPlan>(composition.ActivationPlan);
		Assert.Equal(PetAutoSellActivationPlanStatus.DisabledNoSideEffects, activation.Status);
		Assert.Equal(expectedActivate, activation.Input.Activate);
		Assert.Equal(expectedActivate, activation.TargetSellingState);
		Assert.True(activation.WouldSetSellingState);
		Assert.False(activation.DidSetSellingState);
		Assert.True(activation.WouldSendPacket);
		Assert.False(activation.DidSendPacket);
		Assert.False(activation.ShouldDispatchLiveSideEffects);
		Assert.False(activation.IsLive);
	}

	[Fact]
	public void CreateDisabledAutoSellActivationComposition_RecordsMissingPetReturnBeforeService()
	{
		var packet = ReadPacket(buffer =>
		{
			buffer.WriteH((int)PetAction.Food);
			buffer.WriteD(4);
			buffer.WriteD(1);
			buffer.WriteD(0);
			buffer.WriteD(0);
		});
		var context = new CmPetAutoSellActivationCompositionContext(
			PetPresent: false,
			PetHasMerchantFunction: true,
			PetObjectId: null,
			MasterObjectId: 1153,
			PetName: null);

		var composition = CmPetAutoSellActivationCompositionPlanService.CreateDisabledPlan(packet, context);

		Assert.Equal(CmPetAutoSellActivationCompositionPlanStatus.ActivationPlanCreated, composition.Status);
		var activation = Assert.IsType<PetAutoSellActivationPlan>(composition.ActivationPlan);
		Assert.Equal(PetAutoSellActivationPlanStatus.MissingPet, activation.Status);
		Assert.Empty(activation.Steps);
		Assert.False(activation.WouldSetSellingState);
		Assert.False(activation.WouldSendPacket);
		Assert.False(activation.ShouldDispatchLiveSideEffects);
		Assert.Contains("if (pet == null) return", activation.JavaSource, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(PetAction.Spawn, 0, CmPetAutoSellActivationCompositionPlanStatus.NotFoodAction)]
	[InlineData(PetAction.Food, 3, CmPetAutoSellActivationCompositionPlanStatus.NotAutoSellAction)]
	public void CreateDisabledAutoSellActivationComposition_SkipsBranchesJavaRoutesElsewhere(
		PetAction action,
		int actionType,
		CmPetAutoSellActivationCompositionPlanStatus expectedStatus)
	{
		var packet = action == PetAction.Food
			? ReadPacket(buffer =>
			{
				buffer.WriteH((int)PetAction.Food);
				buffer.WriteD(actionType);
				buffer.WriteD(1);
				buffer.WriteD(0);
				buffer.WriteD(0);
			})
			: ReadPacket(buffer =>
			{
				buffer.WriteH((int)action);
				buffer.WriteD(900123);
			});
		var context = new CmPetAutoSellActivationCompositionContext(
			PetPresent: true,
			PetHasMerchantFunction: true,
			PetObjectId: 8801,
			MasterObjectId: 1153,
			PetName: "Bibi");

		var composition = CmPetAutoSellActivationCompositionPlanService.CreateDisabledPlan(packet, context);

		Assert.Equal(expectedStatus, composition.Status);
		Assert.Null(composition.ActivationPlan);
		Assert.False(composition.ParsedActivationFlag);
		Assert.False(composition.IsLive);
	}

	private static CmPet CreatePacket() => new(22, new HashSet<GameConnectionState> { GameConnectionState.InGame });

	private static CmPet ReadPacket(Action<PacketBuffer> writePayload)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		writePayload(buffer);
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}
}

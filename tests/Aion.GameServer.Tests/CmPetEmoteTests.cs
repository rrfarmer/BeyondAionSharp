using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmPetEmoteTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaFunctionalPetMoveOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmPetEmote>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(21, buffer => buffer.WriteC((int)PetEmote.FlyStart)), GameConnectionState.InGame));

		Assert.Equal(21, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(21, buffer => buffer.WriteC((int)PetEmote.FlyStart)), GameConnectionState.Authed));
	}

	[Theory]
	[InlineData(PetEmote.MoveStop)]
	[InlineData(PetEmote.MovePositionUpdate)]
	public void ReadFrom_CurrentPositionEmotesReadPositionAndHeadingLikeJava(PetEmote emote)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC((int)emote);
		buffer.WriteF(1.25f);
		buffer.WriteF(2.5f);
		buffer.WriteF(3.75f);
		buffer.WriteC(91);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal((int)emote, packet.EmoteId);
		Assert.Equal(emote, packet.Emote);
		Assert.Equal(1.25f, packet.X1);
		Assert.Equal(2.5f, packet.Y1);
		Assert.Equal(3.75f, packet.Z1);
		Assert.Equal(91, packet.Heading);
		Assert.Equal(0, packet.X2);
	}

	[Fact]
	public void ReadFrom_MoveToReadsCurrentAndTargetPositionLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC((int)PetEmote.MoveTo);
		buffer.WriteF(10.5f);
		buffer.WriteF(11.5f);
		buffer.WriteF(12.5f);
		buffer.WriteC(45);
		buffer.WriteF(20.5f);
		buffer.WriteF(21.5f);
		buffer.WriteF(22.5f);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(PetEmote.MoveTo, packet.Emote);
		Assert.Equal(10.5f, packet.X1);
		Assert.Equal(11.5f, packet.Y1);
		Assert.Equal(12.5f, packet.Z1);
		Assert.Equal(45, packet.Heading);
		Assert.Equal(20.5f, packet.X2);
		Assert.Equal(21.5f, packet.Y2);
		Assert.Equal(22.5f, packet.Z2);
	}

	[Theory]
	[InlineData(PetEmote.FlyStart)]
	[InlineData(PetEmote.Emotion)]
	[InlineData(PetEmote.LootStart)]
	public void ReadFrom_DefaultEmotesReadEmotionAndUnknownLikeJava(PetEmote emote)
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC((int)emote);
		buffer.WriteC(17);
		buffer.WriteC(23);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(emote, packet.Emote);
		Assert.Equal(17, packet.EmotionId);
		Assert.Equal(23, packet.Unknown2);
		Assert.Equal(0, packet.X1);
	}

	[Fact]
	public void ReadFrom_UnknownEmoteStillReadsDefaultBranchLikeJava()
	{
		var packet = CreatePacket();
		using var buffer = new PacketBuffer();
		buffer.WriteC(254);
		buffer.WriteC(44);
		buffer.WriteC(55);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(254, packet.EmoteId);
		Assert.Equal(PetEmote.Unknown, packet.Emote);
		Assert.Equal(44, packet.EmotionId);
		Assert.Equal(55, packet.Unknown2);
	}

	private static CmPetEmote CreatePacket() => new(21, new HashSet<GameConnectionState> { GameConnectionState.InGame });

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

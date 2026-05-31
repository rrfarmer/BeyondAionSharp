using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Tests;

public sealed class CmCraftTests
{
	[Fact]
	public void TryCreatePacket_RegistersJavaCraftOpcodeAsInGameOnly()
	{
		var packet = Assert.IsType<CmCraft>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(141, buffer =>
				{
					buffer.WriteC(1);
					buffer.WriteD(730190);
					buffer.WriteD(155000001);
					buffer.WriteD(9001);
					buffer.WriteH(1);
					buffer.WriteC(0);
					buffer.WriteD(186000040);
					buffer.WriteQ(2);
				}),
				GameConnectionState.InGame));

		Assert.Equal(141, packet.OpCode);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(141, buffer =>
			{
				buffer.WriteC(1);
				buffer.WriteD(730190);
				buffer.WriteD(155000001);
				buffer.WriteD(9001);
				buffer.WriteH(0);
				buffer.WriteC(0);
			}),
			GameConnectionState.Authed));
	}

	[Fact]
	public void ReadFrom_ReadsJavaCraftFieldsAndMaterials()
	{
		var packet = new CmCraft(141, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var buffer = new PacketBuffer();
		buffer.WriteC(129);
		buffer.WriteD(730190);
		buffer.WriteD(155000078);
		buffer.WriteD(0);
		buffer.WriteH(2);
		buffer.WriteC(1);
		buffer.WriteD(186000040);
		buffer.WriteQ(3);
		buffer.WriteD(186000041);
		buffer.WriteQ(7);

		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));

		Assert.Equal(129, packet.UnknownByte);
		Assert.Equal(730190, packet.TargetTemplateId);
		Assert.Equal(155000078, packet.RecipeId);
		Assert.Equal(0, packet.TargetObjectId);
		Assert.Equal(1, packet.CraftType);
		Assert.Equal(2, packet.MaterialsData.Count);
		Assert.Equal(3L, packet.MaterialsData[186000040]);
		Assert.Equal(7L, packet.MaterialsData[186000041]);
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

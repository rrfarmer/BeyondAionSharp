using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmNpcInfo : GameServerPacket
{
	public const int PacketOpCode = 14;
	private const int FriendCreatureType = 38;
	private const int ActiveState = 1;
	private const int NormalNpcObjectType = 1;

	private readonly PostmanNpc _npc;

	public SmNpcInfo(PostmanNpc npc)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_NPC_INFO(Npc, Player).
		_npc = npc;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		var position = _npc.Position;
		var template = _npc.Template;
		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteD(_npc.ObjectId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteD(template.TemplateId);
		buffer.WriteC(FriendCreatureType);
		buffer.WriteH(ActiveState);
		buffer.WriteC(position.Heading);
		buffer.WriteD(template.NameId);
		buffer.WriteD(template.TitleId);
		buffer.WriteH(0);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteD(_npc.CreatorId);
		buffer.WriteS(_npc.MasterName);
		buffer.WriteC(100);
		buffer.WriteD(template.MaxHp);
		buffer.WriteC(template.Level);
		buffer.WriteD(0);
		buffer.WriteF(template.BoundRadius);
		buffer.WriteF(template.Height);
		buffer.WriteF(template.RunSpeed);
		buffer.WriteH(template.AttackSpeed);
		buffer.WriteH(template.AttackSpeed);
		buffer.WriteC(0);
		buffer.WriteF(position.X);
		buffer.WriteF(position.Y);
		buffer.WriteF(position.Z);
		buffer.WriteC(0);
		buffer.WriteH(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteC(0);
		buffer.WriteH(NormalNpcObjectType);
		buffer.WriteC(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(0);
	}
}

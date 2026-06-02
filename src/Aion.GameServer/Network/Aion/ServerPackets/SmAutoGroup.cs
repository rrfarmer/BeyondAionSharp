using Aion.Commons.Network;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAutoGroup : GameServerPacket
{
	public const int PacketOpCode = 122;
	public const byte EntryIconWindowId = 6;

	private readonly int _maskId;
	private readonly int _mapId;
	private readonly int _messageId;
	private readonly int _titleId;
	private readonly int _windowId;
	private readonly int _requestTypeId;
	private readonly bool _close;
	private readonly string _name;

	public int MaskId => _maskId;

	public int WindowId => _windowId;

	public SmAutoGroup(AutoGroupSummary autoGroup)
		: this(autoGroup, windowId: 0, requestTypeId: 0, close: false, name: string.Empty)
	{
	}

	public SmAutoGroup(
		AutoGroupSummary autoGroup,
		int windowId,
		int requestTypeId = 0,
		bool close = false,
		string? name = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_AUTO_GROUP constructors read
		// AutoGroupType template fields before writeImpl emits the selected window.
		_maskId = autoGroup.MaskId;
		_mapId = autoGroup.InstanceMapId;
		_messageId = autoGroup.NameId;
		_titleId = autoGroup.TitleId;
		_windowId = windowId;
		_requestTypeId = requestTypeId;
		_close = close;
		_name = name ?? string.Empty;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_maskId);
		buffer.WriteC(_windowId);
		buffer.WriteD(_mapId);
		switch (_windowId)
		{
			case 0:
			case 7:
				buffer.WriteD(_messageId);
				buffer.WriteD(_titleId);
				buffer.WriteD(0);
				break;
			case 1:
			case 3:
			case 8:
				buffer.WriteD(0);
				buffer.WriteD(0);
				buffer.WriteD(_requestTypeId);
				break;
			case 2:
			case 4:
			case 5:
				buffer.WriteD(0);
				buffer.WriteD(0);
				buffer.WriteD(0);
				break;
			case EntryIconWindowId:
				buffer.WriteD(_messageId);
				buffer.WriteD(_titleId);
				buffer.WriteD(_close ? 0 : 1);
				break;
		}

		buffer.WriteC(0);
		buffer.WriteS(_name);
	}
}

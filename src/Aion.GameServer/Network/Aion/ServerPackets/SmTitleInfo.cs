using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTitleInfo : GameServerPacket
{
	public const int PacketOpCode = 176;

	private readonly int _action;
	private readonly int _titleId;
	private readonly int _playerObjectId;
	private readonly IReadOnlyList<PlayerTitle> _titles;
	private readonly Func<DateTimeOffset> _clock;

	public SmTitleInfo(int titleId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO(int titleId).
		_action = 1;
		_titleId = titleId;
		_playerObjectId = 0;
		_titles = Array.Empty<PlayerTitle>();
		_clock = () => DateTimeOffset.Now;
	}

	public SmTitleInfo(Player player, int titleId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO(Player, int titleId).
		_action = 3;
		_titleId = titleId;
		_playerObjectId = player.ObjectId;
		_titles = Array.Empty<PlayerTitle>();
		_clock = () => DateTimeOffset.Now;
	}

	public SmTitleInfo(IReadOnlyList<PlayerTitle> titles, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO(Player).
		_action = 0;
		_titleId = 0;
		_playerObjectId = 0;
		_titles = titles;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	public SmTitleInfo(int action, int bonusTitleId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO(int action, int bonusTitleId).
		_action = action;
		_titleId = bonusTitleId;
		_playerObjectId = 0;
		_titles = Array.Empty<PlayerTitle>();
		_clock = () => DateTimeOffset.Now;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_TITLE_INFO.writeImpl.
		buffer.WriteC(_action);
		switch (_action)
		{
			case 0:
				buffer.WriteC(0);
				buffer.WriteH(_titles.Count);
				var now = _clock();
				foreach (var title in _titles)
				{
					buffer.WriteD(title.Id);
					buffer.WriteD(title.SecondsUntilExpiration(now));
				}
				break;
			case 1:
			case 6:
				buffer.WriteH(_titleId);
				break;
			case 3:
				buffer.WriteD(_playerObjectId);
				buffer.WriteH(_titleId);
				break;
		}
	}
}

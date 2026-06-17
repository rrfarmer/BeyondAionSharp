using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Transfers;

namespace Aion.GameServer.Network.LoginServer.ServerPackets;

/// <summary>
/// Java parity: gameserver/network/loginserver/serverpackets/SM_PTRANSFER_CONTROL (KID) — cross-server character transfer control.
/// Control messages (OK/ERROR/TASK_STOP) are ported faithfully. The *_INFORMATION payload bodies depend on many
/// not-yet-ported subsystems (PlayerAppearance, item stones, EmotionList/MotionList, NpcFactions, QuestStateList, ...);
/// they currently write only the taskId header (TODO: port the full bodies once those subsystems compile).
/// </summary>
public sealed class SM_PTRANSFER_CONTROL : LoginServerPacket
{
    public const byte CHARACTER_INFORMATION = 1;
    public const byte ITEMS_INFORMATION = 5;
    public const byte DATA_INFORMATION = 6;
    public const byte SKILL_INFORMATION = 7;
    public const byte RECIPE_INFORMATION = 8;
    public const byte QUEST_INFORMATION = 9;
    public const byte ERROR = 2;
    public const byte OK = 3;
    public const byte TASK_STOP = 4;

    private readonly byte _type;
    private readonly int _taskId;
    private readonly string? _result;
    private readonly Player? _player;

    public SM_PTRANSFER_CONTROL(byte type, int taskId)
    {
        _type = type;
        _taskId = taskId;
    }

    public SM_PTRANSFER_CONTROL(byte type, int taskId, string result)
    {
        _type = type;
        _taskId = taskId;
        _result = result;
    }

    public SM_PTRANSFER_CONTROL(byte type, TransferablePlayer tp)
    {
        _type = type;
        _taskId = tp.taskId;
        _player = tp.player;
    }

    // Java parity (writeImpl audited 1:1 vs game-server/.../loginserver/serverpackets/SM_PTRANSFER_CONTROL.java): 2026-06-17
    // Control path (OK / ERROR / TASK_STOP) is byte-identical to Java. The *_INFORMATION branches
    // (CHARACTER/ITEMS/DATA/SKILL/RECIPE/QUEST) are a TRACKED DEFERRAL, not yet 1:1: their full bodies
    // read live DB (InventoryDAO/ItemStoneListDAO) plus Player emotion/motion/title subsystems that are not
    // wired onto the faithful Player (no GetEmotions/GetMotions/GetTitleList accessors exist yet). Until those
    // dependencies are ported, these branches write only writeC(type)+writeD(taskId) — see backlog §C/§D.
    protected override void WritePayload(PacketBuffer buffer)
    {
        buffer.WriteC(_type);
        switch (_type)
        {
            case OK:
                buffer.WriteD(_taskId);
                break;
            case ERROR:
            case TASK_STOP:
                buffer.WriteD(_taskId);
                buffer.WriteS(_result);
                break;
            default:
                // *_INFORMATION cases: full body TODO (depends on unported subsystems).
                buffer.WriteD(_taskId);
                break;
        }
    }
}

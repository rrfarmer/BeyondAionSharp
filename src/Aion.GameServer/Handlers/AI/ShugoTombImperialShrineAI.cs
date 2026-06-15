using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Ritsu
/// </summary>
[AIName("shugo_tomb_imperial_shrine")]
public class ShugoTombImperialShrineAI : GeneralNpcAI
{
    private const int GOLD_KEY = 182006989; // Emperor's Golden Tag
    private const int SILVER_KEY = 182006990; // Empress' Silver Tag
    private const int BRONZE_KEY = 182006991; // Crown Prince's Brass Tag
    private readonly Dictionary<int, int> entriesByKeyId = new Dictionary<int, int>();

    public ShugoTombImperialShrineAI(Npc owner) : base(owner)
    {
        entriesByKeyId.Add(GOLD_KEY, 0);
        entriesByKeyId.Add(SILVER_KEY, 0);
        entriesByKeyId.Add(BRONZE_KEY, 0);
    }

    protected override void HandleDialogStart(Player player)
    {
        base.HandleDialogStart(player);
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        QuestEnv env = new QuestEnv(GetOwner(), player, questId, dialogActionId);
        env.SetExtendedRewardIndex(extendedRewardIndex);
        switch (dialogActionId)
        {
            case DialogAction.SETPRO1:
            case DialogAction.SETPRO2:
            case DialogAction.SETPRO3:
                EnterTreasureRoomIfPossible(player, dialogActionId);
                break;
            default:
                QuestEngine.QuestEngine.GetInstance().OnDialog(env);
                break;
        }
        return true;
    }

    private void EnterTreasureRoomIfPossible(Player player, int dialogActionId)
    {
        WorldMapInstance wmi = GetPosition().GetWorldMapInstance();
        Point3D destination = null;
        switch (dialogActionId)
        {
            case DialogAction.SETPRO1: // Emperor treasure room
                if (!player.GetInventory().DecreaseByItemId(GOLD_KEY, 1))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_IDEVENT01_GOLD_MAP());
                    return;
                }
                destination = GetEntryCountByKeyId(GOLD_KEY) switch
                {
                    0 => new Point3D(74.292816f, 350.66525f, 285.14545f),
                    1 => new Point3D(375.25629f, 198.02574f, 306.84357f),
                    _ => new Point3D(335.55219f, 334.60947f, 458.5939f),
                };
                break;
            case DialogAction.SETPRO2: // Empress treasure room
                if (!player.GetInventory().DecreaseByItemId(SILVER_KEY, 1))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_IDEVENT01_SILVER_MAP());
                    return;
                }
                destination = GetEntryCountByKeyId(SILVER_KEY) switch
                {
                    0 => new Point3D(347.96069f, 41.424923f, 358.3918f),
                    1 => new Point3D(82.877762f, 240.61363f, 421.79004f),
                    _ => new Point3D(75.203018f, 432.58603f, 455.82312f),
                };
                break;
            case DialogAction.SETPRO3: // Prince treasure room
                if (!player.GetInventory().DecreaseByItemId(BRONZE_KEY, 1))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_IDEVENT01_BRONZE_MAP());
                    return;
                }
                destination = GetEntryCountByKeyId(BRONZE_KEY) switch
                {
                    0 => new Point3D(409.74783f, 247.3778f, 516.4457f),
                    1 => new Point3D(181.56918f, 385.08932f, 616.1734f),
                    _ => new Point3D(177.63902f, 77.778755f, 466.1734f),
                };
                break;
        }
        if (destination != null)
            TeleportService.TeleportTo(player, wmi, destination.GetX(), destination.GetY(), destination.GetZ(), (byte)0, TeleportAnimation.FADE_OUT_BEAM);
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
    }

    private int GetEntryCountByKeyId(int keyId)
    {
        lock (entriesByKeyId)
        {
            int entryCount = entriesByKeyId[keyId];
            entriesByKeyId[keyId] = entryCount + 1;
            return entryCount;
        }
    }
}

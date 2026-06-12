using Aion.GameServer.Model;
using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.QuestEngine.Handlers.Models.XmlQuest.Operations;

/// <summary>Java parity: .../operations/ActionItemUseOperation. Anonymous Runnable → Schedule(ct-lambda).</summary>
[XmlType("ActionItemUseOperation")]
public class ActionItemUseOperation : QuestOperation
{
    [XmlElement("finish")] protected QuestOperations finish;

    public override void DoOperate(QuestEnv env)
    {
        Player player = env.GetPlayer();
        Npc npc;
        if (env.GetVisibleObject() is Npc visibleNpc)
            npc = visibleNpc;
        else
            return;
        int defaultUseTime = 3000;
        PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), npc.GetObjectId(), defaultUseTime, 1));
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_QUESTLOOT, 0, npc.GetObjectId()), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), npc.GetObjectId(), defaultUseTime, 0));
            finish.Operate(env);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(defaultUseTime));
    }
}

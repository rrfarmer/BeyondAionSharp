using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/tiamatStrongHold/KharunAI (@author Cheatkiller).</summary>
[AIName("kharun")]
public class KharunAI : NpcAI
{
    public KharunAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        if (dialogActionId == DialogAction.SETPRO1)
        {
            AIActions.DeleteOwner(this);
            StartKharunEvent();
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void StartKharunEvent()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            Npc aethericField = GetPosition().GetWorldMapInstance().GetNpc(730613);
            Npc strongholdDoor = GetPosition().GetWorldMapInstance().GetNpc(730612);
            Npc Kharun = (Npc)Spawn(800335, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)60);
            Kharun.SetTarget(aethericField);
            Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(Kharun, 20943, 60, aethericField).UseNoAnimationSkill();
            PacketSendUtility.BroadcastMessage(Kharun, 1500597, 1000);
            PacketSendUtility.BroadcastMessage(Kharun, 1500598, 5000);
            strongholdDoor.GetController().Die();
            aethericField.GetController().Delete();
        }, 3000L);
    }
}

using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/engulfedOphidianBridgeInstance/RaceGeneralsAI (@author Cheatkiller).
/// </summary>
[AIName("engulfedophidiangenerals")]
public class RaceGeneralsAI : NpcAI
{
    public RaceGeneralsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        switch (GetOwner().GetNpcId())
        {
            case 701989: // asmo
                switch (dialogActionId)
                {
                    case DialogAction.SETPRO1:
                        DeleteNpcs(instance.GetNpcs(701987));
                        DeleteNpcs(instance.GetNpcs(701985));
                        Spawn(233495, 678.5313f, 471.29727f, 599.6582f, unchecked((sbyte)116));
                        Spawn(233493, 677.62946f, 468.9142f, 599.625f, unchecked((sbyte)116));
                        Spawn(233493, 678.6859f, 473.77505f, 599.6679f, unchecked((sbyte)116));
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402060);
                        AIActions.DeleteOwner(this);
                        break;
                    case DialogAction.SETPRO2:
                        DeleteNpcs(instance.GetNpcs(701987));
                        DeleteNpcs(instance.GetNpcs(701985));
                        Spawn(233495, 519.66113f, 446.24088f, 620.125f, unchecked((sbyte)116));
                        Spawn(233493, 517.5026f, 444.2977f, 620.125f, unchecked((sbyte)116));
                        Spawn(233493, 518.73016f, 449.66315f, 620.22894f, unchecked((sbyte)116));
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402055);
                        AIActions.DeleteOwner(this);
                        break;
                    case DialogAction.SETPRO3:
                        DeleteNpcs(instance.GetNpcs(701987));
                        DeleteNpcs(instance.GetNpcs(701985));
                        Spawn(233495, 603.4207f, 538.19196f, 590.976f, (sbyte)28);
                        Spawn(233493, 600.8837f, 538.39116f, 591.0416f, (sbyte)28);
                        Spawn(233493, 605.8344f, 538.0445f, 590.99445f, (sbyte)28);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402070);
                        AIActions.DeleteOwner(this);
                        break;
                    case DialogAction.SETPRO4:
                        DeleteNpcs(instance.GetNpcs(701987));
                        DeleteNpcs(instance.GetNpcs(701985));
                        Spawn(233495, 481.47342f, 526.27606f, 597.375f, (sbyte)19);
                        Spawn(233493, 479.6977f, 528.80994f, 597.375f, (sbyte)19);
                        Spawn(233493, 481.879f, 523.497f, 597.49243f, (sbyte)19);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402065);
                        AIActions.DeleteOwner(this);
                        break;
                }
                break;
            default: // elyos
                switch (dialogActionId)
                {
                    case DialogAction.SETPRO1:
                        DeleteNpcs(instance.GetNpcs(701986));
                        DeleteNpcs(instance.GetNpcs(701984));
                        Spawn(233494, 691.1125f, 467.0932f, 599.875f, (sbyte)54);
                        Spawn(233492, 690.11017f, 464.7947f, 599.875f, (sbyte)54);
                        Spawn(233492, 691.4471f, 471.80127f, 599.84045f, (sbyte)54);
                        AIActions.DeleteOwner(this);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402060);
                        break;
                    case DialogAction.SETPRO2:
                        DeleteNpcs(instance.GetNpcs(701986));
                        DeleteNpcs(instance.GetNpcs(701984));
                        Spawn(233494, 531.18066f, 446.37927f, 620.25f, (sbyte)58);
                        Spawn(233492, 532.5186f, 444.30832f, 620.25f, (sbyte)58);
                        Spawn(233492, 532.3772f, 449.40405f, 620.25f, (sbyte)58);
                        AIActions.DeleteOwner(this);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402055);
                        break;
                    case DialogAction.SETPRO3:
                        DeleteNpcs(instance.GetNpcs(701986));
                        DeleteNpcs(instance.GetNpcs(701984));
                        Spawn(233494, 618.9949f, 551.4716f, 590.75f, (sbyte)55);
                        Spawn(233492, 621.22687f, 555.14624f, 590.67834f, (sbyte)55);
                        Spawn(233492, 620.4509f, 547.35284f, 590.75f, (sbyte)55);
                        AIActions.DeleteOwner(this);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402070);
                        break;
                    case DialogAction.SETPRO4:
                        DeleteNpcs(instance.GetNpcs(701986));
                        DeleteNpcs(instance.GetNpcs(701984));
                        Spawn(233494, 478.23563f, 543.6911f, 597.5f, unchecked((sbyte)112));
                        Spawn(233492, 479.92032f, 545.8376f, 597.5f, unchecked((sbyte)112));
                        Spawn(233492, 476.69766f, 542.24774f, 597.5f, unchecked((sbyte)112));
                        AIActions.DeleteOwner(this);
                        PacketSendUtility.BroadcastToMap(GetOwner(), 1402065);
                        break;
                }
                break;
        }
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
        return true;
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }
}

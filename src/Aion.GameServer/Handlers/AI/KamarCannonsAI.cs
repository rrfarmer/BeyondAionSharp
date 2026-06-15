using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Cheatkiller
/// </summary>
[AIName("kamarcannons")]
public class KamarCannonsAI : ActionItemNpcAI
{
    private Npc flag;

    public KamarCannonsAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        switch (GetOwner().GetNpcId())
        {
            case 701806:
            case 701902:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(21409, player, player);
                AIActions.DeleteOwner(this);
                break;
            case 701808:
            case 701912:
                Item siegePower = player.GetInventory().GetFirstItemByItemId(164000262);
                if (siegePower != null && siegePower.GetItemCount() >= 1)
                {
                    player.GetInventory().DecreaseByItemId(164000262, 1);
                    SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(player.GetRace() == Race.ELYOS ? 21403 : 21404, player, player);
                    AIActions.DeleteOwner(this);
                }
                else
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_IDKAMAR_CANT_USE_SEIGEWEAPON());
                break;
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int npcId = GetOwner().GetNpcId();
        if (npcId != 701806 && npcId != 701902)
            flag = (Npc)Spawn(npcId == 701808 ? 801961 : 801960, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        if (flag != null)
            flag.GetController().Delete();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (flag != null)
            flag.GetController().Delete();
    }
}

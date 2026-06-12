using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_IN_GAME_SHOP_INFO (xTz, KID). In-game-shop actions (item info / category / list / balance / buy / gift). InGameShopEn/SM_IN_GAME_SHOP_* red-tolerated.</summary>
public class CM_IN_GAME_SHOP_INFO : AionClientPacket
{
    private int actionId;
    private int categoryId;
    private int listInCategory;
    private string senderName;
    private string senderMessage;

    public CM_IN_GAME_SHOP_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        actionId = ReadUC();
        categoryId = ReadD();
        listInCategory = ReadD();
        senderName = ReadS();
        senderMessage = ReadS();
    }

    protected override void RunImpl()
    {
        if (InGameShopConfig.ENABLE_IN_GAME_SHOP)
        {
            Player player = GetConnection().GetActivePlayer();

            switch (actionId)
            {
                case 0x01: // item info
                    PacketSendUtility.SendPacket(player, new SM_IN_GAME_SHOP_ITEM(player, categoryId));
                    break;
                case 0x02: // change category
                    PacketSendUtility.SendPacket(player, new SM_IN_GAME_SHOP_CATEGORY_LIST(2, categoryId));
                    player.inGameShop.SetCategory((byte)categoryId);
                    break;
                case 0x04: // category list
                    PacketSendUtility.SendPacket(player, new SM_IN_GAME_SHOP_CATEGORY_LIST(0, categoryId));
                    break;
                case 0x08: // showcat
                    if (categoryId > 1)
                        player.inGameShop.SetSubCategory((byte)categoryId);

                    PacketSendUtility.SendPacket(player, new SM_IN_GAME_SHOP_LIST(player, listInCategory, 1));
                    PacketSendUtility.SendPacket(player, new SM_IN_GAME_SHOP_LIST(player, listInCategory, 0));
                    break;
                case 0x10: // balance
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(player.GetClientConnection().GetAccount().GetToll()));
                    break;
                case 0x20: // buy
                    InGameShopEn.GetInstance().BuyItemRequest(player, categoryId);
                    break;
                case 0x40: // gift
                    InGameShopEn.GetInstance().GiftItemRequest(player, senderName, senderMessage, categoryId);
                    break;
            }
        }
    }
}

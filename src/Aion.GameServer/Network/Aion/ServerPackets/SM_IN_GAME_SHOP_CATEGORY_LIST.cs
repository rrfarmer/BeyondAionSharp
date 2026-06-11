using Aion.GameServer.Configs.Ingameshop;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.Model.Templates.Ingameshop;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_IN_GAME_SHOP_CATEGORY_LIST (xTz). In-game shop categories (type 0) or a category's sub-categories (type 2). List.get->indexer. InGameShopProperty/InGameShopEn/IGCategory red-tolerated.</summary>
public class SM_IN_GAME_SHOP_CATEGORY_LIST : AionServerPacket
{
    private int type;
    private int categoryId;
    private InGameShopProperty ing;

    public SM_IN_GAME_SHOP_CATEGORY_LIST(int type, int category)
    {
        this.type = type;
        this.categoryId = category;
        ing = InGameShopEn.GetInstance().GetIGSProperty();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(type);
        switch (type)
        {
            case 0:
                WriteH(ing.Size()); // size
                foreach (IGCategory category in ing.GetCategories())
                {
                    WriteD(category.GetId()); // categry Id
                    WriteS(category.GetName()); // category Name
                }
                break;
            case 2:
                if (categoryId < ing.Size())
                {
                    IGCategory iGCategory = ing.GetCategories()[categoryId];
                    WriteH(iGCategory.GetSubCategories().Count); // size
                    foreach (IGSubCategory subCategory in iGCategory.GetSubCategories())
                    {
                        WriteD(subCategory.GetId()); // sub category Id
                        WriteS(subCategory.GetName()); // sub category Name
                    }
                }
                break;
        }
    }
}

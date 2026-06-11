using Aion.GameServer.Model;

namespace Aion.GameServer.Model.GameObjects.Players.Title;

/// <summary>Java parity: model/gameobjects/player/title/Title implements Expirable.</summary>
public class Title : IExpirable
{
    private Aion.GameServer.Model.Templates.TitleTemplate template;
    private int id;
    private int expireTime;

    public Title(Aion.GameServer.Model.Templates.TitleTemplate template, int id, int expireTime)
    {
        this.template = template;
        this.id = id;
        this.expireTime = expireTime;
    }

    /// <summary>Returns the template.</summary>
    public Aion.GameServer.Model.Templates.TitleTemplate GetTemplate()
    {
        return template;
    }

    /// <summary>Returns the id.</summary>
    public int GetId()
    {
        return id;
    }

    public int GetExpireTime()
    {
        return expireTime;
    }

    public void OnExpire(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        player.GetTitleList().RemoveTitle(id);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_DELETE_CASH_TITLE_BY_TIMEOUT(template.GetL10n()));
    }
}

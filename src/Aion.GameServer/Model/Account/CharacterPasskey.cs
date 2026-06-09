namespace Aion.GameServer.Model.Account;

/// <summary>Java parity: model/account/CharacterPasskey (cura).</summary>
public class CharacterPasskey
{
    private int objectId;
    private int wrongCount = 0;
    private bool isPass = false;
    private ConnectType connectType;

    public int GetObjectId()
    {
        return objectId;
    }

    public void SetObjectId(int objectId)
    {
        this.objectId = objectId;
    }

    public int GetWrongCount()
    {
        return wrongCount;
    }

    public void SetWrongCount(int count)
    {
        this.wrongCount = count;
    }

    public bool IsPass()
    {
        return isPass;
    }

    public void SetIsPass(bool isPass)
    {
        this.isPass = isPass;
    }

    public ConnectType GetConnectType()
    {
        return connectType;
    }

    public void SetConnectType(ConnectType connectType)
    {
        this.connectType = connectType;
    }

    public enum ConnectType
    {
        ENTER,
        DELETE
    }
}

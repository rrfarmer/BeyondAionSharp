using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionEmblem implements Persistable.</summary>
public class LegionEmblem : IPersistable
{
    private byte emblemId = 0;
    private byte color_a = 0;
    private byte color_r = 0;
    private byte color_g = 0;
    private byte color_b = 0;
    private LegionEmblemType emblemType = LegionEmblemType.DEFAULT;
    private IPersistable.PersistentState persistentState;

    private bool isUploading = false;
    private int uploadSize = 0;
    private int uploadedSize = 0;
    private byte[] uploadData;

    private byte[] customEmblemData = { };

    public byte[] GetCustomEmblemData()
    {
        return customEmblemData;
    }

    public void SetCustomEmblemData(byte[] customEmblemData)
    {
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        this.customEmblemData = customEmblemData;
        this.emblemType = LegionEmblemType.CUSTOM;
    }

    public LegionEmblem()
    {
        SetPersistentState(IPersistable.PersistentState.NEW);
    }

    public void SetEmblem(int emblemId, int color_a, int color_r, int color_g, int color_b, LegionEmblemType emblemType, byte[] emblem_data)
    {
        this.emblemId = (byte)emblemId;
        this.color_a = (byte)color_a;
        this.color_r = (byte)color_r;
        this.color_g = (byte)color_g;
        this.color_b = (byte)color_b;
        this.emblemType = emblemType;
        this.customEmblemData = emblem_data;
        if (this.emblemType == LegionEmblemType.CUSTOM && customEmblemData == null)
            this.emblemType = LegionEmblemType.DEFAULT;

        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public byte GetEmblemId()
    {
        return emblemId;
    }

    /// <summary>The alpha value.</summary>
    public byte GetColor_a()
    {
        return color_a;
    }

    public byte GetColor_r()
    {
        return color_r;
    }

    public byte GetColor_g()
    {
        return color_g;
    }

    public byte GetColor_b()
    {
        return color_b;
    }

    public void SetUploading(bool isUploading)
    {
        this.isUploading = isUploading;
    }

    public bool IsUploading()
    {
        return isUploading;
    }

    public void SetUploadSize(int emblemSize)
    {
        this.uploadSize = emblemSize;
    }

    public int GetUploadSize()
    {
        return uploadSize;
    }

    public void AddUploadData(byte[] data)
    {
        byte[] newData = new byte[uploadedSize];
        int i = 0;
        if (uploadData != null && uploadData.Length > 0)
        {
            foreach (byte dataByte in uploadData)
            {
                newData[i] = dataByte;
                i++;
            }
        }
        foreach (byte dataByte in data)
        {
            newData[i] = dataByte;
            i++;
        }
        this.uploadData = newData;
    }

    public byte[] GetUploadData()
    {
        return this.uploadData;
    }

    public void AddUploadedSize(int uploadedSize)
    {
        this.uploadedSize += uploadedSize;
    }

    public int GetUploadedSize()
    {
        return uploadedSize;
    }

    public void SetEmblemType(LegionEmblemType emblemType)
    {
        this.emblemType = emblemType;
    }

    public LegionEmblemType GetEmblemType()
    {
        return emblemType;
    }

    /// <summary>This method will clear out all upload data.</summary>
    public void ResetUploadSettings()
    {
        this.isUploading = false;
        this.uploadedSize = 0;
        this.uploadData = null;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.persistentState = persistentState;
                break;
        }
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }
}

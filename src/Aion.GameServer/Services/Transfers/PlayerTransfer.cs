using System;

namespace Aion.GameServer.Services.Transfers;

/// <summary>Java parity: services/transfers/PlayerTransfer (xTz). Holds serialized per-section byte[] blobs (common/items/data/skill/recipe/quest) for a cross-server character transfer; getDB concatenates them. java.nio.ByteBuffer (LITTLE_ENDIAN, only whole-array puts) -> plain byte[] concatenation (endianness irrelevant for byte[] puts).</summary>
public class PlayerTransfer
{
    private byte[] commonData, itemsData, data, recipeData, skillData, questData;
    private readonly int taskId, targetAccount;
    private readonly string name, account;

    public PlayerTransfer(int taskId, int targetAccount, string account, string name)
    {
        this.taskId = taskId;
        this.targetAccount = targetAccount;
        this.account = account;
        this.name = name;
    }

    public string GetAccount()
    {
        return account;
    }

    public int GetTargetAccount()
    {
        return targetAccount;
    }

    public string GetName()
    {
        return name;
    }

    public int GetTaskId()
    {
        return taskId;
    }

    public byte[] GetCommonData()
    {
        return commonData;
    }

    public byte[] GetItemsData()
    {
        return itemsData;
    }

    public void SetItemsData(byte[] itemsData)
    {
        this.itemsData = itemsData;
    }

    public void SetCommonData(byte[] commonData)
    {
        this.commonData = commonData;
    }

    public byte[] GetSkillData()
    {
        return skillData;
    }

    public byte[] GetRecipeData()
    {
        return recipeData;
    }

    public byte[] GetQuestData()
    {
        return questData;
    }

    public byte[] GetData()
    {
        return data;
    }

    public void SetSkillData(byte[] skillData)
    {
        this.skillData = skillData;
    }

    public void SetRecipeData(byte[] recipeData)
    {
        this.recipeData = recipeData;
    }

    public void SetQuestData(byte[] questData)
    {
        this.questData = questData;
    }

    public void SetData(byte[] data)
    {
        this.data = data;
    }

    public byte[] GetDB()
    {
        byte[] buffer = new byte[GetCommonData().Length + GetItemsData().Length + GetData().Length + GetSkillData().Length
            + GetRecipeData().Length + GetQuestData().Length];
        int pos = 0;
        Array.Copy(GetCommonData(), 0, buffer, pos, GetCommonData().Length); pos += GetCommonData().Length;
        Array.Copy(GetItemsData(), 0, buffer, pos, GetItemsData().Length); pos += GetItemsData().Length;
        Array.Copy(GetData(), 0, buffer, pos, GetData().Length); pos += GetData().Length;
        Array.Copy(GetSkillData(), 0, buffer, pos, GetSkillData().Length); pos += GetSkillData().Length;
        Array.Copy(GetRecipeData(), 0, buffer, pos, GetRecipeData().Length); pos += GetRecipeData().Length;
        Array.Copy(GetQuestData(), 0, buffer, pos, GetQuestData().Length); pos += GetQuestData().Length;
        return buffer;
    }
}

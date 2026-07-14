using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Recipe;
using Aion.GameServer.SkillEngine.Properties;

namespace Aion.GameServer.Tests;

public sealed class WarningBoundaryParityTests
{
    [Fact]
    public void SkillFirstTarget_MissingAttributeRemainsJavaNull()
    {
        var missing = Deserialize<Properties>("<Properties />");
        var present = Deserialize<Properties>("<Properties first_target=\"TARGET\" />");

        Assert.Null(missing.GetFirstTarget());
        Assert.Equal(FirstTargetAttribute.TARGET, present.GetFirstTarget());
    }

    [Fact]
    public void RecipeDp_MissingAttributeUsesJavaPrimitiveZero()
    {
        var missing = Deserialize<RecipeTemplate>("<RecipeTemplate />");
        var present = Deserialize<RecipeTemplate>("<RecipeTemplate dp=\"200\" />");

        Assert.Equal(0, missing.GetDp());
        Assert.Equal(200, present.GetDp());
    }

    [Fact]
    public void ItemActivationTarget_MissingAttributeRemainsJavaNull()
    {
        var missing = Deserialize<ItemTemplate>("<ItemTemplate />");
        var present = Deserialize<ItemTemplate>("<ItemTemplate activate_target=\"TARGET\" />");

        Assert.Null(missing.GetActivationTarget());
        Assert.Equal(ItemActivationTarget.TARGET, present.GetActivationTarget());
    }

    [Fact]
    public void NpcTribe_MissingAttributeRemainsObservableAsJavaNull()
    {
        var missing = Deserialize<NpcTemplate>("<npc_template npc_id=\"1\" />");
        var present = Deserialize<NpcTemplate>("<npc_template npc_id=\"2\" tribe=\"AGGRESSIVE_ALL\" />");

        Assert.Null(missing.GetNullableTribe());
        Assert.Equal(TribeClass.AGGRESSIVE_ALL, present.GetNullableTribe());
    }

    private static T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return Assert.IsType<T>(serializer.Deserialize(reader));
    }
}

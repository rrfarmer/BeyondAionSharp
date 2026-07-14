using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players.Npcfaction;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

public sealed class JavaEnumBoundaryParityTests
{
    [Fact]
    public void ValueOf_TransferEnumsAcceptOnlyExactJavaNames()
    {
        Assert.Equal(ENpcFactionQuestState.START, JavaEnum.ValueOf<ENpcFactionQuestState>("START"));
        Assert.Equal(QuestStatus.COMPLETE, JavaEnum.ValueOf<QuestStatus>("COMPLETE"));

        foreach (string invalid in new[] { "1", "START,COMPLETE", "start", "UNDEFINED" })
        {
            Assert.Throws<ArgumentException>(() => JavaEnum.ValueOf<ENpcFactionQuestState>(invalid));
            Assert.False(JavaEnum.TryValueOf(invalid, out ENpcFactionQuestState parsed));
            Assert.Equal(default, parsed);
        }
    }

    [Fact]
    public void JaxbEnumProxiesKeepMissingAndInvalidValuesNull()
    {
        Building validBuilding = Deserialize<Building>(
            "<building id=\"1\" size=\"PALACE\" type=\"PERSONAL_FIELD\" />");
        Assert.Equal(HouseType.PALACE, validBuilding.size);
        Assert.Equal(BuildingType.PERSONAL_FIELD, validBuilding.type);

        foreach (string invalid in new[] { "4", "ESTATE,MANSION", "palace", "UNDEFINED" })
        {
            Building building = Deserialize<Building>($"<building id=\"1\" size=\"{invalid}\" />");
            Assert.Null(building.size);
        }

        var missingBuilding = Deserialize<Building>("<building id=\"1\" />");
        Assert.Null(missingBuilding.size);
        Assert.Null(missingBuilding.type);

        ItemUseLimits validLimits = DeserializeWithRoot<ItemUseLimits>(
            "<uselimits gender=\"FEMALE\" />", "uselimits");
        Assert.Equal(Gender.FEMALE, validLimits.GetGenderPermitted());

        foreach (string invalid in new[] { "1", "MALE,FEMALE", "female", "UNDEFINED" })
        {
            ItemUseLimits limits = DeserializeWithRoot<ItemUseLimits>(
                $"<uselimits gender=\"{invalid}\" />", "uselimits");
            Assert.Null(limits.GetGenderPermitted());
        }

        ItemUseLimits missingLimits = DeserializeWithRoot<ItemUseLimits>("<uselimits />", "uselimits");
        Assert.Null(missingLimits.GetGenderPermitted());
    }

    [Fact]
    public void CounterSkillProxyMatchesRealJavaJaxbForValidAndCommaValues()
    {
        Assert.Equal(AttackStatus.RESIST, DeserializeCounterSkill("RESIST"));
        Assert.Null(DeserializeCounterSkill(null));

        Assert.Null(DeserializeCounterSkill("BLOCK,RESIST"));
        Assert.Null(DeserializeCounterSkill("RESIST,PARRY"));
        Assert.Null(DeserializeCounterSkill("RESIST,DODGE"));
        Assert.Null(DeserializeCounterSkill("6"));
        Assert.Null(DeserializeCounterSkill("resist"));
        Assert.Null(DeserializeCounterSkill("UNDEFINED"));
    }

    [Fact]
    public void ShippedCounterSkillValuesMatchJavaJaxb()
    {
        string path = Path.Combine(FindRepositoryRoot(), "game-server", "data", "static_data", "skills", "skill_templates.xml");
        var invalidCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var validCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        using XmlReader reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "skill_template")
                continue;

            string? value = reader.GetAttribute("counter_skill");
            if (value is null)
                continue;

            var template = new SkillTemplate { CounterSkillRaw = value };
            if (value.Contains(",", StringComparison.Ordinal))
            {
                invalidCounts[value] = invalidCounts.GetValueOrDefault(value) + 1;
                Assert.Null(template.GetCounterSkill());
            }
            else
            {
                validCounts[value] = validCounts.GetValueOrDefault(value) + 1;
                Assert.True(JavaEnum.TryValueOf(value, out AttackStatus expected));
                Assert.Equal(expected, template.GetCounterSkill());
            }
        }

        Assert.Equal(97, validCounts.Values.Sum() + invalidCounts.Values.Sum());
        Assert.Equal(67, validCounts.Values.Sum());
        Assert.Equal(40, validCounts["DODGE"]);
        Assert.Equal(25, validCounts["PARRY"]);
        Assert.Equal(2, validCounts["BLOCK"]);
        Assert.Equal(30, invalidCounts.Values.Sum());
        Assert.Equal(22, invalidCounts["BLOCK,RESIST"]);
        Assert.Equal(6, invalidCounts["RESIST,PARRY"]);
        Assert.Equal(2, invalidCounts["RESIST,DODGE"]);
    }

    [Fact]
    public void SignetValueOfRejectsDotNetNumericAndCompositeForms()
    {
        Assert.Equal(SignetEnum.SIGNET2, JavaEnum.ValueOf<SignetEnum>("SIGNET2"));
        Assert.Throws<ArgumentException>(() => JavaEnum.ValueOf<SignetEnum>("1"));
        Assert.Throws<ArgumentException>(() => JavaEnum.ValueOf<SignetEnum>("SIGNET1,SIGNET2"));
        Assert.Throws<ArgumentException>(() => JavaEnum.ValueOf<SignetEnum>("signet2"));
        Assert.Throws<ArgumentException>(() => JavaEnum.ValueOf<SignetEnum>("UNDEFINED"));
    }

    [Fact]
    public void TargetedValueOfBoundariesCannotRegressToDotNetEnumParsing()
    {
        string root = FindRepositoryRoot();
        var expectedCalls = new Dictionary<string, string[]>
        {
            ["src/Aion.GameServer/Services/Transfers/CMT_CHARACTER_INFORMATION.cs"] =
            [
                "JavaEnum.ValueOf<ENpcFactionQuestState>(state)",
                "JavaEnum.ValueOf<QuestStatus>(status)",
            ],
            ["src/Aion.GameServer/Model/Templates/Housing/Building.cs"] =
            [
                "JavaEnum.TryValueOf(value, out HouseType parsed)",
                "JavaEnum.TryValueOf(value, out BuildingType parsed)",
            ],
            ["src/Aion.GameServer/Model/Templates/Item/ItemUseLimits.cs"] =
            [
                "JavaEnum.TryValueOf(value, out Gender parsed)",
            ],
            ["src/Aion.GameServer/SkillEngine/Model/SkillTemplate.cs"] =
            [
                "JavaEnum.TryValueOf(value, out AttackStatus parsed)",
            ],
            ["src/Aion.GameServer/SkillEngine/Effect/SignetBurstEffect.cs"] =
            [
                "JavaEnum.ValueOf<SignetEnum>(signet)",
            ],
        };

        var rawDotNetEnumParser = new Regex(@"\b(?:System\.)?Enum\.(?:Parse|TryParse)(?:<|\()", RegexOptions.CultureInvariant);
        foreach ((string relativePath, string[] calls) in expectedCalls)
        {
            string source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (string expectedCall in calls)
                Assert.Contains(expectedCall, source, StringComparison.Ordinal);

            string executableSource = string.Join('\n', source.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            Assert.DoesNotMatch(rawDotNetEnumParser, executableSource);
        }
    }

    private static AttackStatus? DeserializeCounterSkill(string? value)
    {
        string attribute = value is null ? string.Empty : $" counter_skill=\"{value}\"";
        SkillData data = Deserialize<SkillData>($"<skill_data><skill_template skill_id=\"1\"{attribute} /></skill_data>");
        return Assert.Single(data.skillTemplates).GetCounterSkill();
    }

    private static T Deserialize<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return Assert.IsType<T>(serializer.Deserialize(reader));
    }

    private static T DeserializeWithRoot<T>(string xml, string rootName)
    {
        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute(rootName));
        using var reader = new StringReader(xml);
        return Assert.IsType<T>(serializer.Deserialize(reader));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate AionServer.slnx above " + AppContext.BaseDirectory);
    }
}

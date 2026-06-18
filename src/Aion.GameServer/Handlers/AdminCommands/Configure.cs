using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Aion.Commons.Configuration;
using Aion.Commons.Configuration.Transformers;
using Aion.GameServer.Configs;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Configure (ATracer, Rolandas, Neon). Shows/changes config settings.</summary>
public class Configure : AdminCommand
{
    public Configure()
        : base("configure", "Shows/changes config settings.")
    {
        SetSyntaxInfo(
            "<list> - Shows all available configuration categories.",
            "<category> - Shows all available properties of the specified configuration.",
            "<category> <property> - Shows the properties active value.",
            "<category> <property> <value> - Changes the properties value to the new value."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        Dictionary<string, Type> configs = new();
        foreach (Type cls in Config.GetClasses()
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            string key = cls.Name.ToLower().Replace("config", "");
            if (!configs.ContainsKey(key))
                configs[key] = cls;
        }

        if ("list".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            StringBuilder sb = new StringBuilder("List of available configuration names:");
            foreach (string configname in configs.Keys)
                sb.Append("\n\t").Append(configname);
            SendInfo(admin, sb.ToString());
        }
        else
        {
            if (!configs.TryGetValue(paramsArr[0].ToLower(), out Type cls))
            {
                SendInfo(admin, "Invalid configuration name. You can get a list of available configuration categories via the <list> parameter.");
                return;
            }
            if (paramsArr.Length < 2)
            {
                StringBuilder sb = new StringBuilder("List of available properties for ").Append(cls.Name).Append(":");
                foreach (FieldInfo field in FindStaticFields(cls, _ => true))
                {
                    try
                    {
                        string value = GetFieldValue(field);
                        sb.Append("\n\t").Append(field.Name).Append("\t=\t").Append(value);
                    }
                    catch (Exception e)
                    {
                        sb.Append("\n\t").Append(field.Name).Append("\t=\t").Append("Error reading value: ").Append(e.Message);
                    }
                }
                SendInfo(admin, sb.ToString());
                return;
            }
            string fieldName = paramsArr[1].ToUpper();
            try
            {
                List<FieldInfo> fields = FindStaticFields(cls, field => field.Name.Equals(fieldName));
                if (fields.Count == 0)
                {
                    SendInfo(admin, cls.Name + "." + fieldName + " does not exist.");
                    return;
                }
                FieldInfo field = fields[0];
                string value = GetFieldValue(field);
                if (paramsArr.Length > 2)
                {
                    string newValue = string.Join(" ", paramsArr.Skip(2));
                    try
                    {
                        if (field.IsDefined(typeof(PropertiesAttribute), inherit: true))
                            field.SetValue(null, MapTransformer.Transform(ToMap(newValue), field.FieldType));
                        else
                            field.SetValue(null, ConfigurableProcessor.Transform(newValue, field.FieldType));
                    }
                    catch (TransformationException e)
                    {
                        SendInfo(admin, "The new value could not be set: " + e.InnerException?.Message);
                        return;
                    }
                    SendInfo(admin,
                        "The value of " + cls.Name + "." + fieldName + " has been changed from " + value + " to " + GetFieldValue(field));
                }
                else
                {
                    SendInfo(admin, "The current value of " + cls.Name + "." + fieldName + " is " + value);
                }
            }
            catch (Exception)
            {
                SendInfo(admin, "Could not access " + cls.Name + "." + fieldName);
            }
        }
    }

    private Dictionary<string, string> ToMap(string value)
    {
        Dictionary<string, string> values = new();
        if (value.StartsWith("{") && value.EndsWith("}"))
            value = value.Substring(1, value.Length - 2);
        string[] entries = value.Contains(",")
            ? System.Text.RegularExpressions.Regex.Split(value, " *, *")
            : System.Text.RegularExpressions.Regex.Split(value, " +");
        try
        {
            foreach (string entry in entries)
            {
                int indexOfEqualsSign = entry.IndexOf('=');
                if (indexOfEqualsSign == -1)
                    throw new ArgumentException("Missing value after " + entry + " (format: key=value)");
                values[entry.Substring(0, indexOfEqualsSign).Trim()] = entry.Substring(indexOfEqualsSign + 1).Trim();
            }
        }
        catch (Exception e)
        {
            throw new TransformationException(null, e);
        }
        return values;
    }

    private string GetFieldValue(FieldInfo field)
    {
        object value = field.GetValue(null);
        if (value != null && value.GetType().IsArray)
        {
            var arr = (Array)value;
            var parts = new List<string>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                object element = arr.GetValue(i);
                parts.Add(element == null ? "null" : element.ToString());
            }
            value = "[" + string.Join(", ", parts) + "]";
        }
        return value == null ? "null" : value.ToString();
    }

    private static List<FieldInfo> FindStaticFields(Type cls, Func<FieldInfo, bool> filter)
    {
        return cls.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => filter(field))
            .ToList();
    }
}

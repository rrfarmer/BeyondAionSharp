using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Aion.GameServer.Configs.Main;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Cache;

/// <summary>Java parity: cache/HTMLCache (Layane, nbali, savormix, hex1r0, lord_rex).</summary>
public sealed class HTMLCache
{
    private static readonly ILogger log = NullLogger.Instance;

    // Java parity: FileFilter accepting directories or *.xhtml files.
    private static bool HtmlFilter(string file)
    {
        return Directory.Exists(file) || file.EndsWith(".xhtml");
    }

    private static readonly string HTML_ROOT = HTMLConfig.HTML_ROOT;

    private static class SingletonHolder
    {
        internal static readonly HTMLCache INSTANCE = new HTMLCache();
    }

    public static HTMLCache GetInstance()
    {
        return SingletonHolder.INSTANCE;
    }

    private Dictionary<string, string> cache = new Dictionary<string, string>();

    private int loadedFiles;
    private int size;

    private HTMLCache()
    {
        Reload(false);
    }

    public void Reload(bool deleteCacheFile)
    {
        lock (this)
        {
            cache.Clear();
            loadedFiles = 0;
            size = 0;

            string cacheFile = GetCacheFile();

            if (deleteCacheFile && File.Exists(cacheFile))
            {
                log.LogInformation("Cache[HTML]: Deleting cache file... OK.");

                File.Delete(cacheFile);
            }

            log.LogInformation("Cache[HTML]: Caching started... OK.");

            if (File.Exists(cacheFile))
            {
                log.LogInformation("Cache[HTML]: Using cache file... OK.");

                try
                {
                    // Java parity: ObjectInputStream readObject() of Map<String,String> — C# internal binary round-trip (runtime cache, not Java-shared data).
                    cache = ReadCache(GetCacheFile());

                    foreach (string html in cache.Values)
                    {
                        loadedFiles++;
                        size += html.Length;
                    }
                }
                catch (Exception e)
                {
                    log.LogWarning(e, "");

                    Reload(true);
                    return;
                }
            }
            else
            {
                ParseDir(HTML_ROOT);
            }

            log.LogInformation(ToString());

            if (File.Exists(cacheFile))
            {
                log.LogInformation("Cache[HTML]: Compaction skipped!");
            }
            else
            {
                log.LogInformation("Cache[HTML]: Compacting htmls... OK.");

                StringBuilder sb = new StringBuilder(8192);

                foreach (string key in new List<string>(cache.Keys))
                {
                    try
                    {
                        string oldHtml = cache[key];
                        string newHtml = CompactHtml(sb, oldHtml);

                        size -= oldHtml.Length;
                        size += newHtml.Length;

                        cache[key] = newHtml;
                    }
                    catch (Exception e)
                    {
                        log.LogWarning(e, "Cache[HTML]: Error during compaction of " + key);
                    }
                }

                log.LogInformation(ToString());
            }

            if (!File.Exists(cacheFile))
            {
                log.LogInformation("Cache[HTML]: Creating cache file... OK.");

                try
                {
                    WriteCache(GetCacheFile(), cache);
                }
                catch (IOException e)
                {
                    log.LogWarning(e, "");
                }
            }
        }
    }

    private string GetCacheFile()
    {
        return HTMLConfig.HTML_CACHE_FILE;
    }

    private static Dictionary<string, string> ReadCache(string path)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        using (BinaryReader br = new BinaryReader(new BufferedStream(new FileStream(path, FileMode.Open, FileAccess.Read))))
        {
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = br.ReadString();
                string value = br.ReadString();
                result[key] = value;
            }
        }
        return result;
    }

    private static void WriteCache(string path, Dictionary<string, string> map)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        using (BinaryWriter bw = new BinaryWriter(new BufferedStream(new FileStream(path, FileMode.Create, FileAccess.Write))))
        {
            bw.Write(map.Count);
            foreach (KeyValuePair<string, string> entry in map)
            {
                bw.Write(entry.Key);
                bw.Write(entry.Value);
            }
        }
    }

    private static readonly string[] TAGS_TO_COMPACT;

    static HTMLCache()
    {
        // TODO: is there any other tag that should be replaced?
        string[] tagsToCompact = { "html", "title", "body", "br", "br1", "p", "table", "tr", "td" };

        List<string> list = new List<string>();

        foreach (string tag in tagsToCompact)
        {
            list.Add("<" + tag + ">");
            list.Add("</" + tag + ">");
            list.Add("<" + tag + "/>");
            list.Add("<" + tag + " />");
        }

        List<string> list2 = new List<string>();

        foreach (string tag in list)
        {
            list2.Add(tag);
            list2.Add(tag + " ");
            list2.Add(" " + tag);
        }

        TAGS_TO_COMPACT = list2.ToArray();
    }

    private string CompactHtml(StringBuilder sb, string html)
    {
        sb.Length = 0;
        sb.Append(html);

        for (int i = 0; i < sb.Length; i++)
            if (char.IsWhiteSpace(sb[i]))
                sb[i] = ' ';

        ReplaceAll(sb, "  ", " ");

        ReplaceAll(sb, "< ", "<");
        ReplaceAll(sb, " >", ">");

        for (int i = 0; i < TAGS_TO_COMPACT.Length; i += 3)
        {
            ReplaceAll(sb, TAGS_TO_COMPACT[i + 1], TAGS_TO_COMPACT[i]);
            ReplaceAll(sb, TAGS_TO_COMPACT[i + 2], TAGS_TO_COMPACT[i]);
        }

        ReplaceAll(sb, "  ", " ");

        // String.trim() without additional garbage
        int fromIndex = 0;
        int toIndex = sb.Length;

        while (fromIndex < toIndex && sb[fromIndex] == ' ')
            fromIndex++;

        while (fromIndex < toIndex && sb[toIndex - 1] == ' ')
            toIndex--;

        return sb.ToString(fromIndex, toIndex - fromIndex);
    }

    private void ReplaceAll(StringBuilder sb, string pattern, string value)
    {
        for (int index = 0; (index = IndexOf(sb, pattern, index)) != -1;)
            sb.Remove(index, pattern.Length).Insert(index, value);
    }

    // Java parity: StringBuilder.indexOf(String, int).
    private static int IndexOf(StringBuilder sb, string pattern, int start)
    {
        int max = sb.Length - pattern.Length;
        for (int i = start; i <= max; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (sb[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return i;
        }
        return -1;
    }

    public void ReloadPath(string f)
    {
        ParseDir(f);

        log.LogInformation("Cache[HTML]: Reloaded specified path.");
    }

    public void ParseDir(string dir)
    {
        foreach (string file in ListFiles(dir))
        {
            if (!Directory.Exists(file))
                LoadFile(file);
            else
                ParseDir(file);
        }
    }

    // Java parity: dir.listFiles(HTML_FILTER).
    private static IEnumerable<string> ListFiles(string dir)
    {
        List<string> result = new List<string>();
        foreach (string entry in Directory.GetFileSystemEntries(dir))
        {
            if (HtmlFilter(entry))
                result.Add(entry);
        }
        return result;
    }

    public string LoadFile(string file)
    {
        if (IsLoadable(file))
        {
            try
            {
                byte[] raw = File.ReadAllBytes(file);

                string content = Encoding.GetEncoding(HTMLConfig.HTML_ENCODING).GetString(raw);
                string relpath = GetRelativePath(HTML_ROOT, file);

                size += content.Length;

                cache.TryGetValue(relpath, out string oldContent);
                if (oldContent == null)
                    loadedFiles++;
                else
                    size -= oldContent.Length;

                cache[relpath] = content;

                return content;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "Problem with htm file:");
            }
        }

        return null;
    }

    public string GetHTML(string path)
    {
        return cache.TryGetValue(path, out string value) ? value : null;
    }

    private bool IsLoadable(string file)
    {
        return File.Exists(file) && !Directory.Exists(file) && HtmlFilter(file);
    }

    public bool PathExists(string path)
    {
        return cache.ContainsKey(path);
    }

    public override string ToString()
    {
        return "Cache[HTML]: " + ((float) size / 1024).ToString("F3") + " kilobytes on " + loadedFiles + " file(s) loaded.";
    }

    public static string GetRelativePath(string baseDir, string file)
    {
        return Path.GetRelativePath(baseDir, file).Replace('\\', '/');
    }
}

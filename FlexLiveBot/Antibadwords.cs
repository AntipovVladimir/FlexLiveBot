using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlexLiveBot;

public class Antibadwords
{
    private const string file_stringmap = "lastochka.stringmap.txt";
    private const string file_badwords = "lastochka.antibadwords.dic";
    private const string file_warnedusers = "lastochka.antibadwords.warns";

    private readonly List<string> m_bad_words = new();
    private readonly Regex m_sym_pattern;
    private readonly List<Regex> m_loaded_regex = new();
    private Dictionary<long, int> WarnedUsers = new();


    private void m_load_string_map()
    {
        string data;
        try
        {
            data = File.ReadAllText(file_stringmap);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }
        if (string.IsNullOrWhiteSpace(data)) return;
        foreach (string line in data.Replace("\r\n", "\n").Split(','))
        {
            string[] kvp = line.Split(':');
            if (kvp.Length == 0 || kvp[0].Length==0) continue;
            if (!stringMapReplacements.ContainsKey(kvp[0][0]))
                stringMapReplacements.Add(kvp[0][0], kvp[1][0]);
            else
            {
                int x = kvp[0][0];
                Console.WriteLine("duplicate key {0} - {1}", kvp[0], x);
            }
        }

        Console.WriteLine("Loaded {0} string map replacements", stringMapReplacements.Count);
    }


    private readonly Dictionary<char, char> stringMapReplacements = new();

    private string m_remove_symbols(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;
        return m_sym_pattern.Replace(message, " ");
    }

    private string m_map_replacements(string message)
    {
        StringBuilder sb = new();
        foreach (char ch in message)
        {
            sb.Append(stringMapReplacements.TryGetValue(ch, out char value) ? value : ch);
        }

        return sb.ToString();
    }

    private void m_load_warns()
    {
        Dictionary<long, int> dic = JsonHelpers.Load<Dictionary<long, int>>(file_warnedusers);
        if (dic is not null)
            WarnedUsers = dic;
    }

    private void m_save_warns()
    {
        try
        {
            string data = JsonSerializer.Serialize(WarnedUsers, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(file_warnedusers, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }


    private bool m_remove_from_dictionary(string word, bool noparse = false)
    {
        string w = noparse ? word : m_map_replacements(word.Replace("!sp!", " ")).ToLower();
        return m_bad_words.Remove(w);
    }

    private void m_add_to_dictionary(string[] words)
    {
        if (words.Length == 0)
            return;
        foreach (string word in words)
        {
            string w = m_map_replacements(word.Replace("!sp!", " ")).ToLower();
            if (string.IsNullOrWhiteSpace(w))
                continue;
            if (!m_bad_words.Contains(w))
            {
                m_bad_words.Add(w);
                if (w[^1] == ' ')
                {
                    w = w.Remove(w.Length - 1, 1);
                }

                string pat = $"(.*){w}.?(.*)";
                m_loaded_regex.Add(new(pat));
            }
        }
    }

    private void m_save_dictionary()
    {
        try
        {
            StringBuilder sb = new();
            foreach (string word in m_bad_words)
            {
                sb.AppendLine(word);
            }
            File.WriteAllText(file_badwords, sb.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }


    public string Version { get; } = "1.0.2";

    public Antibadwords()
    {
        m_sym_pattern = new(@"([ \№\`\~\!\@\#\$\%\^\&\*\(\)\-\+\=\{\}\[\]\;\:\'\<\>\.\,\/\?\|\\])+", RegexOptions.Compiled);
        LoadDictionary();
        m_load_string_map();
        m_load_warns();
    }


    public string Save(string locale)
    {
        m_save_dictionary();
        m_save_warns();
        Console.WriteLine(Localization.GetText(locale, LangEnum.s_antibw_saved));
        return Localization.GetText(locale, LangEnum.s_antibw_all_saved);
    }

    public bool LoadDictionary()
    {
        try
        {
            string data = File.ReadAllText(file_badwords);
            m_loaded_regex.Clear();
            m_bad_words.Clear();
            m_add_to_dictionary(data.Replace("\r\n", "\n").Split('\n'));
            Console.WriteLine("Словарь загружен, всего {0} значений", m_bad_words.Count);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    public int GetScore(string message)
    {
        message = m_remove_symbols(message);
        string message2 = m_map_replacements(message).ToLower();
        string message1 = message.ToLower();
        if (string.IsNullOrEmpty(message))
            return 0;
        int score = 0;
        try
        {
            foreach (Regex regex in m_loaded_regex)
            {
                if (regex.IsMatch(message1) || regex.IsMatch(message2))
                    score++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return score;
    }

    public void Warn(long uid)
    {
        if (!WarnedUsers.ContainsKey(uid))
            WarnedUsers.Add(uid, 0);
        WarnedUsers[uid]++;
    }

    public int WarnTimes(long uid)
    {
        return WarnedUsers.ContainsKey(uid) ? WarnedUsers[uid] : 0;
    }


    public string GetStat(string locale)
    {
        return string.Format(Localization.GetText(locale, LangEnum.s_antibw_stat), m_bad_words.Count, m_loaded_regex.Count);
    }

    public string RemoveWord(ReadOnlySpan<string> words, string locale, bool forcecmd = false)
    {
        string _result;
        if (words.Length > 0)
            _result = m_remove_from_dictionary(words[0], forcecmd)
                ? Localization.GetText(locale, LangEnum.s_antibw_dictionary_removed)
                : Localization.GetText(locale, LangEnum.s_antibw_dictionary_unchanged_notfound);
        else
            _result = Localization.GetText(locale, LangEnum.s_antibw_dictionary_put_word_to_delete);
        return _result;
    }

    public string AddWords(ReadOnlySpan<string> words, string locale)
    {
        string _result;
        if (words.Length > 0)
        {
            int olddic = m_bad_words.Count;
            m_add_to_dictionary(words.ToArray());
            int newdic = m_bad_words.Count;
            _result = (newdic != olddic)
                ? string.Format(Localization.GetText(locale, LangEnum.s_antibw_dictionary_expanded), newdic - olddic)
                : string.Format(Localization.GetText(locale, LangEnum.s_antibw_dictionary_unchanged), olddic);
        }
        else
            _result = Localization.GetText(locale, LangEnum.s_antibw_dictionary_put_word);

        return _result;
    }
    
}
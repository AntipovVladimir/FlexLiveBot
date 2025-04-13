// ReSharper disable UseStringInterpolation
namespace FlexLiveBot;

public static class Localization
{
    public const string LocalesPath = "lang";

    public static string ValidateLocale(string locale)
    {
        return Locales.ContainsKey(locale) ? locale : "ru";
    }

    public static readonly Dictionary<string, Locale> Locales = new();
    public static string GetText(string locale, LangEnum enumIndex) => GetText(locale, (int)enumIndex);

    public static string GetText(string locale, int index)
    {
        if (Locales.ContainsKey(locale))
        {
            if (Locales[locale].Text.ContainsKey(index))
                return Locales[locale].Text[index];
        }

        string result = string.Format("Error! no localization found for {0} index {1}", locale, index);
        Console.WriteLine(result);
        return result;
    }

    public static void LoadLocalization(bool force = false)
    {
        string[] files = Directory.GetFiles(LocalesPath, "*.txt", SearchOption.TopDirectoryOnly);
        foreach (string file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
                continue;
            
            try
            {
                Locale obj = JsonHelpers.Load<Locale>(file);
                if (obj != null)
                {
                    if (Locales.ContainsKey(obj.LocaleName))
                    {
                        if (!force)
                        {
                            Console.WriteLine("Locale {0} already loaded", obj.LocaleName);
                            continue;
                        }

                        Locales.Remove(obj.LocaleName);
                    }

                    Locales.Add(obj.LocaleName, obj);
                    Console.WriteLine("Loaded locale: {0}", obj.LocaleName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
            }
        }
    }

    public static void CreateTestLocale1()
    {
        LoadLocalization(true);
    }
}
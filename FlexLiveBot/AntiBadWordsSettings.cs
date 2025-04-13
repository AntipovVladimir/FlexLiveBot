namespace FlexLiveBot;


public class AntiBadWordsSettings
{
    public bool Enabled { get; set; }
    public bool WarnsEnabled { get; set; }
    public int WarnTimes { get; set; }
    public int BanTime { get; set; }
    public int AutoCleanup { get; set; }

    public string SetSettings(string setting, string value, string locale)
    {
        if (!string.IsNullOrEmpty(value))
        {
            switch (setting)
            {
                case "cleanup":
                    if (int.TryParse(value, out int cleantime) && cleantime is >= 0 and <= 60)
                        AutoCleanup = cleantime;
                    break;
                case "warns":
                    WarnsEnabled = value.DetectBool(WarnsEnabled);
                    break;
                case "warntimes":
                    if (int.TryParse(value, out int warntimes) && warntimes >= 0)
                        WarnTimes = warntimes;
                    break;
                case "bantime":
                    if (int.TryParse(value, out int score) && score >= 0)
                        BanTime = score;
                    break;
            }
        }

        return setting switch
        {
            "cleanup" => Localization.GetText(locale, LangEnum.s_s_set_bw_cleanup),
            "warns" => string.Format(Localization.GetText(locale, LangEnum.s_s_bw_warns_enabled), WarnsEnabled),
            "warntimes" => string.Format(Localization.GetText(locale, LangEnum.s_s_bw_warn_times), WarnTimes),
            "bantime" => string.Format(Localization.GetText(locale, LangEnum.s_s_bw_ban_time), BanTime),
            _ => string.Empty
        };
    }
}
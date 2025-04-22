namespace FlexLiveBot;


public class AntispamSettings
{
    public bool Enabled { get; set; }
    public int DaysToUnban { get; set; }
    public int SpamScoreValue { get; set; }
    public int SpamScoreForward { get; set; }
    public long ReportChatId { get; set; }
    public bool PremiumAffix { get; set; }
    public bool ReportEnabled { get; set; }
    public bool ReportCopy { get; set; }
    public bool CountOwnForward { get; set; }
    public bool CountCurrentForward { get; set; }
    public int AutoCleanup { get; set; }
    public bool Solidarity { get; set; }
    public bool SilentSolidarity { get; set; }
    public int NewJoinSilentTime { get; set; }
    public bool RestrictNewJoinMedia { get; set; }
    public bool Reactions { get; set; }
    public int MaxReactions { get; set; }
    public int ReactionsInterval { get; set; }
    public bool ReactOnFriends { get; set; }
    public bool RestrictBlacklisted { get; set; }

    public bool IgnoreThreads { get; set; }
    
    public bool CleanServiceMessages { get; set; }
    public bool SkipMedia { get; set; }
    #region antispam settings

    public string SetSettings(string setting, string value, string locale)
    {
        if (!string.IsNullOrEmpty(value))
        {
            switch (setting)
            {
                case "spamscore":
                    if (int.TryParse(value, out int score) && score is > 1 and < 999)
                        SpamScoreValue = score;
                    break;
                case "spamscorefwd":
                    if (int.TryParse(value, out int _score) && _score is > -1 and < 100)
                        SpamScoreForward = _score;
                    break;
                case "daystounban":
                    if (int.TryParse(value, out int days) && days is > 0 and < 366)
                        DaysToUnban = days;
                    break;
                case "reportto":
                    if (long.TryParse(value, out long targetId))
                        ReportChatId = targetId;
                    break;
                case "premiumaffix":
                    PremiumAffix = value.DetectBool(PremiumAffix);
                    break;
                case "report":
                    ReportEnabled = value.DetectBool(ReportEnabled);
                    break;
                case "reportcopy":
                    ReportCopy = value.DetectBool(ReportCopy);
                    break;
                case "ownforward":
                    CountOwnForward = value.DetectBool(CountOwnForward);
                    break;
                case "ownchatforward":
                    CountCurrentForward = value.DetectBool(CountCurrentForward);
                    break;
                case "cleanup":
                    if (int.TryParse(value, out int cleantime) && cleantime is >= 0 and <= 60)
                        AutoCleanup = cleantime;
                    break;
                case "solidarity":
                    Solidarity = value.DetectBool(Solidarity);
                    break;
                case "silentsolidarity":
                    SilentSolidarity = value.DetectBool(SilentSolidarity);
                    break;
                case "newjoinsilence":
                    if (int.TryParse(value, out int minuts) && minuts >= 0)
                        NewJoinSilentTime = minuts;
                    break;
                case "restrictnewjoinmedia":
                    RestrictNewJoinMedia = value.DetectBool(RestrictNewJoinMedia);
                    break;
                case "restrictblacklisted":
                    RestrictBlacklisted = value.DetectBool(RestrictBlacklisted);
                    break;
                case "reactions":
                    Reactions = value.DetectBool(Reactions);
                    break;
                case "maxreactions":
                    if (int.TryParse(value, out int reactions) && reactions >= 0)
                        MaxReactions = reactions;
                    break;
                case "reactionsinterval":
                    if (int.TryParse(value, out int interval) && interval >= 0)
                        ReactionsInterval = interval;
                    break;
                case "reactonfriends":
                    ReactOnFriends = value.DetectBool(ReactOnFriends);
                    break;
                case "cleanservicemessages":
                    CleanServiceMessages  = value.DetectBool(ReactOnFriends);
                    break;
                case "skipmedia":
                    SkipMedia = value.DetectBool(ReactOnFriends);
                    break; 
            }
        }

        return setting switch
        {
            "spamscore" => string.Format(Localization.GetText(locale, LangEnum.s_s_spamscorevalue), SpamScoreValue),
            "spamscorefwd" => string.Format(Localization.GetText(locale, LangEnum.s_s_spamscoreforward), SpamScoreForward),
            "daystounban" => string.Format(Localization.GetText(locale, LangEnum.s_s_as_daystounban), DaysToUnban),
            "reportto" => string.Format(Localization.GetText(locale, LangEnum.s_s_reportchatid), ReportChatId),
            "premiumaffix" => string.Format(Localization.GetText(locale, LangEnum.s_s_premiumaffix), PremiumAffix),
            "report" => string.Format(Localization.GetText(locale, LangEnum.s_s_reportenabled), ReportEnabled),
            "reportcopy" => string.Format(Localization.GetText(locale, LangEnum.s_s_reportcopy), ReportCopy),
            "ownforward" => string.Format(Localization.GetText(locale, LangEnum.s_s_countoforward), CountOwnForward),
            "ownchatforward" => string.Format(Localization.GetText(locale, LangEnum.s_s_countcforward), CountCurrentForward),
            "cleanup" => string.Format(Localization.GetText(locale, LangEnum.s_s_as_cleanup), AutoCleanup),
            "solidarity" => string.Format(Localization.GetText(locale, LangEnum.s_s_solidarity), Solidarity),
            "silentsolidarity" => string.Format(Localization.GetText(locale, LangEnum.s_s_silentsolidarity), SilentSolidarity),
            "newjoinsilence" => string.Format(Localization.GetText(locale, LangEnum.s_s_newjoinsilenttime), NewJoinSilentTime),
            "restrictnewjoinmedia" => string.Format(Localization.GetText(locale, LangEnum.s_s_restrict_new_join_media), RestrictNewJoinMedia),
            "restrictblacklisted" => string.Format(Localization.GetText(locale, LangEnum.s_antispam_restrict_blacklisted), RestrictBlacklisted),
            "reactions" => string.Format(Localization.GetText(locale, LangEnum.s_s_reactions), Reactions),
            "maxreactions" => string.Format(Localization.GetText(locale, LangEnum.s_s_maxreactions), MaxReactions),
            "reactionsinterval" => string.Format(Localization.GetText(locale, LangEnum.s_s_reactionsinterval), ReactionsInterval),
            "reactonfriends" => string.Format(Localization.GetText(locale, LangEnum.s_s_reactonfriends), ReactOnFriends),
            "cleanservicemessages"=>string.Format(Localization.GetText(locale, LangEnum.s_s_as_clean_service_messages), CleanServiceMessages),
            "skipmedia"=>string.Format(Localization.GetText(locale, LangEnum.s_s_as_skip_media), SkipMedia),
            _ => string.Empty
        };
    }

    #endregion
}

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
// ReSharper disable UseStringInterpolation

namespace FlexLiveBot;

public class SettingsSession
{
    public long userId { get; set; }
    public long targetId { get; set; }
    public int messageId { get; set; }
    SettingsState currentState { get; set; }
    SettingsState previousState { get; set; }
    private readonly Settings settings;

    public SettingsSession(long uid, Settings _settings)
    {
        userId = uid;
        settings = _settings;
    }

    public async Task Start(TelegramBotClient bot, UserScore userScore)
    {
        UserMetrics umetrics = userScore.GetUserMetrics(userId);
        string ulang = Localization.ValidateLocale(umetrics.Locale);

        previousState = currentState;
        currentState = SettingsState.SelectChat;
        int i = 0;
        InlineKeyboardMarkup inlineMarkup = new();
        foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels)
        {
            if (channel.Value.ChatAdmins.Contains(userId))
            {
                inlineMarkup.AddButton($"{channel.Value.ChatTitle} @{channel.Value.UserName}", channel.Key.ToString());
                if (i % 2 == 1)
                    inlineMarkup.AddNewRow();
                i++;
            }
        }

        string reply;
        if (i == 0)
        {
            reply = Localization.GetText(ulang, LangEnum.s_s_first_addme_as_admin);
        }
        else
        {
            inlineMarkup.AddNewRow();
            inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_lang_settings), "lang");
            inlineMarkup.AddNewRow();
            inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_close), "close");
            reply = Localization.GetText(ulang, LangEnum.s_s_select_chat);
        }

        if (messageId != 0)
        {
            await bot.DeleteMessage(userId, messageId);
        }

        Message imsg = await bot.SendMessage(userId, reply, replyMarkup: inlineMarkup);
        messageId = imsg.MessageId;
    }

    public async Task ProcesCallback(TelegramBotClient bot, UserScore userScore, string cb)
    {
        if (string.IsNullOrEmpty(cb))
            return;
        InlineKeyboardMarkup inlineMarkup = new();
        string reply;
        UserMetrics umetrics = userScore.GetUserMetrics(userId);
        string ulang = Localization.ValidateLocale(umetrics.Locale);
        if (cb.Equals("close", StringComparison.Ordinal))
        {
            previousState = currentState = SettingsState.None;
            if (messageId != 0)
                await bot.DeleteMessage(userId, messageId);
            messageId = 0;
            return;
        }

        if (cb.Equals("return", StringComparison.Ordinal) && currentState == SettingsState.LangSettings)
        {
            cb = string.Empty;
            currentState = SettingsState.None;
        }

        if (currentState == SettingsState.LangSettings)
        {
            string[] words = cb.Split('=');
            if (words[0].Equals("lang", StringComparison.OrdinalIgnoreCase) && words.Length == 2)
            {
                umetrics.Locale = words[1].ToLower();
            }

            cb = "lang";
        }

        if (cb.Equals("lang", StringComparison.Ordinal))
        {
            reply = Localization.GetText(ulang, LangEnum.s_s_lang_settings);
            foreach (KeyValuePair<string, Locale> kvp in Localization.Locales)
            {
                string markup = string.Empty;
                if (kvp.Key.Equals(ulang))
                    markup = "✅";
                inlineMarkup.AddButton(string.Format(Localization.Locales[kvp.Key].MenuName, markup), Localization.Locales[kvp.Key].MenuCommand);
                inlineMarkup.AddNewRow();
            }

            inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "return");
            previousState = SettingsState.None;
            currentState = SettingsState.LangSettings;
            await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup);
            return;
        }


        if (currentState == SettingsState.None)
        {
            await Start(bot, userScore);
            return;
        }

        if (cb.Equals("return", StringComparison.Ordinal))
        {
            switch (currentState)
            {
                case SettingsState.None:
                    previousState = SettingsState.None;
                    break;
                case SettingsState.SelectChat:
                    previousState = SettingsState.None;
                    break;
                case SettingsState.MenuSettings:
                    previousState = SettingsState.None;
                    break;
                case SettingsState.AntiBWSettings:
                    previousState = SettingsState.SelectChat;
                    break;
                case SettingsState.AntiSpamSettings:
                    previousState = SettingsState.SelectChat;
                    break;
                case SettingsState.LangSettings:
                    previousState = SettingsState.None;
                    break;
            }

            currentState = previousState;
            cb = string.Empty;
        }

        if (currentState == SettingsState.None)
        {
            await Start(bot, userScore);
            return;
        }

        if (currentState == SettingsState.SelectChat)
        {
            if (!string.IsNullOrEmpty(cb) && long.TryParse(cb, out long tid))
                targetId = tid;
        }

        if (targetId == 0 || !settings.Channels.ContainsKey(targetId))
        {
            reply = Localization.GetText(ulang, LangEnum.s_s_not_serving_this_chat);
            previousState = currentState = SettingsState.None;
            if (messageId != 0)
                await bot.DeleteMessage(userId, messageId);
            Message msg = await bot.SendMessage(userId, reply);
            messageId = msg.MessageId;
            return;
        }

        ChannelSettings csettings = settings.Channels[targetId];
        if (!csettings.ChatAdmins.Contains(userId))
        {
            reply = Localization.GetText(ulang, LangEnum.s_s_u_have_no_admin_rights_in_chat);
            previousState = currentState = SettingsState.None;
            if (messageId != 0)
                await bot.DeleteMessage(userId, messageId);
            Message msg = await bot.SendMessage(userId, reply);
            messageId = msg.MessageId;
            return;
        }

        switch (currentState)
        {
            case SettingsState.None:
                break;
            case SettingsState.SelectChat:
            {
                inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_antispam), "antispam");
                inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_antibw), "antibw");
                inlineMarkup.AddNewRow();
                inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "return");
                reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_select_settings), settings.Channels[targetId].ChatTitle);
                previousState = currentState;
                currentState = SettingsState.MenuSettings;
                await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup);
                break;
            }
            case SettingsState.MenuSettings:
            {
                if (cb.Equals("antispam", StringComparison.Ordinal))
                {
                    reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_select_as_settings), csettings.ChatTitle);
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_status), csettings.AntiSpam.Enabled ? "✅" : "❌"),
                        "toggleEnabled");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_solidarity_status), csettings.AntiSpam.Solidarity ? "✅" : "❌"),
                        "toggleSolidarity");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_solidarity_silence), csettings.AntiSpam.SilentSolidarity ? "✅" : "❌"),
                        "toggleSilentSolidarity");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_premium_affix), csettings.AntiSpam.PremiumAffix ? "✅" : "❌"),
                        "togglePremiumAffix");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_report_status), csettings.AntiSpam.ReportEnabled ? "✅" : "❌"),
                        "toggleReportEnabled");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_report_copy), csettings.AntiSpam.ReportCopy ? "✅" : "❌"),
                        "toggleReportCopy");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_own_forward), csettings.AntiSpam.CountOwnForward ? "✅" : "❌"),
                        "toggleCountOwnForward");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_current_forward), csettings.AntiSpam.CountCurrentForward ? "✅" : "❌"),
                        "toggleCountCurrentForward");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_reactions_status), csettings.AntiSpam.Reactions ? "✅" : "❌"),
                        "toggleReactions");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_react_on_friends), csettings.AntiSpam.ReactOnFriends ? "✅" : "❌"),
                        "toggleReactOnFriends");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_days_to_unban), csettings.AntiSpam.DaysToUnban),
                        "setDaysToUnban");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_spamscore), csettings.AntiSpam.SpamScoreValue),
                        "setSpamScoreValue");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_spamscore_forward), csettings.AntiSpam.SpamScoreForward),
                        "setSpamScoreForward");
                    long tid = (csettings.AntiSpam.ReportChatId == 0) ? targetId : csettings.AntiSpam.ReportChatId;
                    inlineMarkup.AddNewRow();
                    string targetChat;
                    if (tid == userId)
                        targetChat = Localization.GetText(ulang, LangEnum.s_s_as_into_pm);
                    else
                    {
                        if (!settings.Channels.ContainsKey(tid))
                            csettings.AntiSpam.ReportChatId = tid = csettings.ChatId;
                        targetChat = settings.Channels[tid].ChatTitle;
                    }

                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_report_target), targetChat), "setReportChatId");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_as_new_join_silence_time), csettings.AntiSpam.NewJoinSilentTime),
                        "setNewJoinSilentTime");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_restrict_new_join_media),
                        csettings.AntiSpam.RestrictNewJoinMedia ? "✅" : "❌"), "toggleRestrictNewJoinMedia");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_restrict_blacklisted),
                        csettings.AntiSpam.RestrictBlacklisted ? "✅" : "❌"), "toggleRestrictBlacklisted");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_max_reactions), csettings.AntiSpam.MaxReactions),
                        "setMaxReactions");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_reactions_interval), csettings.AntiSpam.ReactionsInterval),
                        "setReactionsInterval");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_as_cleanup_interval), csettings.AntiSpam.AutoCleanup),
                        "setAutoCleanup");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_clean_service_messages),
                        csettings.AntiSpam.CleanServiceMessages ? "✅" : "❌"), "toggleCleanServiceMessages");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_skip_media),
                        csettings.AntiSpam.SkipMedia ? "✅" : "❌"), "toggleSkipMedia");

                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "return");
                    previousState = currentState;
                    currentState = SettingsState.AntiSpamSettings;
                    await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup);
                    return;
                }

                if (cb.Equals("antibw", StringComparison.Ordinal))
                {
                    reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_select_bw_settings), csettings.ChatTitle);
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_enabled), csettings.AntiBadWords.Enabled ? "✅" : "❌"),
                        "toggleEnabled");
                    inlineMarkup.AddButton(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_warns_status), csettings.AntiBadWords.WarnsEnabled ? "✅" : "❌"),
                        "toggleWarns");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_warns_times), csettings.AntiBadWords.WarnTimes),
                        "setWarnTimes");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_ban_time_status), csettings.AntiBadWords.BanTime),
                        "setBanTime");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_cleanup_interval), csettings.AntiBadWords.AutoCleanup),
                        "setAutoCleanup");
                    inlineMarkup.AddNewRow();
                    inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "return");
                    previousState = currentState;
                    currentState = SettingsState.AntiBWSettings;
                    await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup);
                    return;
                }

                break;
            }
            case SettingsState.AntiBWSettings:
            {
                string[] words = cb.Split('=');
                switch (words[0])
                {
                    case "antibw": break;
                    case "toggleEnabled":
                        csettings.AntiBadWords.Enabled = !csettings.AntiBadWords.Enabled;
                        break;
                    case "toggleWarns":
                        csettings.AntiBadWords.WarnsEnabled = !csettings.AntiBadWords.WarnsEnabled;
                        break;
                    case "antibwclean":
                        if (words.Length > 1)
                            csettings.AntiBadWords.SetSettings("cleanup", words[1], ulang);
                        break;
                    case "antibwbantime":
                        if (words.Length > 1)
                            csettings.AntiBadWords.SetSettings("bantime", words[1], ulang);
                        break;
                    case "antibwmaxwarns":
                        if (words.Length > 1)
                            csettings.AntiBadWords.SetSettings("warntimes", words[1], ulang);
                        break;
                    case "setBanTime":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_bantime_setup), csettings.AntiBadWords.BanTime);
                        List<int> bt = new() { 10, 15, 30, 60, 120, 240 };
                        foreach (int btv in bt)
                            inlineMarkup.AddButton(btv.ToString(), string.Format("antibwbantime={0}", btv));
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton("6 часов", "antibwbantime=360");
                        inlineMarkup.AddButton("12 часов", "antibwbantime=720");
                        inlineMarkup.AddButton("1 сутки", "antibwbantime=1440");
                        inlineMarkup.AddButton("3 суток", "antibwbantime=4320");
                        inlineMarkup.AddButton("1 неделя", "antibwbantime=10080");
                        inlineMarkup.AddButton("1 месяц", "antibwbantime=43200");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antibw");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "setWarnTimes":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_warns_setup), csettings.AntiBadWords.WarnTimes);
                        List<int> wt = new() { 1, 3, 5, 10 };
                        foreach (int wtv in wt)
                            inlineMarkup.AddButton(wtv.ToString(), string.Format("antibwmaxwarns={0}", wtv));
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_do_not_ban), "antibwmaxwarns=0");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antibw");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "setAutoCleanup":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_cleanup_setup), csettings.AntiBadWords.AutoCleanup);
                        List<int> ac = new() { 1, 3, 5, 10, 15 };
                        foreach (int acv in ac)
                            inlineMarkup.AddButton(acv.ToString(), string.Format("antibwclean={0}", acv));
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_never), "antibwclean=0");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antibw");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                }

                cb = "antibw";
                goto case SettingsState.MenuSettings;
            }
            case SettingsState.AntiSpamSettings:
            {
                string[] words = cb.Split('=');
                switch (words[0])
                {
                    case "antispam":
                        break;
                    case "toggleEnabled":
                        csettings.AntiSpam.Enabled = !csettings.AntiSpam.Enabled;
                        break;
                    case "toggleSolidarity":
                        csettings.AntiSpam.Solidarity = !csettings.AntiSpam.Solidarity;
                        break;
                    case "toggleSilentSolidarity":
                        csettings.AntiSpam.SilentSolidarity = !csettings.AntiSpam.SilentSolidarity;
                        break;
                    case "togglePremiumAffix":
                        csettings.AntiSpam.PremiumAffix = !csettings.AntiSpam.PremiumAffix;
                        break;
                    case "toggleReportEnabled":
                        csettings.AntiSpam.ReportEnabled = !csettings.AntiSpam.ReportEnabled;
                        break;
                    case "toggleReportCopy":
                        csettings.AntiSpam.ReportCopy = !csettings.AntiSpam.ReportCopy;
                        break;
                    case "toggleCountOwnForward":
                        csettings.AntiSpam.CountOwnForward = !csettings.AntiSpam.CountOwnForward;
                        break;
                    case "toggleCountCurrentForward":
                        csettings.AntiSpam.CountCurrentForward = !csettings.AntiSpam.CountCurrentForward;
                        break;
                    case "toggleReactions":
                        csettings.AntiSpam.Reactions = !csettings.AntiSpam.Reactions;
                        break;
                    case "toggleReactOnFriends":
                        csettings.AntiSpam.ReactOnFriends = !csettings.AntiSpam.ReactOnFriends;
                        break;
                    case "toggleRestrictNewJoinMedia":
                        csettings.AntiSpam.RestrictNewJoinMedia = !csettings.AntiSpam.RestrictNewJoinMedia;
                        break;
                    case "toggleRestrictBlacklisted":
                        csettings.AntiSpam.RestrictBlacklisted = !csettings.AntiSpam.RestrictBlacklisted;
                        break;
                    case "toggleCleanServiceMessages":
                        csettings.AntiSpam.CleanServiceMessages = !csettings.AntiSpam.CleanServiceMessages;
                        break;
                    case "toggleSkipMedia":
                        csettings.AntiSpam.SkipMedia = !csettings.AntiSpam.SkipMedia;
                        break;
                    case "setDaysToUnban":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_days_to_unban_setup), csettings.AntiSpam.DaysToUnban);
                        List<int> dtu = new() { 1, 3, 7, 14, 30, 365 };
                        foreach (int dtuv in dtu)
                            inlineMarkup.AddButton(dtuv.ToString(), string.Format("daystounban={0}", dtuv));
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "setAutoCleanup":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_cleanup_setup), csettings.AntiSpam.AutoCleanup);
                        List<int> ac = new() { 1, 3, 5, 10, 15 };
                        foreach (int acv in ac)
                            inlineMarkup.AddButton(acv.ToString(), string.Format("antiasclean={0}", acv));
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_never), "antiasclean=0");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "antiasclean":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("cleanup", words[1], ulang);
                        break;
                    case "daystounban":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("daystounban", words[1], ulang);
                        break;
                    case "newjoinsilence":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("newjoinsilence", words[1], ulang);
                        break;
                    case "setNewJoinSilentTime":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_njst_setup), csettings.AntiSpam.NewJoinSilentTime);
                        List<int> njstm = new() { 10, 30, 60, 120, 180 };
                        foreach (int njstv in njstm)
                            inlineMarkup.AddButton(njstv.ToString(), string.Format("newjoinsilence={0}", njstv));
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_turn_off), "newjoinsilence=0");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "setSpamScoreForward":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_spamscorefwd_setup), csettings.AntiSpam.SpamScoreForward);
                        for (int i = 0; i <= 5; i++)
                            inlineMarkup.AddButton($"{i}", $"spamscorefwd={i}");
                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "spamscorefwd":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("spamscorefwd", words[1], ulang);
                        break;
                    case "setSpamScoreValue":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_spamscore_setup), csettings.AntiSpam.SpamScoreValue);
                        for (int i = 0; i < 15; i++)
                        {
                            inlineMarkup.AddButton($"{i}", $"spamscore={i}");
                            if (i % 5 == 4)
                                inlineMarkup.AddNewRow();
                        }

                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "spamscore":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("spamscore", words[1], ulang);
                        break;
                    case "setMaxReactions":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_maxreactions_setup), csettings.AntiSpam.MaxReactions);
                        for (int i = 0; i < 10; i++)
                        {
                            inlineMarkup.AddButton($"{i * 5}", $"maxreactions={i * 5}");
                            if (i % 5 == 4)
                                inlineMarkup.AddNewRow();
                        }

                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "maxreactions":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("maxreactions", words[1], ulang);
                        break;
                    case "setReactionsInterval":
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_reactions_interval_setup), csettings.AntiSpam.ReactionsInterval);
                        for (int i = 0; i < 10; i++)
                        {
                            inlineMarkup.AddButton($"{i * 12}", $"reactionsinterval={i * 12}");
                            if (i % 5 == 4)
                                inlineMarkup.AddNewRow();
                        }

                        inlineMarkup.AddNewRow();
                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "reactionsinterval":
                        if (words.Length > 1)
                            csettings.AntiSpam.SetSettings("reactionsinterval", words[1], ulang);
                        break;
                    case "setReportChatId":
                        long tid = csettings.AntiSpam.ReportChatId;
                        string chatTarget = Localization.GetText(ulang, LangEnum.s_s_chat_not_selected);
                        if (tid == csettings.ChatId || tid == 0)
                            chatTarget = csettings.ChatTitle;
                        if (tid == userId)
                            chatTarget = Localization.GetText(ulang, LangEnum.s_s_in_own_pm);
                        if (settings.Channels.ContainsKey(tid))
                            chatTarget = settings.Channels[tid].ChatTitle;
                        if (csettings.ChatAdmins.Contains(tid))
                            chatTarget = Localization.GetText(ulang, LangEnum.s_s_in_other_pm);
                        reply = string.Format(Localization.GetText(ulang, LangEnum.s_s_as_report_target_setup), chatTarget);

                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_in_my_pm), $"reportto={userId}");
                        inlineMarkup.AddNewRow();
                        foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels)
                        {
                            if (!channel.Value.ChatAdmins.Contains(userId))
                                continue;
                            inlineMarkup.AddButton($"{channel.Value.ChatTitle}", $"reportto={channel.Key}");
                            inlineMarkup.AddNewRow();
                        }

                        inlineMarkup.AddButton(Localization.GetText(ulang, LangEnum.s_s_back), "antispam");
                        await bot.EditMessageText(chatId: userId, messageId, reply, replyMarkup: inlineMarkup, parseMode: ParseMode.Html);
                        return;
                    case "reportto":
                        if (words.Length > 1)
                        {
                            if (long.TryParse(words[1], out long chatid))
                                if (chatid == userId || (settings.Channels.ContainsKey(chatid) && settings.Channels[chatid].ChatAdmins.Contains(userId)))
                                    csettings.AntiSpam.ReportChatId = chatid;
                        }

                        break;
                }

                cb = "antispam";
                goto case SettingsState.MenuSettings;
            }
            default:
                return;
        }
    }

    public async Task Close(TelegramBotClient bot)
    {
        previousState = currentState = SettingsState.None;
        if (messageId != 0)
            await bot.DeleteMessage(userId, messageId);
        messageId = 0;
    }
}

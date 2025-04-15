using System.Text;
using FlexLiveBot;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using NLog;
using System.Web;

ReactionTypeEmoji heartReaction = new() { Emoji = "❤" };
ReactionTypeEmoji likeReaction = new() { Emoji = "👍" };
ReactionTypeEmoji dislikeReaction = new() { Emoji = "👎" };

// ReSharper disable UseStringInterpolation
TaskFactory m_task_factory = new();

RuntimeSettings runtimeSettings = RuntimeSettings.Load();
Settings settings = Settings.Load();

LogConfiguration.ConfigureClassLogger("lastochka.log", "lastochka.spam.log");
Localization.LoadLocalization();
ChannelScore channelScore = new();
UserScore userScore = new();
Antispam antispam = new();
Antibadwords antibadwords = new();
EmojiVotes emojiVotes = new();
Logger Log = LogManager.GetCurrentClassLogger();
Logger spamLog = LogManager.GetLogger("spamlog");
CancellationTokenSource cts = new();
string botToken = runtimeSettings.BotToken.Contains(':')
    ? runtimeSettings.BotToken
    : string.Format("{0}:{1}", runtimeSettings.OwnUID, runtimeSettings.BotToken);
TelegramBotClient bot = new(botToken, cancellationToken: cts.Token);

List<long> m_tg_whitelist = new()
{
    777000, // Telegram
    136817688, // ChannelBot
    1087968824 // GroupAnonymousBot
};

EventWaitHandle waitHandle = new(false, EventResetMode.AutoReset, null, out bool createdNew);
bool signaled;

User me = await bot.GetMe();
bot.OnError += OnError;
bot.OnMessage += OnMessage;
bot.OnUpdate += OnUpdate;


Log.Info(string.Format("@{0} is running... Process !shutdown in pm to terminate", me.Username));
if (!createdNew)
    waitHandle.Set();
else
    do
    {
        signaled = waitHandle.WaitOne(TimeSpan.FromMilliseconds(33));
    } while (!signaled);


/* here goes exit */
cts.Cancel(); // stop the bot
emojiVotes.Save();
settings.Save();
antispam.Save();
userScore.SaveMetrics();
userScore.SaveBanlist();
antibadwords.Save("ru");
channelScore.Save();

/* Logic goes here */

async Task<string> ProcessChatAdmins(long chatId)
{
    ChatFullInfo chatinfo = await bot.GetChat(chatId);
    ChatMember[] chatadmins = await bot.GetChatAdministrators(chatId);
    StringBuilder stringBuilder = new();
    List<long> adminuids = new();
    foreach (ChatMember mem in chatadmins)
    {
        if (mem.User.Id == runtimeSettings.OwnUID)
            continue;
        adminuids.Add(mem.User.Id);
        stringBuilder.AppendLine(mem.User.ToString());
    }

    ChannelSettings channelSettings = settings.SetupChannelSettings(chatId);
    channelSettings.ChatType = chatinfo.Type;
    if (channelSettings.ChatAdmins is null)
        channelSettings.ChatAdmins = new List<long>();
    channelSettings.ChatAdmins.Clear();
    channelSettings.ChatAdmins.AddRange(adminuids);

    if (chatinfo.Type is ChatType.Channel or ChatType.Group or ChatType.Supergroup)
    {
        channelSettings.ChatTitle = chatinfo.Title ?? string.Empty;
        channelSettings.UserName = chatinfo.Username ?? string.Empty;
    }

    settings.Save();
    return stringBuilder.ToString();
}

string GetChatName(long chatId)
{
    if (settings.Channels.TryGetValue(chatId, out ChannelSettings value))
    {
        if (!string.IsNullOrEmpty(value.ChatTitle))
            return value.ChatTitle;
        if (!string.IsNullOrEmpty(value.UserName))
            return value.UserName;
    }

    return chatId.ToString();
}

async Task GetChatAdmins(string[] strings, Message message)
{
    if (strings.Length < 2)
    {
        await bot.SendMessage(message.Chat, "usage - /getadmins chatId", messageThreadId: message.MessageThreadId);
        return;
    }

    string result;
    if (long.TryParse(strings[1], out long chatId))
        result = await ProcessChatAdmins(chatId);
    else
        result = "getadmins chatId parse error";

    await bot.SendMessage(message.Chat, result, messageThreadId: message.MessageThreadId);
}

string GetUserFromId(long uid)
{
    UserMetrics umetrics = userScore.GetUserMetrics(uid);
    if (umetrics.UserId != uid) return string.Format("<a href=\"tg://user?id={0}\">{1}</a>", uid, uid);
    List<string> items = new();
    if (!string.IsNullOrEmpty(umetrics.FirstName)) items.Add(umetrics.FirstName);
    if (!string.IsNullOrEmpty(umetrics.LastName)) items.Add(umetrics.LastName);
    if (!string.IsNullOrEmpty(umetrics.UserName)) items.Add(string.Format("@{0}", umetrics.UserName));
    string result = string.Join(' ', items);
    return string.Format("<a href=\"tg://user?id={0}\">{1}</a>", uid, string.IsNullOrEmpty(result) ? uid.ToString() : HttpUtility.HtmlEncode(result));
}

string GetUserFrom(User from)
{
    List<string> items = new();
    if (!string.IsNullOrEmpty(from.FirstName)) items.Add(from.FirstName);
    if (!string.IsNullOrEmpty(from.LastName)) items.Add(from.LastName);
    if (!string.IsNullOrEmpty(from.Username)) items.Add(string.Format("@{0}", from.Username));
    string result = string.Join(' ', items);
    return string.IsNullOrEmpty(result) ? from.Id.ToString() : result;
}

string GetUserNameFrom(User from)
{
    List<string> items = new();
    if (!string.IsNullOrEmpty(from.FirstName)) items.Add(from.FirstName);
    if (!string.IsNullOrEmpty(from.LastName)) items.Add(from.LastName);
    if (!string.IsNullOrEmpty(from.Username)) items.Add(from.Username);
    string result = string.Join(' ', items);
    return string.IsNullOrEmpty(result) ? from.Id.ToString() : result;
}

string GetUserFromForReply(User from)
{
    return string.Format("<a href=\"tg://user?id={0}\">id{1}</a>", from.Id, from.Id);
    /*return string.IsNullOrEmpty(from.Username)
        ? string.Format("<a href=\"tg://user?id={0}\">{1}</a>", from.Id, from.FirstName)
        : string.Format("<a href=\"tg://user?id={0}\">{1}</a> @{2}", from.Id, from.FirstName, from.Username);*/
}

string BeatufyNumber(int num)
{
    int _num = num % 10;
    return _num switch
    {
        2 or 3 or 4 => "раза",
        _ => "раз"
    };
}


async Task<bool> ProcessMessageNoText(Message message, StringBuilder stringBuilder)
{
    Log.Info("ProcessMessageNoText");
    if (message.From is null) return true;
    if (message.Chat.Type == ChatType.Private || !settings.Channels.ContainsKey(message.Chat.Id)) return false;
    ChannelSettings csettings = settings.Channels[message.Chat.Id];
    if (csettings.ChatType != message.Chat.Type) csettings.ChatType = message.Chat.Type;
    if (csettings.ChatType == ChatType.Channel) return true;
    if (!csettings.AntiSpam.Enabled) return true;
    if (message.Type is MessageType.NewChatMembers or MessageType.LeftChatMember)
    {
        if (userScore.BanTimes(message.From.Id) > 0)
        {
            await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
            Log.Info("message deleted");
            return true;
        }

        string fullUserName = GetUserNameFrom(message.From);

        bool isBannedName = antispam.HasBanwords(fullUserName);
        string[] testwords = fullUserName.Split(' ');
        foreach (string testword in testwords)
        {
            string teststr = testword.ToLower();
            if (!teststr.StartsWith('@')) continue;
            if (!teststr.EndsWith("bot")) continue;
            isBannedName = true;
            break;
        }

        Log.Info(string.Format("Checking fullname: {0}, result: {1}", fullUserName, isBannedName));
        if (isBannedName)
        {
            userScore.AddToBanlist(message.From.Id);
            string logstr = string.Format("FullUserName {0} contains banned word", fullUserName);
            Log.Info(logstr);
            await bot.BanChatMember(message.Chat.Id, message.From.Id, DateTime.Now.AddDays(csettings.AntiSpam.DaysToUnban), true);
            await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
            Log.Info("message deleted");
            return true;
        }
    }


    if (csettings.AntiSpam.SpamScoreValue == 0) return true;
    if (m_tg_whitelist.Contains(message.From.Id)) return false;

    stringBuilder.AppendLine(string.Format("id:{0} {1} uname:{2} firstname:{3} lastname:{4} isbot:{5} isprem:{6}",
        message.From.Id, GetUserFrom(message.From), message.From.Username, message.From.FirstName, message.From.LastName, message.From.IsBot,
        message.From.IsPremium));
    if (message.ReplyToMessage != null)
    {
        stringBuilder.AppendLine(string.Format("reply to message [{0}]:{1} says {2}", message.ReplyToMessage.MessageId, message.ReplyToMessage.From,
            message.ReplyToMessage.Text));
    }

    int spamscore = 0;
    bool haveMedia = message.Story is not null || message.Audio is not null || message.Sticker is not null || message.Video is not null ||
                     message.VideoNote is not null || message.Document is not null || message.Dice is not null || message.Game is not null ||
                     message.Poll is not null || message.Location is not null || message.Photo is not null || message.Contact is not null;

    spamscore = ProcessForwarded(message, csettings, spamscore, stringBuilder);

    userScore.UpdateMemberMetrics(message.From, message.Chat.Id, 0);
    UserMetrics umetrics = userScore.GetUserMetrics(message.From.Id);
    PerChannelMetrics cmetrics = userScore.GetUserChannelMetrics(message.From.Id, message.Chat.Id);
    bool dopunish = false;
    if (umetrics.UserId != 0 && cmetrics.JoinDate is not null && csettings.AntiSpam.NewJoinSilentTime > 0)
    {
        DateTime dt = cmetrics.JoinDate.Value;
        if (dt.AddMinutes(csettings.AntiSpam.NewJoinSilentTime) > DateTime.Now)
        {
            if (message.ForwardFrom != null)
            {
                stringBuilder.AppendLine(string.Format("С момента знакомства еще не прошло {0} минут, а пользователь уже репостит",
                    csettings.AntiSpam.NewJoinSilentTime));
                spamscore += csettings.AntiSpam.SpamScoreValue;
            }

            if (message.Entities != null)
            {
                foreach (MessageEntity ent in message.Entities)
                {
                    if (ent.Type is not (MessageEntityType.TextLink or MessageEntityType.Url)) continue;
                    if (ent.Url is not null && ent.Url.Contains("127.0.0.")) continue;
                    haveMedia = true;
                    break;
                }
            }

            if (haveMedia)
            {
                stringBuilder.AppendLine(string.Format("С момента знакомства еще не прошло {0} минут, а пользователь уже постит ссылки",
                    csettings.AntiSpam.NewJoinSilentTime));
                spamscore += csettings.AntiSpam.SpamScoreValue;
            }

            dopunish = true;
        }
    }

    if (haveMedia && umetrics.Score < 1)
    {
        if (!dopunish && umetrics.Score > -3)
        {
            await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
            await ReportPost(csettings,
                string.Format("Сообщение без текста от пользователя {0} отмечено как спам и удалено", GetUserFromForReply(message.From)));
            umetrics.Score--;
            stringBuilder.AppendLine(string.Format("message deleted, userscore decreased to {0}", umetrics.Score));
            return true;
        }

        spamscore += csettings.AntiSpam.SpamScoreValue;
        stringBuilder.AppendLine("msg without text with media for untrusted user affix used");
    }

    if (spamscore > 1 && message.From.IsPremium && csettings.AntiSpam.PremiumAffix && dopunish)
    {
        spamscore++;
        stringBuilder.AppendLine("Premium affix used");
    }

    stringBuilder.AppendLine(string.Format("Message spam score {0} of {1}", spamscore, csettings.AntiSpam.SpamScoreValue));
    if (antispam.IsSuspect(message.From.Id))
    {
        stringBuilder.AppendLine("User in suspect list, possible spamer, suspect affix used!");
        spamscore++;
    }

    Log.Info(stringBuilder.ToString());

    if (csettings.ChatAdmins.Contains(message.From.Id)
        || message.From.Id == runtimeSettings.OwnerUID
        || csettings.ChatFriends.Contains(message.From.Id)) return false;

    userScore.AdjustScore(message.From.Id, ref spamscore, csettings.AntiSpam.SpamScoreValue);


    if (spamscore >= csettings.AntiSpam.SpamScoreValue)
    {
        await AntispamPunish(csettings, message);
    }
    else if (message.Voice is not null)
    {
        await bot.SetMessageReaction(message.Chat.Id, message.MessageId, reaction: new[] { dislikeReaction });
        Log.Info(string.Format("SetReaction dislike on messageId {0} in chat {1}", message.MessageId, message.Chat.Id));
    }

    return true;
}

async Task AntispamPunish(ChannelSettings cs, Message msg, bool isFromReport = false)
{
    if (msg.From is null) return;
    if (cs.ChatAdmins.Contains(msg.From.Id)) return;
    if (cs.ChatFriends.Contains(msg.From.Id))
    {
        if (cs.AntiSpam is { Enabled: true, ReactOnFriends: true })
        {
            try
            {
                await bot.DeleteMessage(chatId: msg.Chat.Id, messageId: msg.MessageId);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error(ex.StackTrace);
            }

            if (!isFromReport)
                await PostToChat(msg.Chat.Id, msg.MessageThreadId, "Сообщение отмечено как спам и удалено");
        }

        return;
    }

    if (!cs.AntiSpam.Enabled)
    {
        bool isAdminInOtherChat = false;
        foreach (ChannelSettings channel in settings.Channels.Values)
        {
            if (!channel.ChatAdmins.Contains(msg.From.Id)) continue;
            isAdminInOtherChat = true;
            break;
        }

        if (!isAdminInOtherChat) antispam.AddSuspect(msg.From.Id);
        return;
    }

    int days = cs.AntiSpam.DaysToUnban;
    userScore.AddToBanlist(msg.From.Id);
    if (days < 365)
        days *= userScore.BanTimes(msg.From.Id);
    try
    {
        await bot.BanChatMember(msg.Chat.Id, msg.From.Id, DateTime.Now.AddDays(days), revokeMessages: true);
    }
    catch (Exception ex)
    {
        Log.Error(ex.Message);
        Log.Error(ex.StackTrace);
    }

    MessageId reportMessageId = null;
    try
    {
        reportMessageId = await bot.CopyMessage(chatId: runtimeSettings.ReportUID, msg.Chat.Id, msg.MessageId);
    }
    catch (Exception ex)
    {
        Log.Error(ex.Message);
        Log.Error(ex.StackTrace);
    }

    try
    {
        await bot.DeleteMessage(msg.Chat.Id, msg.MessageId);
    }
    catch (Exception ex)
    {
        Log.Error(ex.Message);
        Log.Error(ex.StackTrace);
    }

    if (cs.AntiSpam.ReportEnabled)
    {
        StringBuilder rsb = new();
        rsb.Append(string.Format("Пользователь {0} забанен в чате {1} за спам. ", GetUserFromForReply(msg.From), cs.ChatTitle));
        if (reportMessageId is not null)
            rsb.Append(string.Format(runtimeSettings.ReportLink, reportMessageId.Id));
        if (cs.AntiSpam.ReportChatId == 0 || cs.AntiSpam.ReportChatId == msg.Chat.Id)
            await PostToChat(msg.Chat.Id, msg.MessageThreadId, string.Format("Пользователь {0} забанен за спам.", GetUserFromForReply(msg.From)));
        else
            await PostToChat(cs.AntiSpam.ReportChatId, null, rsb.ToString());
    }

    if (!cs.AntiSpam.Solidarity || isFromReport)
        return;
    foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels)
    {
        if (channel.Key == msg.Chat.Id || !channel.Value.AntiSpam.Enabled || !channel.Value.AntiSpam.Solidarity) continue;
        if (channel.Value.ChatAdmins.Contains(msg.From.Id) || channel.Value.ChatFriends.Contains(msg.From.Id)) continue;
        await bot.BanChatMember(channel.Key, msg.From.Id, DateTime.Now.AddDays(channel.Value.AntiSpam.DaysToUnban), true);
        if (!channel.Value.AntiSpam.ReportEnabled || channel.Value.AntiSpam.SilentSolidarity) continue;
        long targetChannel = (channel.Value.AntiSpam.ReportChatId == 0 || channel.Value.AntiSpam.ReportChatId == channel.Key)
            ? channel.Key
            : channel.Value.AntiSpam.ReportChatId;
        await PostToChat(targetChannel, null, string.Format("Пользователь {0} забанен из солидарности за спам в другом чате.", GetUserFromForReply(msg.From)));
    }
}

async Task<bool> ProcessBadWords(Message message)
{
    Log.Info(string.Format("ProcessBadWords: {0}", message.Text));
    if (string.IsNullOrEmpty(message.Text) || message.From is null)
    {
        Log.Info("bw: empty message");
        return true;
    }

    ChannelSettings cs = settings.Channels[message.Chat.Id];
    int bwscore = antibadwords.GetScore(message.Text);
    Log.Info("bw score is {0}", bwscore);
    bool isFriend = cs.ChatFriends.Contains(message.From.Id);
    if (bwscore > 0)
    {
        Log.Info(string.Format("bwscore is {0}", bwscore));
        await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
        string bwbanlog = cs.AntiBadWords.BanTime > 0
            ? string.Format("Пользователь {0} забанен за мат на {1} минут!", GetUserFromForReply(message.From), cs.AntiBadWords.BanTime)
            : string.Format("Пользователь {0} забанен за мат!", GetUserFromForReply(message.From));

        if (cs.AntiBadWords.WarnsEnabled)
        {
            if (antibadwords.WarnTimes(message.From.Id) > cs.AntiBadWords.WarnTimes && !isFriend)
            {
                await bot.BanChatMember(chatId: message.Chat.Id, userId: message.From.Id, DateTime.Now.AddMinutes(cs.AntiBadWords.BanTime),
                    revokeMessages: false);
            }
            else
            {
                antibadwords.Warn(message.From.Id);
                string tlast = string.Empty;
                if (!isFriend)
                {
                    int last = cs.AntiBadWords.WarnTimes - antibadwords.WarnTimes(message.From.Id);
                    if (last > 0)
                        tlast = string.Format("Еще {0} {1} и последует бан!", last, BeatufyNumber(last));
                    else if (last == 0)
                        tlast = "Последнее предупрежедние!";
                }

                bwbanlog = string.Format("Предупреждение пользователю {0} за ненормативную лексику. {1}", GetUserFromForReply(message.From), tlast);
            }
        }
        else
        {
            if (!isFriend)
                await bot.BanChatMember(chatId: message.Chat.Id, userId: message.From.Id, DateTime.Now.AddMinutes(cs.AntiBadWords.BanTime),
                    revokeMessages: false);
        }

        Message _msgToCleanup = await bot.SendMessage(chatId: message.Chat.Id, bwbanlog, messageThreadId: message.MessageThreadId, parseMode: ParseMode.Html);
        if (cs.AntiBadWords.AutoCleanup > 0)
            await m_task_factory.StartNew(async () => { await CleanupMessage(_msgToCleanup, cs.AntiBadWords.AutoCleanup); });
        Log.Info(bwbanlog);
        return true;
    }

    return false;
}


int ProcessForwarded(Message message1, ChannelSettings channelSettings, int spamscore, StringBuilder stringBuilder1)
{
    if (message1.ForwardFrom == null && message1.ForwardFromChat == null)
        return spamscore;
    bool ignoreCondition = false;
    switch (message1.ForwardOrigin)
    {
        case MessageOriginChat moch:
            if (message1.ForwardFromChat != null && message1.ForwardFromChat.Id == message1.Chat.Id && !channelSettings.AntiSpam.CountCurrentForward)
                ignoreCondition = true;
            Log.Info(string.Format("Forwarded on behalf of {0}", moch.SenderChat));
            break;
        case MessageOriginUser mou:
            if (message1.From != null
                && message1.ForwardFrom?.Id == message1.From.Id
                && !channelSettings.AntiSpam.CountOwnForward)
                ignoreCondition = true;
            Log.Info(string.Format("Forwarded from user {0}", mou.SenderUser));
            break;
        case MessageOriginChannel moc:
            if (message1.ForwardFromChat != null && message1.ForwardFromChat.Id == message1.Chat.Id && !channelSettings.AntiSpam.CountCurrentForward)
                ignoreCondition = true;
            Log.Info(string.Format("Forwarded from channel {0}", moc.Chat.Title));
            break;
        case MessageOriginHiddenUser mohu:
            Log.Info(string.Format("Forwarded from hidden user {0}", mohu.SenderUserName));
            break;
    }

    if (message1.ReplyMarkup is not null) spamscore += channelSettings.AntiSpam.SpamScoreValue;

    if (!ignoreCondition) spamscore += channelSettings.AntiSpam.SpamScoreForward;
    if (message1.ForwardFrom != null) stringBuilder1.AppendLine(string.Format("ForwardFrom {0}", GetUserFrom(message1.ForwardFrom)));
    if (message1.ForwardFromChat != null) stringBuilder1.AppendLine(string.Format("ForwardFromChat {0}", message1.ForwardFromChat));
    return spamscore;
}

async Task<bool> ProcessMessage(Message message, StringBuilder stringBuilder)
{
    Log.Info("ProcessMessage");
    /* сообщения в личке не проверяются на спам, но могут быть расценены как команды, потому выходим без прерывания процесса обработки*/
    if (message.Chat.Type == ChatType.Private || !settings.Channels.ContainsKey(message.Chat.Id)) return false;
    ChannelSettings csettings = settings.Channels[message.Chat.Id];
    if (csettings.ChatType != message.Chat.Type) csettings.ChatType = message.Chat.Type;
    /* антиспам для каналов типа "лента" не имеет смысла - права на постинг сообщений только у админов, при этом постинг происходит анонимно\от имени группового бота, выходим с прерыванием обработки */
    if (csettings.ChatType == ChatType.Channel) return true;

    if (message.From is null || message.Text is null || !csettings.AntiSpam.Enabled || csettings.AntiSpam.SpamScoreValue == 0 ||
        m_tg_whitelist.Contains(message.From.Id)) return true;
    UserMetrics umetrics = userScore.GetUserMetrics(message.From.Id);
    umetrics.LastMessages.Enqueue(new()
    {
        ChatId = message.From.Id,
        Date = message.Date,
        MessageId = message.MessageId,
        Text = message.Text
    });

    if (csettings.AntiBadWords.Enabled && await ProcessBadWords(message))
        return true;


    stringBuilder.AppendLine(GetUserFrom(message.From));
    stringBuilder.AppendLine(string.Format("id:{0} uname:{1} firstname:{2} lastname:{3} isbot:{4} isprem:{5}",
        message.From.Id, message.From.Username, message.From.FirstName, message.From.LastName, message.From.IsBot, message.From.IsPremium));
    if (message.ReplyToMessage != null)
    {
        Log.Info(string.Format("reply to message [{0}]:{1} says {2}",
            message.ReplyToMessage.MessageId, message.ReplyToMessage.From, message.ReplyToMessage.Text));
    }

    int spamscore = antispam.GetScore(message, "ru", new(), csettings.AntiSpam.SpamScoreValue);
    int baseScore = spamscore;
    string emojis = antispam.AdjustEmojis(message.Text, "ru", ref spamscore, out int foundEmojis, 2, 4);
    int foundWrongUnicode = antispam.GetUnicodeChars(message.Text);
    int delta = foundWrongUnicode - foundEmojis;
    Log.Info(string.Format("Unsupported unicode chars: {0}", delta));
    spamscore += delta / 3;
    stringBuilder.Append(emojis);

    MessageItem[] lastMessages = umetrics.LastMessages.ToArray();
    List<MessageItem> sameItems = new();
    double deltaSeconds = 0;
    for (int i = 0; i < lastMessages.Length - 1; i++)
    {
        if (lastMessages[i].Text.Equals(lastMessages[i + 1].Text))
            sameItems.Add(lastMessages[i]);
        deltaSeconds += (lastMessages[i + 1].Date - lastMessages[i].Date).TotalSeconds;
    }

    bool floodDetected = lastMessages.Length > 5 && Math.Abs(deltaSeconds / lastMessages.Length) < 1.0f;
    if (floodDetected)
    {
        spamscore += 3;
        Log.Info(string.Format("Flood detected at rate {0} messages per {1} seconds", lastMessages.Length, deltaSeconds));
    }

    if (sameItems.Count > 1)
    {
        spamscore += sameItems.Count;
        Log.Info(string.Format("Repeating messages {0} times", sameItems.Count));
    }

    spamscore = ProcessForwarded(message, csettings, spamscore, stringBuilder);


    userScore.UpdateMemberMetrics(message.From, message.Chat.Id, 1);
    PerChannelMetrics cmetrics = userScore.GetUserChannelMetrics(message.From.Id, message.Chat.Id);
    Log.Info(string.Format("Umetrics: UserId :{0}, JoinDate: {1}, NJST: {2}", umetrics.UserId, cmetrics.JoinDate, csettings.AntiSpam.NewJoinSilentTime));
    bool haveUrls = HaveUrls(message);

    if (umetrics.UserId != 0 && cmetrics.JoinDate is not null && csettings.AntiSpam.NewJoinSilentTime > 0 && spamscore < csettings.AntiSpam.SpamScoreValue)
    {
        DateTime dt = cmetrics.JoinDate.Value;
        if (dt.AddMinutes(csettings.AntiSpam.NewJoinSilentTime) > DateTime.Now)
        {
            if (message.ForwardFrom != null)
            {
                if (umetrics.Score is < 1 and > -3 && baseScore == 0)
                {
                    await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
                    await ReportPost(csettings, string.Format("Репост от нового пользователя {0} удален", GetUserFromForReply(message.From)));
                    umetrics.Score--;
                    stringBuilder.AppendLine(string.Format("message deleted, userscore decreased to {0}", umetrics.Score));
                    return true;
                }

                stringBuilder.AppendLine(string.Format("С момента знакомства еще не прошло {0} минут, а пользователь уже репостит",
                    csettings.AntiSpam.NewJoinSilentTime));
                spamscore += csettings.AntiSpam.SpamScoreValue;
            }

            if (message.Text.Contains('@'))
            {
                if (userScore.HaveKnownUsername(message.Text, message.Chat.Id, runtimeSettings.OwnerUserName))
                    haveUrls = true;
            }

            if (antispam.ContainsUrl(message.Text) || haveUrls)
            {
                if (umetrics.Score is < 1 and > -3 && baseScore == 0)
                {
                    await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
                    await ReportPost(csettings,
                        string.Format("Сообщение от нового пользователя {0} содержит ссылки, удалено", GetUserFromForReply(message.From)));
                    umetrics.Score--;
                    stringBuilder.AppendLine(string.Format("message deleted, userscore decreased to {0}", umetrics.Score));
                    return true;
                }

                stringBuilder.AppendLine(string.Format("С момента знакомства еще не прошло {0} минут, а пользователь уже постит ссылки",
                    csettings.AntiSpam.NewJoinSilentTime));
                spamscore += csettings.AntiSpam.SpamScoreValue;
            }
        }
    }

    if (spamscore > 1 && message.From.IsPremium && csettings.AntiSpam.PremiumAffix)
    {
        spamscore++;
        stringBuilder.AppendLine("Premium affix used");
    }

    stringBuilder.AppendLine(string.Format("Message spam score {0} of {1}", spamscore, csettings.AntiSpam.SpamScoreValue));
    if (antispam.IsSuspect(message.From.Id))
    {
        stringBuilder.AppendLine("User in suspect list, possible spamer, suspect affix used!");
        spamscore++;
    }

    Log.Info(stringBuilder.ToString());
    channelScore.AddMessageMetric(message.Chat.Id, spamscore, csettings.AntiSpam.SpamScoreValue);
    if (csettings.ChatAdmins.Contains(message.From.Id) || message.From.Id == runtimeSettings.OwnerUID)
        return false;
    if (!csettings.ChatFriends.Contains(message.From.Id))
        if (userScore.AdjustScore(message.From.Id, ref spamscore, csettings.AntiSpam.SpamScoreValue))
            csettings.ChatFriends.Add(message.From.Id);
    bool isFriend = csettings.ChatFriends.Contains(message.From.Id);
    if (spamscore >= csettings.AntiSpam.SpamScoreValue)
    {
        spamLog.Warn(string.Format("Spam from {0} with score {1}: ", GetUserFrom(message.From), spamscore));
        spamLog.Warn(message.Text);
        await AntispamPunish(csettings, message);
        if (sameItems.Count > 0)
        {
            foreach (MessageItem item in sameItems)
            {
                if (settings.Channels.ContainsKey(item.ChatId) && settings.Channels[item.ChatId].AntiSpam.Enabled)
                    await bot.DeleteMessage(chatId: item.ChatId, messageId: item.MessageId);
            }
        }
        else if (floodDetected)
        {
            while (umetrics.LastMessages.Any())
            {
                MessageItem item = umetrics.LastMessages.Dequeue();
                if (settings.Channels.ContainsKey(item.ChatId) && settings.Channels[item.ChatId].AntiSpam.Enabled)
                    await bot.DeleteMessage(chatId: item.ChatId, messageId: item.MessageId);
            }
        }
    }

    return !isFriend;
}

async Task PunishForReactions(ChannelSettings channel, User from, bool solidarity)
{
    await bot.BanChatMember(chatId: channel.ChatId, userId: from.Id, DateTime.Now.AddDays(channel.AntiSpam.DaysToUnban), revokeMessages: true);
    string banlog;
    if (channel.AntiSpam.ReportChatId == 0 || channel.AntiSpam.ReportChatId == channel.ChatId)
        banlog = solidarity
            ? string.Format("Пользователь {0} забанен из солидарности за спам реакциями в другом чате!", GetUserFromForReply(from))
            : string.Format("Пользователь {0} забанен за спам реакциями!", GetUserFromForReply(from));
    else
        banlog = solidarity
            ? string.Format("Пользователь {0} забанен в чате {1} за спам реакциями в другом чате из солидарности!", GetUserFromForReply(from),
                channel.ChatTitle)
            : string.Format("Пользователь {0} забанен в чате {1} за спам реакциями", GetUserFromForReply(from), channel.ChatTitle);
    Log.Info(banlog);
    await ReportPost(channel, banlog);
}


async Task ReportPost(ChannelSettings channel, string banlog)
{
    if (!channel.AntiSpam.ReportEnabled) return;
    if (channel.AntiSpam.ReportChatId == 0 || channel.AntiSpam.ReportChatId == channel.ChatId)
    {
        await PostToChat(channel.ChatId, null, banlog);
    }
    else
    {
        await PostToChat(channel.AntiSpam.ReportChatId, null, banlog);
    }
}

bool CheckIfOwner(long userId) => userId == runtimeSettings.OwnerUID;

bool CheckIfFriendOfChat(long chatId, long userId)
{
    if (!settings.Channels.ContainsKey(chatId)) return false;
    return settings.Channels[chatId].ChatFriends.Contains(userId);
}

bool CheckIfAdminOfChat(long chatId, long userId)
{
    if (settings.Channels is null)
        settings.Channels = new Dictionary<long, ChannelSettings>();
    if (!settings.Channels.ContainsKey(chatId)) return false;
    if (settings.Channels[chatId].ChatAdmins is null)
        ProcessChatAdmins(chatId).Wait();
    return settings.Channels[chatId].ChatAdmins.Contains(userId);
}


async Task CleanupMessage(Message message, int timer)
{
    await Task.Delay(TimeSpan.FromMinutes(timer));
    try
    {
        await bot.DeleteMessage(chatId: message.Chat.Id, messageId: message.MessageId);
    }
    catch (Exception ex)
    {
        Log.Error(ex.Message);
        Log.Error(ex.StackTrace);
    }
}


int ParseChatId(bool IsPrivate, bool IsOwner, int start, ref long chatId, string[] words)
{
    if (IsPrivate && words.Length > start)
    {
        if (IsOwner && words[start].Equals("all"))
        {
            chatId = -1;
            start++;
            return start;
        }

        if (words[start].StartsWith('@'))
        {
            string testuid = words[start].Replace("@", string.Empty);
            foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels)
            {
                if (!channel.Value.UserName.Equals(testuid, StringComparison.InvariantCultureIgnoreCase)) continue;
                chatId = channel.Key;
                start++;
                break;
            }
        }
        else
        {
            long.TryParse(words[2], out chatId);
            start++;
        }
    }

    return start;
}

async Task OnMessage(Message msg, UpdateType type)
{
    await m_task_factory.StartNew(async () => await TaskOnMessage(msg, type));
}

bool HaveUrls(Message msg)
{
    if (msg.Entities is null || msg.Text is null) return false;
    foreach (MessageEntity ent in msg.Entities)
    {
        if (ent.Type == MessageEntityType.TextLink)
        {
            Log.Info(string.Format("msgEntity is TextLink and contains url: {0}", ent.Url));
            return true;
        }

        if (ent.Type == MessageEntityType.Url)
        {
            if (ent.Offset > 0 && (ent.Length + ent.Offset) < msg.Text.Length)
            {
                string substr = msg.Text.Substring(ent.Offset, ent.Length);
                Log.Info(string.Format("msgEntity is Url and url is: {0}", substr));
                return antispam.ContainsUrl(substr);
            }
        }
    }

    return false;
}


async Task TaskOnMessage(Message msg, UpdateType type)
{
    bool haveStory = msg.Type == MessageType.Story || msg.Story is not null;
    bool haveAudio = msg.Type == MessageType.Audio || msg.Audio is not null;
    bool haveSticker = msg.Type == MessageType.Sticker || msg.Sticker is not null;
    bool haveVideo = msg.Type == MessageType.Video || msg.Video is not null;
    bool haveVideoNote = msg.Type == MessageType.VideoNote || msg.VideoNote is not null;
    bool haveDocument = msg.Type == MessageType.Document || msg.Document is not null;
    bool haveDice = msg.Type == MessageType.Dice || msg.Dice is not null;
    bool haveGame = msg.Type == MessageType.Game || msg.Game is not null;
    bool havePoll = msg.Type == MessageType.Poll || msg.Poll is not null;
    bool haveLocation = msg.Type == MessageType.Location || msg.Location is not null;
    bool havePhoto = msg.Type == MessageType.Photo || msg.Photo is not null;
    bool haveContact = msg.Type == MessageType.Contact || msg.Contact is not null;

    if (msg.Text is null && !string.IsNullOrEmpty(msg.Caption)) msg.Text = msg.Caption;
    bool haveText = msg.Text is not null;

    StringBuilder sb = new();
    sb.AppendLine(string.Format("{0} type:{1} msgId {2} from {3} in {4}: ", type.ToString(), msg.Type.ToString(), msg.MessageId, msg.From, msg.Chat));
    /* messageType: NewChatMembers, LeftChatMember*/
    if (msg.ForwardFrom != null) sb.Append(string.Format("fwd userId {0} ", msg.ForwardFrom.Id));
    if (msg.ForwardFromChat != null) sb.Append(string.Format("fwd chatId {0} ", msg.ForwardFromChat.Id));
    if (msg.ForwardFromMessageId != null) sb.Append(string.Format("fwd msgId {0} ", msg.ForwardFromMessageId));
    if (msg.AuthorSignature != null) sb.Append(string.Format("authsign {0} ", msg.AuthorSignature));
    if (msg.ForwardSenderName != null) sb.Append(string.Format("fsn: {0} ", msg.ForwardSenderName));
    if (haveText) sb.AppendLine(msg.Text);
    else
    {
        sb.AppendLine(string.Format("Story: {0} Audio: {1} Sticker: {2} Video: {3} VideoNote: {4} Document: {5} ",
            haveStory, haveAudio, haveSticker, haveVideo, haveVideoNote, haveDocument));
        sb.AppendLine(string.Format("Dice: {0} Game: {1} Poll: {2} Location: {3} Photo: {4} Contact: {5} ",
            haveDice, haveGame, havePoll, haveLocation, havePhoto, haveContact));
    }

    if (msg.From is null)
    {
        if (msg.Chat.Type == ChatType.Channel) return;
        await bot.DeleteMessage(chatId: msg.Chat.Id, messageId: msg.MessageId);
        sb.AppendLine("message deleted");
        Log.Info(sb.ToString());
        return;
    }

    Log.Info(sb.ToString());
    sb.Clear();
    if (haveSticker)
    {
        Log.Info("message is sticker, skip");
        return;
    }

    long uid = msg.From.Id;
    if (!CheckIfAdminOfChat(msg.Chat.Id, uid))
    {
        Log.Debug("not an admin of chat");
        if (!haveText)
        {
            await ProcessMessageNoText(msg, sb);
            return;
        }

        if (await ProcessMessage(msg, sb)) return;
    }
    else
    {
        Log.Debug("admin of chat");
    }

    if (string.IsNullOrWhiteSpace(msg.Text))
    {
        Log.Info("no text, skip");
        return;
    }

    string[] words = msg.Text.Split(' ');
    if (words.Length == 0) return;

    bool IsPrivate = msg.Chat.Type is ChatType.Private or ChatType.Sender;
    bool IsOwner = CheckIfOwner(uid);
    long chatId = 0;
    if (!IsPrivate) chatId = msg.Chat.Id;

    string cmd = words[0];
    if (!cmd[0].Equals('/') && !cmd[0].Equals('!')) return;
    cmd = cmd.Remove(0, 1);

    UserMetrics uscore = userScore.GetUserMetrics(uid);
    string ulang = uscore.Locale;

    bool forcecmd = false;
    switch (cmd)
    {
        case "delfriend":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
            long targetUid = 0;
            if (msg.ReplyToMessage is { From: { } })
            {
                targetUid = msg.ReplyToMessage.From.Id;
            }

            if (words.Length < start && targetUid == 0) return;
            if (targetUid == 0)
            {
                if (words[start].StartsWith('@'))
                    targetUid = userScore.GetUidByUsername(words[start].Replace("@", string.Empty));
                else
                    long.TryParse(words[start], out targetUid);
            }

            if (targetUid == 0 || chatId is 0 or -1) return;
            if (settings.Channels.ContainsKey(chatId))
            {
                if (settings.Channels[chatId].ChatFriends.Remove(targetUid))
                {
                    Log.Info(string.Format("Пользователь {0} удален из списка друзей канала {1}", targetUid, chatId));
                    await PostToChat(chatId, null, "Пользователь удален из списка друзей чата");
                }
            }

            return;
        }
        case "listfriends":
        {
            ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;

            if (settings.Channels.ContainsKey(chatId) && settings.Channels[chatId].ChatFriends.Count > 0)
            {
                StringBuilder _sb = new();
                _sb.AppendLine("Друзья чата:");
                List<string> userfriends = new();

                foreach (long friend in settings.Channels[chatId].ChatFriends)
                    userfriends.Add(GetUserFromId(friend));

                string[] ufriends = userfriends.ToArray();
                int ix = 0;
                while (ix < ufriends.Length)
                {
                    _sb.AppendLine(ufriends[ix]);
                    ix++;
                    if (ix % 25 != 0)
                        continue;
                    await bot.SendMessage(msg.Chat.Id, _sb.ToString(), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    _sb.Clear();
                }

                if (_sb.Length > 0)
                {
                    try
                    {
                        await bot.SendMessage(msg.Chat.Id, _sb.ToString(), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    }
                    catch (Exception ex)
                    {
                        Log.Info(ex.Message);
                    }
                }
            }

            return;
        }
        case "addfriend":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
            long targetUid = 0;
            if (msg.ReplyToMessage is { From: { } })
            {
                targetUid = msg.ReplyToMessage.From.Id;
            }

            if (words.Length < start && targetUid == 0) return;
            if (targetUid == 0)
            {
                if (words[start].StartsWith('@'))
                    targetUid = userScore.GetUidByUsername(words[start].Replace("@", string.Empty));
                else
                    long.TryParse(words[start], out targetUid);
            }

            if (targetUid == 0 || chatId is 0 or -1) return;
            if (settings.Channels.ContainsKey(chatId))
            {
                if (!settings.Channels[chatId].ChatFriends.Contains(targetUid))
                {
                    settings.Channels[chatId].ChatFriends.Add(targetUid);
                    Log.Info(string.Format("Пользователь {0} добавлен в список друзей канала {1}", targetUid, chatId));
                    await PostToChat(msg.From.Id, msg.MessageThreadId, "Пользователь добавлен в список друзей чата");
                    if (!IsPrivate) await bot.DeleteMessage(msg.Chat.Id, msg.MessageId);
                }
            }

            return;
        }
        case "report":
        {
            Log.Info(string.Format("report called from {0} in {1}", uid, chatId));
            ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && !CheckIfAdminOfChat(chatId, uid) && !CheckIfFriendOfChat(chatId, uid))
            {
                Log.Info(string.Format("report: no rights for user {0}", uid));
                return;
            }

            if (msg.ReplyToMessage is null)
            {
                Log.Info("report: no reply message");
                return;
            }

            if (settings.Channels.ContainsKey(chatId))
            {
                if (!settings.Channels[chatId].AntiSpam.Enabled)
                    return;
            }

            long targetUid = 0;
            int targetMsgId = 0;
            if (msg.ReplyToMessage is { From: { } })
            {
                targetUid = msg.ReplyToMessage.From.Id;
                targetMsgId = msg.ReplyToMessage.MessageId;
            }

            if (targetUid == 0 || targetMsgId == 0 || targetUid == runtimeSettings.OwnUID || targetUid == runtimeSettings.OwnerUID)
            {
                Log.Info("report: targetUid == 0 || targetMsgId == 0 || targetUid == ownUid || targetUid == ownerUid ");
                return;
            }

            if (msg.ReplyToMessage.From is { IsBot: true, Username: "GroupAnonymousBot" })
            {
                Log.Info("report: reply origin is GroupAnonymousBot");
                return;
            }

            if (uid == targetUid)
            {
                Log.Info("report: self reporting");
                return;
            }

            channelScore.AddReportMetric(chatId);
            await m_task_factory.StartNew(async () =>
            {
                try
                {
                    string reply = string.Format("Пользователь {0} в чате {1} зарепортил пост от {2} #{3}", GetUserFromId(uid), GetChatName(chatId),
                        GetUserFromId(targetUid), targetMsgId);
                    await bot.SendMessage(runtimeSettings.OwnerUID, reply, disableNotification: true, parseMode: ParseMode.Html);
                    await bot.CopyMessage(runtimeSettings.OwnerUID, chatId, targetMsgId, disableNotification: true);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    Log.Error(ex.StackTrace);
                }
            });
            await m_task_factory.StartNew(async () =>
            {
                try
                {
                    if (settings.Channels.TryGetValue(chatId, out ChannelSettings cs))
                        await AntispamPunish(cs, msg.ReplyToMessage);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    Log.Error(ex.StackTrace);
                }
            });
            await m_task_factory.StartNew(async () =>
            {
                try
                {
                    await PostToChat(chatId, msg.MessageThreadId, "Жалоба на сообщение принята, сообщение отправлено на анализ и удалено.");
                    await bot.DeleteMessage(chatId, msg.MessageId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    Log.Error(ex.StackTrace);
                }
            });
            return;
        }
        case "clear":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner) return;
            if (words.Length < start) return;
            int.TryParse(words[start], out int targetUid);
            if (targetUid == 0 || !settings.Channels.ContainsKey(chatId)) return;
            await bot.DeleteMessage(chatId, targetUid);
            return;
        }
        case "ban":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner) return;
            if (words.Length < start) return;

            long targetUid;
            if (words[start].StartsWith('@'))
            {
                targetUid = userScore.GetUidByUsername(words[start].Replace("@", string.Empty));
            }
            else
            {
                long.TryParse(words[start], out targetUid);
            }

            if (targetUid == 0) return;


            if (chatId == -1)
            {
                foreach (KeyValuePair<long, ChannelSettings> kvp in settings.Channels.Where(kvp => kvp.Value.AntiSpam is { Enabled: true, Solidarity: true }))
                {
                    try
                    {
                        await bot.BanChatMember(kvp.Key, targetUid, DateTime.Now.AddDays(365), revokeMessages: true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                        //break;
                    }

                    //Log.Info(string.Format("Пользователь {0} забанен на канале {1} ({2})", targetUid, kvp.Value.ChatTitle, kvp.Key));
                }
            }
            else
            {
                await bot.BanChatMember(chatId, targetUid, DateTime.Now.AddDays(365), revokeMessages: true);
                //Log.Info(string.Format("Пользователь {0} забанен на канале {1}", targetUid, chatId));
            }

            return;
        }
        case "unban":
        {
            Log.Info("Unban called");
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid)))
            {
                Log.Info(string.Format("Unban no rights for uid {0} in chatId {1}", uid, chatId));
                return;
            }

            if (words.Length < start)
            {
                Log.Info("Unban words.length not enough");
                return;
            }

            long targetUid;
            if (words[start].StartsWith('@'))
                targetUid = userScore.GetUidByUsername(words[start].Replace("@", string.Empty));
            else
                long.TryParse(words[start], out targetUid);
            if (targetUid == 0)
            {
                Log.Info(string.Format("Unban no targetUid parsed from [{0}]", words[start]));
                return;
            }

            userScore.Unban(targetUid);
            userScore.GetUserMetrics(targetUid).LastMessages.Clear();
            if (chatId == -1)
            {
                foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels.Where(channel => channel.Value.AntiSpam.Solidarity))
                    try
                    {
                        await bot.UnbanChatMember(chatId: channel.Key, userId: targetUid, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.Message);
                        Log.Error(ex.StackTrace);
                    }
            }
            else
            {
                try
                {
                    await bot.UnbanChatMember(chatId: chatId, userId: targetUid, true);
                    Log.Info(string.Format("Пользователь {0} разбанен на канале {1}", targetUid, chatId));
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    Log.Error(ex.StackTrace);
                }
            }

            return;
        }
        case "lt":
        {
            if (IsOwner) Localization.CreateTestLocale1();
            return;
        }
        case "shutdown":
        {
            if (IsOwner) await m_task_factory.StartNew(async () => await SetShutdown());
            return;
        }
        case "as":
        case "antispam":
        {
            if (words.Length < 2) return;

            switch (words[1])
            {
                case "help":
                {
                    await bot.SendMessage(msg.Chat, Localization.GetText(ulang, LangEnum.s_antispam_help), messageThreadId: msg.MessageThreadId,
                        parseMode: ParseMode.Html);
                    break;
                }
                case "on":
                case "enable":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (IsOwner || (chatId != 0 && CheckIfAdminOfChat(chatId, uid)))
                    {
                        settings.Channels[chatId].AntiSpam.Enabled = true;
                        await bot.SendMessage(msg.Chat, "Антиспам на канале включен", messageThreadId: msg.MessageThreadId);
                    }

                    return;
                case "off":
                case "disable":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (IsOwner || (chatId != 0 && CheckIfAdminOfChat(chatId, uid)))
                    {
                        settings.Channels[chatId].AntiSpam.Enabled = false;
                        await bot.SendMessage(msg.Chat, "Антиспам на канале выключен", messageThreadId: msg.MessageThreadId);
                    }

                    return;
                case "set":
                case "mod":
                {
                    int start = ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    sb.Clear();
                    for (int i = start; i < words.Length; i++)
                    {
                        if (!words[i].Contains('=')) continue;
                        string[] keyvaluepair = words[i].Split('=');
                        bool showCmdStatus = keyvaluepair.Length == 1;
                        ChannelSettings channel = settings.Channels[chatId];
                        sb.AppendLine(channel.AntiSpam.SetSettings(keyvaluepair[0], showCmdStatus ? string.Empty : keyvaluepair[1], ulang));
                    }

                    string sbresult = sb.ToString();
                    if (!string.IsNullOrEmpty(sbresult))
                        await bot.SendMessage(msg.Chat, sbresult, messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    return;
                }
                case "unused":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.GetSpamUnused(ulang));
                    return;
                }
                case "spamtop":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.GetSpamTop(ulang));
                    return;
                }
                case "learntop":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.GetLearningTop(ulang));
                    return;
                }
                case "status":
                case "config":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    sb = new();
                    sb.AppendLine(string.Format("Антиспам: <b>{0}</b>", settings.Channels[chatId].AntiSpam.Enabled));
                    sb.AppendLine(string.Format("Срок бана за спам: <b>{0}</b> дн.", settings.Channels[chatId].AntiSpam.DaysToUnban));
                    sb.AppendLine(string.Format("Кол-во баллов для активации реакции на спам: <b>{0}</b>", settings.Channels[chatId].AntiSpam.SpamScoreValue));
                    sb.AppendLine(string.Format("Кол-во баллов добавляемых за пересылаемое сообщение: <b>{0}</b>",
                        settings.Channels[chatId].AntiSpam.SpamScoreForward));
                    sb.AppendLine(string.Format("Учитывать premium статус как негативный фактор (+1 балл): <b>{0}</b>",
                        settings.Channels[chatId].AntiSpam.PremiumAffix));
                    sb.AppendLine(string.Format("Отчет по блокировкам: <b>{0}</b>", settings.Channels[chatId].AntiSpam.ReportEnabled));
                    long targetId = (settings.Channels[chatId].AntiSpam.ReportChatId == 0) ? chatId : settings.Channels[chatId].AntiSpam.ReportChatId;
                    sb.AppendLine(string.Format("Канал для отчетов по блокировкам: <b>{0}</b>", GetChatName(targetId)));
                    sb.AppendLine(string.Format("Пересылка копии заблокированного сообщения: <b>{0}</b>", settings.Channels[chatId].AntiSpam.ReportCopy));
                    sb.AppendLine(string.Format("Учитывать форвард собственных сообщений: <b>{0}</b>", settings.Channels[chatId].AntiSpam.CountOwnForward));
                    sb.AppendLine(
                        string.Format("Учитывать форвард сообщений из этого чата: <b>{0}</b>", settings.Channels[chatId].AntiSpam.CountCurrentForward));
                    sb.AppendLine(string.Format("Автоочистка собственных сообщений через: <b>{0}</b> мин.", settings.Channels[chatId].AntiSpam.AutoCleanup));
                    sb.AppendLine(string.Format("Режим солидарности с другими чатами: <b>{0}</b>", settings.Channels[chatId].AntiSpam.Solidarity));
                    sb.AppendLine(string.Format("Тишина при режиме солидарности с другими чатами: <b>{0}</b>",
                        settings.Channels[chatId].AntiSpam.SilentSolidarity));
                    sb.AppendLine(string.Format("Время с момента знакомства, в течение которого запрещены репосты и ссылки: <b>{0}</b> мин.",
                        settings.Channels[chatId].AntiSpam.NewJoinSilentTime));
                    sb.AppendLine(string.Format("Ограничить новых пользователей в отправке медиа на время знакомства: <b>{0}</b>",
                        settings.Channels[chatId].AntiSpam.RestrictNewJoinMedia));
                    sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_antispam_restrict_blacklisted),
                        settings.Channels[chatId].AntiSpam.RestrictBlacklisted));
                    sb.AppendLine(string.Format("Обнаружение спама реакциями: <b>{0}</b>", settings.Channels[chatId].AntiSpam.Reactions));
                    sb.AppendLine(string.Format("Максимум разрешенных реакций за интервал проверки: <b>{0}</b>",
                        settings.Channels[chatId].AntiSpam.MaxReactions));
                    sb.AppendLine(string.Format("Интервал в течение которого аккумулируются реакции: <b>{0}</b> сек.",
                        settings.Channels[chatId].AntiSpam.ReactionsInterval));
                    sb.AppendLine(string.Format("Реагировать на сообщения \"друзей\": <b>{0}</b>", settings.Channels[chatId].AntiSpam.ReactOnFriends));
                    await bot.SendMessage(msg.Chat, sb.ToString(), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    return;
                case "test":
                    if ((msg.Chat.Type == ChatType.Group && !CheckIfAdminOfChat(msg.Chat.Id, uid)) || !IsOwner) return;
                    long targetChatId = msg.Chat.Id;
                    int? threadId = msg.MessageThreadId;
                    Message _msg = msg;
                    if (msg.ReplyToMessage is not null)
                    {
                        Log.Debug("testing ReplyToMessage");
                    }

                    if (msg.ReplyToMessage?.Text != null)
                    {
                        _msg = msg.ReplyToMessage;
                    }

                    if (_msg.Entities != null)
                        foreach (MessageEntity ent in _msg.Entities)
                        {
                            Log.Info("offset: {0} length: {1} type: {2}", ent.Offset, ent.Length, ent.Type);
                        }

                    HaveUrls(_msg);
                    await bot.SendMessage(targetChatId, antispam.TestScore(_msg, ulang), messageThreadId: threadId);
                    break;

                case "ver":
                case "version":
                    await bot.SendMessage(msg.Chat,
                        string.Format(Localization.GetText(ulang, LangEnum.s_antispam_version), antispam.Version), messageThreadId: msg.MessageThreadId);
                    return;
                case "stat":
                    if (words.Length == 2)
                    {
                        if (IsOwner)
                            await bot.SendMessage(msg.Chat, antispam.GetStat(ulang), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                        return;
                    }

                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    await bot.SendMessage(msg.Chat, channelScore.GetMetrics(chatId, ulang), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    break;
                case "clear":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.ClearDictionary(ulang));
                    break;
                case "reload":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.ReloadDictionary(ulang));
                    break;
                case "save":
                    if (!IsOwner) return;
                    antispam.Save();
                    settings.Save();
                    emojiVotes.Save();
                    await bot.SendMessage(msg.Chat, "Словари антиспама и настройки каналов сохранены");
                    break;
                case "banword":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.AddToBanwords(words.AsSpan().Slice(2), ulang));
                    break;
                case "unbanword":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.RemoveFromBanwords(words.AsSpan().Slice(2), ulang));
                    return;
                case "blacklist":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.AddToBlacklist(words.AsSpan().Slice(2), ulang));
                    break;
                case "unblacklist":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.RemoveFromBlacklist(words.AsSpan().Slice(2), ulang));
                    return;
                case "blc":
                    if (!IsOwner) return;
                    int cnt = 0;
                    if (words.Length == 3) int.TryParse(words[2], out cnt);
                    await bot.SendMessage(msg.Chat,
                        string.Format("Черный список пользователей по метрике рецидивов [{0}]: {1}", cnt, userScore.GetBanlist(cnt)));
                    return;
                case "add":
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.AddAsSpam(words.AsSpan().Slice(2), ulang));
                    return;
                case "remove":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.RemoveFromSpam(words.AsSpan().Slice(2), ulang));
                    return;
                }
                case "search":
                {
                    if (!IsOwner) return;
                    if (words.Length >= 2)
                    {
                        string text = antispam.Search(words.AsSpan().Slice(2), ulang);
                        Log.Debug(text);
                        await bot.SendMessage(msg.Chat, text);
                    }

                    return;
                }
                case "adjust":
                {
                    if (!IsOwner) return;
                    if (words.Length >= 2)
                        await bot.SendMessage(msg.Chat, antispam.AdjustScore(words.AsSpan().Slice(2), ulang));
                    return;
                }
                case "except":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antispam.AddToExceptions(words.AsSpan().Slice(2), ulang));
                    return;
                }
                case "unsuspect":
                {
                    if (!IsOwner) return;
                    for (int i = 2; i < words.Length; i++)
                    {
                        long _tid;
                        if (words[i].StartsWith('@')) _tid = userScore.GetUidByUsername(words[i].Replace("@", string.Empty));
                        else long.TryParse(words[i], out _tid);
                        if (_tid != 0)
                            antispam.RemoveSuspect(_tid);
                    }

                    return;
                }
                case "suspects":
                {
                    if (!IsOwner) return;
                    sb = new();
                    foreach (long i in antispam.GetSuspects)
                    {
                        UserMetrics umetrics = userScore.GetUserMetrics(i);
                        if (umetrics.UserId == i)
                        {
                            sb.AppendLine(string.Format("{0}: {1} {2} {3}", i, umetrics.UserName, umetrics.FirstName, umetrics.LastName));
                        }
                    }

                    if (sb.Length > 0) await bot.SendMessage(msg.Chat, sb.ToString());
                    return;
                }
            }

            break;
        }
        case "score":
        {
            if (words.Length < 2)
            {
                await bot.SendMessage(msg.Chat,
                    userScore.GetUserScore(uid, IsOwner, msg.Chat.Type == ChatType.Group && !CheckIfAdminOfChat(msg.Chat.Id, uid), ulang),
                    messageThreadId: msg.MessageThreadId);
                return;
            }

            switch (words[1])
            {
                case "save":
                    if (IsOwner) userScore.SaveMetrics();
                    break;
            }

            break;
        }
        case "version":
        {
            sb = new();
            sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_antibw_version), antibadwords.Version));
            sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_antispam_version), antispam.Version));
            await bot.SendMessage(msg.Chat, sb.ToString(), messageThreadId: msg.MessageThreadId);
            return;
        }
        case "bw":
        {
            if (words.Length < 2)
                return;
            switch (words[1])
            {
                case "ver":
                case "version":
                    await bot.SendMessage(msg.Chat, string.Format(Localization.GetText(ulang, LangEnum.s_antibw_version), antibadwords.Version),
                        messageThreadId: msg.MessageThreadId);
                    return;
                case "stat":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antibadwords.GetStat(ulang));
                    return;
                }
                case "forceremove":
                    forcecmd = true;
                    goto case "remove";
                case "remove":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antibadwords.RemoveWord(words.AsSpan().Slice(2), ulang, forcecmd));
                    return;
                }
                case "add":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antibadwords.AddWords(words.AsSpan().Slice(2), ulang));
                    return;
                }
                case "save":
                {
                    if (!IsOwner) return;
                    await bot.SendMessage(msg.Chat, antibadwords.Save(ulang));
                    return;
                }
                case "reload":
                {
                    if (!IsOwner) return;
                    antibadwords.LoadDictionary();
                    return;
                }
                case "help":
                {
                    await bot.SendMessage(msg.Chat, Localization.GetText(ulang, LangEnum.s_antibw_help), messageThreadId: msg.MessageThreadId,
                        parseMode: ParseMode.Html);
                    return;
                }
                case "on":
                case "enable":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    settings.Channels[chatId].AntiBadWords.Enabled = true;
                    await bot.SendMessage(msg.Chat, "Антимат на канале включен", messageThreadId: msg.MessageThreadId);
                    return;
                case "off":
                case "disable":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    settings.Channels[chatId].AntiBadWords.Enabled = false;
                    await bot.SendMessage(msg.Chat, "Антимат на канале выключен", messageThreadId: msg.MessageThreadId);

                    return;
                case "test":
                    if ((msg.Chat.Type == ChatType.Group && !CheckIfAdminOfChat(msg.Chat.Id, uid)) || !IsOwner) return;
                    int result = antibadwords.GetScore(msg.Text);
                    await bot.SendMessage(msg.Chat, string.Format("Антимат: совпадений по словарю: {0}", result), messageThreadId: msg.MessageThreadId);
                    break;
                case "status":
                case "config":
                    ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    sb = new();
                    sb.AppendLine(string.Format("Антимат: <b>{0}</b>", settings.Channels[chatId].AntiBadWords.Enabled));
                    sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_ban_time), settings.Channels[chatId].AntiBadWords.BanTime));
                    sb.AppendLine(
                        string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_warns_enabled), settings.Channels[chatId].AntiBadWords.WarnsEnabled));
                    sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_warn_times), settings.Channels[chatId].AntiBadWords.WarnTimes));
                    sb.AppendLine(string.Format(Localization.GetText(ulang, LangEnum.s_s_bw_cleanup), settings.Channels[chatId].AntiBadWords.AutoCleanup));
                    await bot.SendMessage(msg.Chat, sb.ToString(), messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    return;
                case "set":
                case "mod":
                {
                    int start = ParseChatId(IsPrivate, IsOwner, 2, ref chatId, words);
                    if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
                    StringBuilder _sb = new();
                    for (int i = start; i < words.Length; i++)
                    {
                        if (!words[i].Contains('=')) continue;
                        string[] keyvaluepair = words[i].Split('=');
                        bool showCmdStatus = keyvaluepair.Length == 1;
                        ChannelSettings channel = settings.Channels[chatId];
                        _sb.AppendLine(channel.AntiBadWords.SetSettings(keyvaluepair[0], showCmdStatus ? string.Empty : keyvaluepair[1], ulang));
                    }

                    string reply = sb.ToString();
                    if (!string.IsNullOrEmpty(reply))
                        await bot.SendMessage(msg.Chat.Id, reply, messageThreadId: msg.MessageThreadId, parseMode: ParseMode.Html);
                    return;
                }
            }

            break;
        }
        case "start":
        {
            Log.Debug("start:IsPrivate={0}, UID={1}", IsPrivate, uid);
            if (!IsPrivate) return;
            await bot.DeleteMessage(msg.Chat.Id, msg.MessageId);
            await settings.GetSession(uid).Start(bot, userScore);
            return;
        }
        case "voters":
        {
            if ((msg.Chat.Type == ChatType.Group && !CheckIfAdminOfChat(msg.Chat.Id, uid)) || !IsOwner) return;
            long targetChatId = msg.Chat.Id;
            int? threadId = msg.MessageThreadId;

            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            int msgId = 0;
            DateTime fromDate = DateTime.Today.AddDays(-7);
            int rndCount = 3;
            if (!IsPrivate)
            {
                if (msg.ReplyToMessage is null) return;
                if (msg.ReplyToMessage?.Text != null)
                {
                    msgId = msg.ReplyToMessage.MessageId;
                    fromDate = msg.ReplyToMessage.Date;
                }
            }
            else
            {
                if (words.Length > start)
                {
                    int.TryParse(words[start], out msgId);
                    start++;
                }
            }

            if (words.Length > start)
            {
                int.TryParse(words[start], out rndCount);
                start++;
            }

            string[] emojis = Array.Empty<string>();
            StringBuilder vsb = new();
            // GetVotes(long chatId, int messageId, DateTime begin, DateTime end, StringBuilder sb, string[] checkEmojis)

            if (words.Length > start)
            {
                emojis = words.AsSpan().Slice(start).ToArray();
            }

            List<long> voters = emojiVotes.GetVoters(chatId, msgId, fromDate, DateTime.Now, vsb, emojis);
            if (voters.Count > 0)
            {
                Log.Info(vsb.ToString());
                voters.Shuffle();
                StringBuilder votesResult = new();
                votesResult.AppendLine(string.Format("Выбираем {0} случайных пользователей", rndCount));
                for (int i = 0; i < rndCount; i++)
                {
                    if (i < voters.Count)
                    {
                        votesResult.AppendLine(string.Format("[{0}] {1}", i + 1, GetUserFromId(voters[i])));
                    }
                }

                await bot.SendMessage(targetChatId, votesResult.ToString(), messageThreadId: threadId, parseMode: ParseMode.Html);
            }
            else
            {
                Log.Info("sorry, no voters");
            }

            return;
        }
        case "emojis":
        {
            if ((msg.Chat.Type == ChatType.Group && !CheckIfAdminOfChat(msg.Chat.Id, uid)) || !IsOwner) return;
            long targetChatId = msg.Chat.Id;
            int? threadId = msg.MessageThreadId;

            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            int msgId = 0;
            DateTime fromDate = DateTime.Today.AddDays(-7);
            string[] emojis = Array.Empty<string>();
            if (!IsPrivate)
            {
                if (msg.ReplyToMessage is null) return;
                if (msg.ReplyToMessage?.Text != null)
                {
                    msgId = msg.ReplyToMessage.MessageId;
                    fromDate = msg.ReplyToMessage.Date;
                }
            }
            else
            {
                if (words.Length > start)
                {
                    int.TryParse(words[start], out msgId);
                    start++;
                }
            }

            if (words.Length > start) emojis = words.AsSpan().Slice(start).ToArray();
            StringBuilder vsb = new();
            // GetVotes(long chatId, int messageId, DateTime begin, DateTime end, StringBuilder sb, string[] checkEmojis)

            string votesResult = emojiVotes.GetVotes(chatId, msgId, fromDate, DateTime.Now, vsb, emojis);
            if (!string.IsNullOrEmpty(votesResult))
            {
                Log.Info(vsb.ToString());
                await bot.SendMessage(targetChatId, votesResult, messageThreadId: threadId);
            }

            return;
        }
        case "channels":
        {
            if (!IsOwner)
                return;
            sb = new();
            List<long> toRemove = new();
            foreach (KeyValuePair<long, ChannelSettings> channel in settings.Channels)
            {
                string mystatus;
                try
                {
                    ChatMember mydata = await bot.GetChatMember(channel.Key, runtimeSettings.OwnUID);
                    channel.Value.MyStatus = (int)mydata.Status;
                    mystatus = mydata.Status.ToString();
                }
                catch (Exception ex)
                {
                    mystatus = ex.Message;
                    toRemove.Add(channel.Key);
                    Log.Info(string.Format("{0} : {1} @{2} status: {3}", channel.Key, channel.Value.ChatTitle, channel.Value.UserName, mystatus));
                    continue;
                }

                try
                {
                    ChatFullInfo chatData = await bot.GetChat(channel.Key);
                    channel.Value.UserName = chatData.Username ?? string.Empty;
                    channel.Value.ChatTitle = chatData.Title ?? string.Empty;
                    channel.Value.ChatType = chatData.Type;
                }
                catch (Exception ex)
                {
                    Log.Info(ex.Message);
                }

                sb.AppendLine(string.Format("{0} : {1} @{2} status: {3} : {4}", channel.Key, channel.Value.ChatTitle, channel.Value.UserName, mystatus,
                    channel.Value.ChatType.ToString()));
            }

            if (toRemove.Count > 0)
                foreach (long i in toRemove)
                    settings.Channels.Remove(i, out _);

            await bot.SendMessage(msg.Chat, sb.ToString(), messageThreadId: msg.MessageThreadId);
            return;
        }
        case "chanremove":
        {
            if (!IsOwner) return;
            if (words.Length < 2) return;
            long.TryParse(words[1], out chatId);
            if (settings.Channels.ContainsKey(chatId)) settings.Channels.Remove(chatId, out _);
            return;
        }
        case "leavechat":
        {
            if (!IsOwner) return;
            if (words.Length < 2) return;
            long.TryParse(words[1], out chatId);
            await bot.LeaveChat(chatId);
            if (settings.Channels.ContainsKey(chatId)) settings.Channels.Remove(chatId, out _);
            return;
        }
        case "savesettings":
        {
            if (!IsOwner) return;
            settings.Save();
            return;
        }
        case "getadmins":
        {
            await GetChatAdmins(words, msg);
            return;
        }
        case "getchatid":
        {
            await bot.SendMessage(msg.Chat, msg.Chat.Id.ToString(), messageThreadId: msg.MessageThreadId);
            return;
        }
        case "whois":
        {
            if (!IsOwner) return;
            if (words.Length < 2) return;
            long.TryParse(words[1], out long targetUid);
            if (targetUid == 0) return;
            await bot.SendMessage(msg.Chat, GetUserFromId(targetUid), ParseMode.Html);
            return;
        }
        case "removelike":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
            if (words.Length < start) return;
            int.TryParse(words[start], out int targetUid);
            if (targetUid == 0) return;
            await RemoveMyReactions(chatId, targetUid);
            return;
        }
        case "like":
        {
            int start = ParseChatId(IsPrivate, IsOwner, 1, ref chatId, words);
            if (!IsOwner && (chatId == 0 || !CheckIfAdminOfChat(chatId, uid))) return;
            if (words.Length < start) return;
            int.TryParse(words[start], out int targetUid);
            if (targetUid == 0) return;
            await bot.SetMessageReaction(chatId, targetUid, reaction: new[] { heartReaction });
            return;
        }
        case "кусь":
        {
            await bot.SetMessageReaction(msg.Chat, msg.MessageId, reaction: new[] { heartReaction });
            Log.Info(string.Format("SetReaction heart on messageId {0} in chat {1}", msg.MessageId, msg.Chat));
            return;
        }
    }
}


async Task SetShutdown()
{
    Log.Info("Shutdown after 5 sec...");
    await Task.Delay(5000);
    waitHandle.Set();
}

// method to handle errors in polling or in your OnMessage/OnUpdate code
async Task OnError(Exception exception, HandleErrorSource source)
{
    Log.Error(exception); // just dump the exception to the console
    Log.Error(exception.StackTrace);
    await Task.CompletedTask;
}


// method that handle other types of updates received by the bot:
async Task OnChatMemberUpdate(ChatMemberUpdated cmu)
{
    userScore.UpdateMemberMetrics(cmu.NewChatMember.User, cmu.Chat.Id, 0);

    switch (cmu.OldChatMember.Status)
    {
        case ChatMemberStatus.Administrator when cmu.NewChatMember.Status != ChatMemberStatus.Administrator:
            Log.Info(string.Format("{0} not admin in chat {1} anymore. Delisted by {2}", cmu.OldChatMember.User, cmu.Chat, cmu.From));
            break;
        case ChatMemberStatus.Member when cmu.NewChatMember.Status == ChatMemberStatus.Left:
            Log.Info(string.Format("{0} has left the chat {1}", cmu.OldChatMember.User, cmu.Chat));
            break;
        case ChatMemberStatus.Left when cmu.NewChatMember.Status == ChatMemberStatus.Member:
            userScore.UpdateJoinDate(cmu.NewChatMember.User, cmu.Chat.Id);
            UserMetrics umetrics = userScore.GetUserMetrics(cmu.NewChatMember.User.Id);
            PerChannelMetrics cmetrics = userScore.GetUserChannelMetrics(cmu.NewChatMember.User.Id, cmu.Chat.Id);
            Log.Info(string.Format("Umetrics: UserId :{0}, JoinDate: {1}", umetrics.UserId, cmetrics.JoinDate));
            Log.Info(string.Format("{0} has joined the chat {1}", cmu.NewChatMember.User, cmu.Chat));

            Log.Debug(string.Format("id:{0} {1} isbot:{2} isprem:{3}",
                cmu.NewChatMember.User.Id, GetUserFrom(cmu.NewChatMember.User), cmu.NewChatMember.User.IsBot, cmu.NewChatMember.User.IsPremium));
            Log.Debug(string.Format("invite link: {0}; via join request: {1}; via chat folder link: {2}",
                cmu.InviteLink, cmu.ViaJoinRequest, cmu.ViaChatFolderInviteLink));
            if (!settings.Channels.ContainsKey(cmu.Chat.Id))
                return;
            ChannelSettings csettings = settings.Channels[cmu.Chat.Id];
            if (!csettings.AntiSpam.Enabled)
                return;

            string fullUserName = GetUserNameFrom(cmu.NewChatMember.User);
            bool isBannedName = antispam.HasBanwords(fullUserName);
            Log.Info(string.Format("Checking fullname: {0}, result: {1}", fullUserName, isBannedName));

            if (isBannedName)
            {
                userScore.AddToBanlist(cmu.NewChatMember.User.Id);
                string logstr = string.Format("FullUserName {0} contains banned word", fullUserName);
                Log.Info(logstr);
                await bot.BanChatMember(cmu.Chat.Id, cmu.NewChatMember.User.Id, DateTime.Now.AddDays(csettings.AntiSpam.DaysToUnban), true);
                //await ReportPost(csettings, logstr);
                return;
            }

            if (csettings.AntiSpam.RestrictBlacklisted && userScore.BanTimes(cmu.NewChatMember.User.Id) > 1)
            {
                await bot.BanChatMember(cmu.Chat.Id, cmu.NewChatMember.User.Id, DateTime.Now.AddDays(csettings.AntiSpam.DaysToUnban), true);
                Log.Info("Restricted blacklisted active, user banned!!!");
                await ReportPost(csettings, string.Format("Пользователь {0} в черном списке, забанен", GetUserFromForReply(cmu.NewChatMember.User)));
                return;
            }

            if (csettings.AntiSpam is { RestrictNewJoinMedia: true, NewJoinSilentTime: > 0 and < 3600 })
            {
                await bot.RestrictChatMember(cmu.Chat.Id, cmu.NewChatMember.User.Id, Settings.RestrictedPermissions, true,
                    DateTime.Now.AddMinutes(csettings.AntiSpam.NewJoinSilentTime));
                Log.Info(string.Format("User media restricted for {0} minutes", csettings.AntiSpam.NewJoinSilentTime));
            }

            break;
        case ChatMemberStatus.Left when cmu.NewChatMember.Status == ChatMemberStatus.Kicked:
            Log.Info(string.Format("{0} prebanned by {1} from chat {2}", cmu.OldChatMember.User, cmu.From, cmu.Chat));
            break;
        case ChatMemberStatus.Member when cmu.NewChatMember.Status == ChatMemberStatus.Kicked:
            Log.Info(string.Format("{0} kicked by {1} from chat {2}", cmu.OldChatMember.User, cmu.From, cmu.Chat));
            userScore.GetUserMetrics(cmu.NewChatMember.User.Id).Score = 0;
            break;
        case ChatMemberStatus.Kicked when cmu.NewChatMember.Status == ChatMemberStatus.Kicked:
            Log.Info(string.Format("{0} rebanned by {1} in chat {2}", cmu.OldChatMember.User, cmu.From, cmu.Chat));
            break;
        case ChatMemberStatus.Kicked when cmu.NewChatMember.Status == ChatMemberStatus.Left:
            Log.Info(string.Format("{0} unbanned by {1} in chat {2}", cmu.OldChatMember.User, cmu.From, cmu.Chat));
            break;
        default:
            Log.Debug(string.Format("id:{0} {1} isbot:{2} isprem:{3}",
                cmu.NewChatMember.User.Id, GetUserFrom(cmu.NewChatMember.User), cmu.NewChatMember.User.IsBot, cmu.NewChatMember.User.IsPremium));
            Log.Info(string.Format("ChatMember update  by {0} {1}", GetUserFrom(cmu.From), cmu.Date));
            Log.Debug(string.Format("invite link: {0}; via join request: {1}; via chat folder link: {2}",
                cmu.InviteLink, cmu.ViaJoinRequest, cmu.ViaChatFolderInviteLink));
            Log.Info(string.Format("ChatId: {0} Type: {1} Title: {2} Username: {3} {4} {5} IsForum: {6}",
                cmu.Chat.Id, cmu.Chat.Type, cmu.Chat.Title, cmu.Chat.Username, cmu.Chat.FirstName, cmu.Chat.LastName, cmu.Chat.IsForum));
            Log.Info(string.Format("OldChatMember: {0}:{1} => NewChatMember: {2}:{3}",
                cmu.OldChatMember.User, cmu.OldChatMember.Status, cmu.NewChatMember.User, cmu.NewChatMember.Status));
            break;
    }
}


async Task MyChatMemberUpdate(ChatMemberUpdated cmu)
{
    if (cmu.Chat.Type == ChatType.Private) return;
    Log.Info(string.Format("MyChatMember update  by {0} status changed from {1} to {2}", cmu.From, cmu.OldChatMember.Status, cmu.NewChatMember.Status));
    Log.Debug(string.Format("invite link: {0}", cmu.InviteLink));
    Log.Debug(string.Format("via join request: {0}; via chat folder invite link: {1}", cmu.ViaJoinRequest, cmu.ViaChatFolderInviteLink));
    Log.Debug(string.Format("ChatId: {0} Type: {1} Title: {2} Username: {3} {4} {5} IsForum: {6}",
        cmu.Chat.Id, cmu.Chat.Type, cmu.Chat.Title, cmu.Chat.Username, cmu.Chat.FirstName, cmu.Chat.LastName, cmu.Chat.IsForum));
    string cmuchatId = cmu.Chat.Id.ToString();

    bool weAreAdmin = cmu.NewChatMember.Status == ChatMemberStatus.Administrator;
    bool processChatSetup =
        (cmu.OldChatMember.Status == ChatMemberStatus.Left
         && cmu.NewChatMember.Status != ChatMemberStatus.Kicked)
        || weAreAdmin;

    if (processChatSetup) await ProcessChatAdmins(cmu.Chat.Id);
    if (cmu.OldChatMember.Status == ChatMemberStatus.Administrator && !weAreAdmin)
    {
        // we are not admin anymore
        if (settings.Channels.ContainsKey(cmu.Chat.Id))
            settings.Channels[cmu.Chat.Id].AntiSpam.Enabled = false;
    }

    if (settings.Channels.TryGetValue(cmu.Chat.Id, out ChannelSettings channel))
        channel.MyStatus = (int)cmu.NewChatMember.Status;

    string resultChat = (channel != null)
        ? string.Format("{0} : {1} @{2} status: {3}", channel.ChatId, channel.ChatTitle, channel.UserName, channel.MyStatus)
        : cmuchatId.StartsWith("-100")
            ? string.Format("<a href=\"https://t.me/c/{0}\">{1}</a>", cmuchatId.Remove(0, 4), cmu.Chat)
            : string.Format("{0}", cmu.Chat);

    string reply = cmu.NewChatMember.Status switch
    {
        ChatMemberStatus.Member when cmu.OldChatMember.Status == ChatMemberStatus.Left => string.Format("Меня пригласили в чат {0}", resultChat),
        ChatMemberStatus.Administrator when cmu.OldChatMember.Status == ChatMemberStatus.Kicked => string.Format("Из грязи в князи на канале {0}", resultChat),
        ChatMemberStatus.Administrator when cmu.OldChatMember.Status == ChatMemberStatus.Member => string.Format("В чате {0} мне дали права администратора",
            resultChat),
        ChatMemberStatus.Left when cmu.OldChatMember.Status != ChatMemberStatus.Left => string.Format("Покидаем чат {0}", resultChat),
        ChatMemberStatus.Kicked when cmu.OldChatMember.Status != ChatMemberStatus.Kicked => string.Format("Меня выгнали из чата {0}", resultChat),
        _ => string.Empty
    };
    if (cmu.NewChatMember.Status == ChatMemberStatus.Left && cmu.OldChatMember.Status != ChatMemberStatus.Left)
    {
        // мы покинули чат, возможно что канал удален, сносим все настройки
        settings.Channels.Remove(cmu.Chat.Id);
        await settings.CloseSessionByChatId(bot, cmu.Chat.Id);
    }

    if (!string.IsNullOrEmpty(reply))
    {
        Log.Info(reply);
        await bot.SendMessage(runtimeSettings.OwnerUID, reply, parseMode: ParseMode.Html);
    }
}

async Task OnMessageReactionUpdate(MessageReactionUpdated mru)
{
    if (mru.Chat.Type == ChatType.Channel) return;
    Log.Info(string.Format("MessageReaction update chat:{0} message:{1} user:{2} actorchat:{3}", mru.Chat, mru.MessageId, mru.User, mru.ActorChat));
    if (mru.User is null) return;
    userScore.UpdateMemberMetrics(mru.User, mru.Chat.Id, 0);
    if (!settings.Channels.ContainsKey(mru.Chat.Id)) return;
    emojiVotes.RegisterMRU(mru);
    ChannelSettings csettings = settings.Channels[mru.Chat.Id];
    if (!csettings.AntiSpam.Reactions || csettings.AntiSpam.MaxReactions <= 0 || csettings.AntiSpam.ReactionsInterval <= 3 ||
        csettings.ChatAdmins.Contains(mru.User.Id) || mru.User.Id == runtimeSettings.OwnerUID)
        return;
    if (antispam.CheckReactionFlood(mru.User.Id, csettings.AntiSpam.MaxReactions, csettings.AntiSpam.ReactionsInterval * 1000))
    {
        channelScore.AddReactionMetric(mru.Chat.Id, true, csettings.AntiSpam.SpamScoreValue);
        if (!csettings.AntiSpam.Enabled)
        {
            bool isAdminInOtherChat = false;
            foreach (ChannelSettings cs in settings.Channels.Values)
            {
                if (!cs.ChatAdmins.Contains(mru.User.Id))
                    continue;
                isAdminInOtherChat = true;
                break;
            }

            if (!isAdminInOtherChat)
                antispam.AddSuspect(mru.User.Id);
        }
        else
            await PunishForReactions(csettings, mru.User, false);
    }
    else
    {
        channelScore.AddReactionMetric(mru.Chat.Id, false, 0);
    }
}


async Task OnCallBackQuery(CallbackQuery cbq)
{
    if (cbq.Message?.Chat != null)
        userScore.UpdateMemberMetrics(cbq.From, cbq.Message.Chat.Id, 0);
    SettingsSession session = settings.GetSession(cbq.From.Id);
    if (cbq.Message != null && cbq.Message.MessageId == session.messageId)
    {
        string data = cbq.Data ?? string.Empty;
        Log.Debug("Session {0}, data: {1}", cbq.From.Id, data);
        await session.ProcesCallback(bot, userScore, data);
    }
}

async Task OnUpdate(Update update)
{
    await m_task_factory.StartNew(async () => { await TaskOnUpdate(update); });
}

async Task TaskOnUpdate(Update update)
{
    switch (update)
    {
        case { MessageReactionCount: { } }:
            break;
        case { MessageReaction: { } mru }:
            await OnMessageReactionUpdate(mru);
            break;
        case { MyChatMember: { } cmu }:
            await MyChatMemberUpdate(cmu);
            break;
        case { ChatMember: { } cmu2 }:
            await OnChatMemberUpdate(cmu2);
            break;
        case { CallbackQuery: { } query }:
            await OnCallBackQuery(query);
            break;
        default:
            Log.Info(string.Format("-- OnUpdate: {0} --", update.Type));
            break;
    }
}

async Task RemoveMyReactions(long chatId, int messageId)
{
    await bot.SetMessageReaction(chatId, messageId, reaction: null);
    Log.Info(string.Format("Removed my reactions on messageId {0} in chat {1}", messageId, chatId));
}

async Task PostToChat(long chatId, int? msgThreadId, string msg)
{
    LinkPreviewOptions linkPreviewOptions = new() { IsDisabled = true };
    Message _msg = null;
    try
    {
        _msg = await bot.SendMessage(chatId: chatId, text: msg, messageThreadId: msgThreadId, parseMode: ParseMode.Html,
            linkPreviewOptions: linkPreviewOptions);
    }
    catch (Exception ex)
    {
        Log.Error(ex.Message);
        Log.Error(ex.StackTrace);
    }

    if (_msg != null && settings.Channels.ContainsKey(chatId) && settings.Channels[chatId].AntiSpam.AutoCleanup > 0)
        await m_task_factory.StartNew(async () => { await CleanupMessage(_msg, settings.Channels[chatId].AntiSpam.AutoCleanup); });
}
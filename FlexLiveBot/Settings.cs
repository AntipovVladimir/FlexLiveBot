using System.Text.Json;
using NLog;
using Telegram.Bot;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace FlexLiveBot;

public class Settings
{
    public static readonly ChatPermissions RestrictedPermissions = new()
    {
        CanSendPhotos = false,
        CanSendAudios = false,
        CanSendPolls = false,
        CanSendDocuments = false,
        CanSendVideos = false,
        CanSendVoiceNotes = false,
        CanSendVideoNotes = false,
        CanSendOtherMessages = false,
        CanSendMessages = true,
        CanInviteUsers = true
    };


    public Dictionary<long, ChannelSettings> Channels { get; set; } = new();
    private readonly Dictionary<long, SettingsSession> settingsSessions = new();

    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string settingsFile = "lastochka.json";

    public void StartSession(long uid)
    {
        if (settingsSessions.ContainsKey(uid))
            settingsSessions.Remove(uid);
        settingsSessions.Add(uid, new(uid, this));
    }

    public SettingsSession GetSession(long uid)
    {
        if (!settingsSessions.ContainsKey(uid))
            settingsSessions.Add(uid, new(uid, this));
        return settingsSessions[uid];
    }


    public async Task CloseSessionByChatId(TelegramBotClient bot, long chatId)
    {
        SettingsSession session; //= null;
        do
        {
            session = GetSessionByChatId(chatId);
            if (session is not null)
            {
                await session.Close(bot);
                settingsSessions.Remove(session.userId);
            }
        } while (session is not null);
    }

    public SettingsSession GetSessionByChatId(long chatId)
    {
        foreach (KeyValuePair<long, SettingsSession> kvp in settingsSessions)
        {
            if (kvp.Value.targetId == chatId)
                return kvp.Value;
        }

        return null;
    }

    public void Save()
    {
        try
        {
            string data = JsonSerializer.Serialize(this);
            File.WriteAllText(settingsFile, data);
            Console.WriteLine("Channels settings saved");
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            Log.Error(ex.StackTrace);
        }
    }

    public static Settings Load()
    {
        return JsonHelpers.Load<Settings>(settingsFile);
    }

    public void SetupChannelSettings(long chatId)
    {
        if (Channels.ContainsKey(chatId))
            return;
        Channels.Add(chatId, new()
        {
            ChatId = chatId,
            AntiSpam = new()
            {
                Enabled = false, DaysToUnban = 30, SpamScoreValue = 5, SpamScoreForward = 2, PremiumAffix = true, ReportEnabled = true,
                Solidarity = true, SilentSolidarity = true, AutoCleanup = 3, NewJoinSilentTime = 30, RestrictNewJoinMedia = true, RestrictBlacklisted = true
            }
        });
    }
}
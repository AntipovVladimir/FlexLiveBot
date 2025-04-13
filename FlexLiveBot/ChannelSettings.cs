using Telegram.Bot.Types.Enums;

namespace FlexLiveBot;

public class ChannelSettings
{
    public long ChatId { get; set; }
    public string ChatTitle { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<long> ChatAdmins { get; set; } = new();
    public AntispamSettings AntiSpam { get; set; } = new();
    public AntiBadWordsSettings AntiBadWords { get; set; } = new();
    public int MyStatus { get; set; }
    public List<long> ChatFriends { get; set; } = new();
    public ChatType ChatType { get; set; }
}

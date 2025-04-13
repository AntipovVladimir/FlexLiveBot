using System.Text.Json.Serialization;

namespace FlexLiveBot;

public class UserMetrics
{
    public long UserId { get; set; }
    public int Score { get; set; }
    public int Posts { get; set; }
    public bool isBot { get; set; }
    public bool isPremium { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public long ChatLastSeen { get; set; }
    public Dictionary<long, PerChannelMetrics> ChannelMetrics { get; set; } = new();
    public string Locale { get; set; } = string.Empty;
    [JsonIgnore]
    public FixedQueue<MessageItem> LastMessages = new(10);
}

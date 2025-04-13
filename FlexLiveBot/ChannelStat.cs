namespace FlexLiveBot;
public class ChannelStat
{
    public int totalMessages { get; set; }
    public int totalReactions { get; set; }
    public int totalReports { get; set; }
    public int totalSpams { get; set; }
    public Dictionary<DateTime, List<SpamRecord>> spamRecords { get; set; }=  new();
}

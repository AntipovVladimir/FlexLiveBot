namespace FlexLiveBot;
public class mruData
{
    public long Uid { get; set; }
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public DateTime TimeStamp { get; set; }
    public string[] OldReactions { get; set; } = Array.Empty<string>();
    public string[] NewReactions { get; set; } = Array.Empty<string>();
}
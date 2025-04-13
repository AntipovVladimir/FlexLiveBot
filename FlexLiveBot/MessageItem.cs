namespace FlexLiveBot;
public class MessageItem
{
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public DateTime Date { get; set; }
    public string Text { get; set; } = string.Empty;
}

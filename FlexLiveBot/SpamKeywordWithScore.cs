namespace FlexLiveBot;
public class SpamKeywordWithScore
{
    public string Keyword { get; set; } = string.Empty;
    public float Score { get; set; } = 1.0f;
    public int Hits { get; set; }
}
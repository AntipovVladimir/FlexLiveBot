namespace FlexLiveBot;
public class Locale
{
    public string LocaleName { get; set; } = string.Empty; // ru
    public string MenuName { get; set; } = string.Empty;
    public string MenuCommand { get; set; } = string.Empty;
    public Dictionary<int, string> Text { get; set; } = new();
}
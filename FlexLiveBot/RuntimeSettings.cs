using System.Text.Json.Serialization;
using NLog;
#pragma warning disable CS8618

namespace FlexLiveBot;

internal record RuntimeSettings
{
    [JsonIgnore]
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    [JsonIgnore]
    private const string settingsFile = "runtime.json"; 
        
    public long OwnerUID { get; init; }
    public long OwnUID { get; init; }
    public long ReportUID { get; init; }
    public string ReportLink { get;init; }
    public string OwnerUserName { get; init; }
    public string BotToken { get; init; }

    public static RuntimeSettings Load()
    {
        RuntimeSettings obj = JsonHelpers.Load<RuntimeSettings>(settingsFile);
        if (obj is not null)
            return obj;
        throw new("Runtime settings can't be loaded! Please, fix runtime.json");
    }
}
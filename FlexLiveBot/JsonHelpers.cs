using System.Text.Json;
using NLog;

namespace FlexLiveBot;

public static class JsonHelpers
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    public static T Load<T>(string filename)
    {
        try
        {
            string cfg = File.ReadAllText(filename);
            if (!string.IsNullOrWhiteSpace(cfg))
            {
                T obj = JsonSerializer.Deserialize<T>(cfg);
                if (obj is not null)
                    return obj;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            Log.Error(ex.StackTrace);
        }
        return default;
    }
}
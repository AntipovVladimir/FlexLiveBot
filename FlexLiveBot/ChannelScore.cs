using System.Text;
using System.Text.Json;
using NLog;
// ReSharper disable UseStringInterpolation

namespace FlexLiveBot;

public class ChannelScore
{
    private const string file_stats = "lastochka.channelstat.json";
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    public ChannelScore()
    {
        Load();
    }
    public void Load()
    {
        Dictionary<long, ChannelStat> obj = JsonHelpers.Load<Dictionary<long, ChannelStat>>(file_stats);
        long records = 0;
        if (obj is not null)
        {
            ChannelStats.Clear();
            foreach (KeyValuePair<long, ChannelStat> i in obj)
            {
                ChannelStats.Add(i.Key, i.Value);
                records += i.Value.spamRecords.Count;
            }
        }
        Log.Info(string.Format("Channel stat loaded: {0} channels, records {1}", ChannelStats.Count, records));
    }
    public void Save()
    {
        try
        {
            string data = JsonSerializer.Serialize(ChannelStats);
            File.WriteAllText(file_stats, data);
            Log.Info("Channels stats saved");
        }
        catch (Exception e)
        {
            Log.Info(e.Message);
        }
    }
    
    private readonly Dictionary<long, ChannelStat> ChannelStats = new();

    public void AddMessageMetric(long chatId, int score, int maxscore)
    {
        if (!ChannelStats.ContainsKey(chatId))
            ChannelStats.Add(chatId, new());
        ChannelStats[chatId].totalMessages++;
        if (score >= maxscore)
        {
            ChannelStats[chatId].totalSpams++;
            DateTime dt = DateTime.Now.Date;
            if (!ChannelStats[chatId].spamRecords.ContainsKey(dt))
                ChannelStats[chatId].spamRecords.Add(dt, new());
            ChannelStats[chatId].spamRecords[dt].Add(new() { dt = DateTime.Now, score = score });
        }
    }

    public void AddReactionMetric(long chatId, bool isSpam, int score)
    {
        if (!ChannelStats.ContainsKey(chatId))
            ChannelStats.Add(chatId, new());
        ChannelStats[chatId].totalReactions++;
        if (isSpam)
        {
            ChannelStats[chatId].totalSpams++;
           DateTime dt = DateTime.Now.Date;
            if (!ChannelStats[chatId].spamRecords.ContainsKey(dt))
                ChannelStats[chatId].spamRecords.Add(dt, new());
            ChannelStats[chatId].spamRecords[dt].Add(new() { dt = DateTime.Now, score = score });
        }
    }

    public void AddReportMetric(long chatId)
    {
        if (!ChannelStats.ContainsKey(chatId))
            ChannelStats.Add(chatId, new());
        ChannelStats[chatId].totalReports++;
    }

    public string GetMetrics(long chatId, string locale)
    {
        if (!ChannelStats.ContainsKey(chatId))
            ChannelStats.Add(chatId, new());
        StringBuilder sb = new();
        sb.AppendLine(Localization.GetText(locale, LangEnum.s_cscore_channel_stat));
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_cscore_total_messages),ChannelStats[chatId].totalMessages));
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_cscore_total_reactions), ChannelStats[chatId].totalReactions));
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_cscore_total_spams),ChannelStats[chatId].totalSpams));
        int i = 0;
        int ts = 0;
        foreach (KeyValuePair<DateTime, List<SpamRecord>> rec in ChannelStats[chatId].spamRecords)
        {
            if (rec.Key != DateTime.Now.Date)
                continue;
            i = rec.Value.Count;
            foreach (SpamRecord sr in rec.Value)
            {
                ts += sr.score;
            }
        }
        float average = i > 0 ? (float)Math.Round((float)ts / i,1) : 0;
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_cscore_summary), i, average));
        return sb.ToString();
    }
}
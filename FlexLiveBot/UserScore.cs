using System.Text.Json;
using NLog;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace FlexLiveBot;

public class UserScore
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private readonly int _goodScore;

    public UserScore(int goodScore = 20)
    {
        _goodScore = goodScore;
        LoadBanlist();
        LoadMetrics();
    }

    private const string file_metrics = "lastochka.metrics.json";
    private const string file_banlist = "lastochka.banlist.json";
    private readonly Dictionary<long, int> Banlist = new();


    public int GetBanlist(int times)
    {
        if (times == 0)
            return Banlist.Count;
        int i = 0;
        foreach (int val in Banlist.Values)
            if (val >= times)
                i++;
        return i;
    }

    public void LoadBanlist()
    {
        Banlist.Clear();
        Dictionary<long, int> dic = JsonHelpers.Load<Dictionary<long, int>>(file_banlist);
        if (dic != null)
            foreach (KeyValuePair<long, int> item in dic)
                Banlist.Add(item.Key, item.Value);
        Log.Info(string.Format(Localization.GetText("ru", LangEnum.s_uscore_banlist_loaded), Banlist.Count));
    }

    public void SaveBanlist()
    {
        try
        {
            string data = JsonSerializer.Serialize(Banlist);
            File.WriteAllText(file_banlist, data);
            Log.Info(Localization.GetText("ru", LangEnum.s_uscore_banlist_saved));
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            Log.Error(ex.StackTrace);
        }
    }

    public void AddToBanlist(long uid)
    {
        if (!Banlist.ContainsKey(uid))
            Banlist.Add(uid, 0);
        Banlist[uid]++;
    }

    public int BanTimes(long uid)
    {
        if (!Banlist.ContainsKey(uid))
            return 0;
        return Banlist[uid];
    }

    public void Unban(long uid)
    {
        Banlist.Remove(uid);
        if (userMetrics.ContainsKey(uid))
            userMetrics[uid].Score = 20;
    }


    public int GetScore(long uid)
    {
        if (userMetrics.ContainsKey(uid))
            return userMetrics[uid].Score;
        return 0;
    }

    public string GetUserScore(long uid, bool isOwner, bool isAdmin, string locale)
    {
        if (isOwner)
            return Localization.GetText(locale, LangEnum.s_uscore_owner_trust);
        if (isAdmin)
            return Localization.GetText(locale, LangEnum.s_uscore_admin_trust);
        return string.Format(Localization.GetText(locale, LangEnum.s_uscore_trust_level), GetScore(uid));
    }

    public bool AdjustScore(long uid, ref int score, int maxscore)
    {
        if (!userMetrics.ContainsKey(uid))
            return false;
        int oldscore = userMetrics[uid].Score;
        if (score * 2 <= maxscore)
        {
            // good
            if (oldscore < _goodScore)
                userMetrics[uid].Score++;
            else
                return true;
        }
        else
        {
            if (oldscore >= _goodScore)
            {
                userMetrics[uid].Score--;
                score--;
            }
        }

        return false;
    }

    private Dictionary<long, UserMetrics> userMetrics = new();

    public void SaveMetrics()
    {
        try
        {
            string data = JsonSerializer.Serialize(userMetrics, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(file_metrics, data);
            Console.WriteLine(Localization.GetText("ru", LangEnum.s_uscore_metrics_saved));
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            Log.Error(ex.StackTrace);
        }
    }

    public void LoadMetrics()
    {
        Dictionary<long, UserMetrics> obj = JsonHelpers.Load<Dictionary<long, UserMetrics>>(file_metrics);
        if (obj is null || obj.Count <= 0) return;
        userMetrics.Clear();
        userMetrics = obj;
    }

    public void UpdateJoinDate(User user, long chatId)
    {
        if (!userMetrics.ContainsKey(user.Id))
        {
            userMetrics.Add(user.Id, new()
            {
                UserId = user.Id,
                isPremium = user.IsPremium,
                isBot = user.IsBot,
                UserName = user.Username ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName ?? string.Empty,
                ChatLastSeen = chatId,
                Score = 0,
                Posts = 0,
                ChannelMetrics = new() { { chatId, new() { ChatId = chatId } } }
            });
        }

        if (!userMetrics[user.Id].ChannelMetrics.ContainsKey(chatId))
        {
            userMetrics[user.Id].ChannelMetrics.Add(chatId, new() { ChatId = chatId });
        }

        userMetrics[user.Id].ChannelMetrics[chatId].LastSeen = DateTime.Now;
        userMetrics[user.Id].ChannelMetrics[chatId].JoinDate = DateTime.Now;
    }

    public PerChannelMetrics GetUserChannelMetrics(long uid, long chatId)
    {
        if (!userMetrics.ContainsKey(uid))
            return NullUser.ChannelMetrics[0];
        if (userMetrics[uid].ChannelMetrics.ContainsKey(chatId))
            return userMetrics[uid].ChannelMetrics[chatId];
        userMetrics[uid].ChannelMetrics.Add(chatId, new() { ChatId = chatId, JoinDate = null, LastSeen = null });
        return userMetrics[uid].ChannelMetrics[chatId];
    }

    public bool HaveKnownUsername(string text, long chatId, string ownerUsername)
    {
        if (text.StartsWith("/start@")) return false;
        string[] test = text.Split(' ');
        long targetUid = 0;
        bool moredogs = false;
        foreach (string str in test)
        {
            if (!str.StartsWith('@') || str.Equals(ownerUsername))
                continue;
            moredogs = true;
            targetUid = GetUidByUsername(str);
        }

        if (targetUid != 0)
        {
            UserMetrics tuser = GetUserMetrics(targetUid);
            if (!tuser.ChannelMetrics.ContainsKey(chatId) && tuser.Score < 2)
                return true;
        }
        else
            return moredogs;

        return false;
    }


    public UserMetrics GetUserMetrics(long uid)
    {
        return userMetrics.ContainsKey(uid) ? userMetrics[uid] : NullUser;
    }

    public long GetUidByUsername(string username)
    {
        foreach (KeyValuePair<long, UserMetrics> um in userMetrics)
        {
            if (um.Value.UserName.Equals(username))
                return um.Key;
        }

        return 0;
    }

    public void UpdateMemberMetrics(User user, long chatId, int posts)
    {
        if (!userMetrics.ContainsKey(user.Id))
            userMetrics.Add(user.Id, new()
            {
                UserId = user.Id,
                isPremium = user.IsPremium,
                isBot = user.IsBot,
                UserName = user.Username ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName ?? string.Empty,
                ChatLastSeen = chatId,
                Score = 0,
                Posts = posts,
                ChannelMetrics = new() { { chatId, new() { ChatId = chatId, LastSeen = DateTime.Now } } }
            });
        userMetrics[user.Id].isPremium = user.IsPremium;
        userMetrics[user.Id].isBot = user.IsBot;
        userMetrics[user.Id].UserName = user.Username ?? string.Empty;
        userMetrics[user.Id].FirstName = user.FirstName;
        userMetrics[user.Id].LastName = user.LastName ?? string.Empty;
        userMetrics[user.Id].ChatLastSeen = chatId;
        userMetrics[user.Id].Posts += posts;
        if (!userMetrics[user.Id].ChannelMetrics.ContainsKey(chatId))
        {
            userMetrics[user.Id].ChannelMetrics.Add(chatId, new() { ChatId = chatId });
        }

        userMetrics[user.Id].ChannelMetrics[chatId].LastSeen = DateTime.Now;
    }

    private static readonly UserMetrics NullUser = new()
    {
        UserId = 0,
        ChannelMetrics = new() { { 0, new() { ChatId = 0, JoinDate = null, LastSeen = null } } }
    };
}
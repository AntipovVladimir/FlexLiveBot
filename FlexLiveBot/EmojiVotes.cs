using System.Text;
using System.Text.Json;
using NLog;
using Telegram.Bot.Types;

// ReSharper disable UseStringInterpolation

namespace FlexLiveBot;

public class EmojiVotes
{
    private const string file_messages = "lastochka.votemessages.json";
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private List<mruData> mruDatas = new();

    public EmojiVotes()
    {
        Load();
        CleanUp(DateTime.Today.AddDays(-7));
    }


    public void Load()
    {
        List<mruData> idata = JsonHelpers.Load<List<mruData>>(file_messages);
        if (idata == null) return;
        mruDatas = idata;
        Log.Info("RVM loaded");
    }

    public void Save()
    {
        CleanUp(DateTime.Today.AddDays(-7));
        try
        {
            string data = JsonSerializer.Serialize(mruDatas, new JsonSerializerOptions() { WriteIndented = true });
            System.IO.File.WriteAllText(file_messages, data);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public void CleanUp(DateTime oldDate)
    {
        Console.WriteLine("MRU cleanup before date {0}", oldDate);
        int oldCount = mruDatas.Count;
        List<mruData> newList = new();
        foreach (mruData item in mruDatas)
        {
            if (item.TimeStamp >= oldDate)
                newList.Add(item);
        }

        mruDatas = newList;
        Console.WriteLine("MRU removed {0}", oldCount - mruDatas.Count);
    }

    public bool RegisterMRU(MessageReactionUpdated mru)
    {
        if (mru.User is null)
            return false;
        mruData item = new()
        {
            Uid = mru.User.Id,
            ChatId = mru.Chat.Id,
            MessageId = mru.MessageId,
            TimeStamp = mru.Date
        };
        List<string> oldEmojis = new();
        List<string> newEmojis = new();
        foreach (ReactionType t in mru.OldReaction)
        {
            if (t is ReactionTypeEmoji emoji)
                oldEmojis.Add(emoji.Emoji);
            if (t is ReactionTypeCustomEmoji custom)
                oldEmojis.Add(custom.CustomEmojiId);
        }

        foreach (ReactionType t in mru.NewReaction)
        {
            if (t is ReactionTypeEmoji emoji)
                newEmojis.Add(emoji.Emoji);
            if (t is ReactionTypeCustomEmoji custom)
                newEmojis.Add(custom.CustomEmojiId);
        }

        item.OldReactions = oldEmojis.ToArray();
        item.NewReactions = newEmojis.ToArray();
        mruDatas.Add(item);
        return true;
    }

    public string GetVotes(long chatId, int messageId, DateTime begin, DateTime end, StringBuilder sb, string[] checkEmojis)
    {
        int totalVotes = 0;
        int totalEmojis = 0;
        List<mruData> rightItems = new();
        foreach (mruData item in mruDatas)
        {
            if (item.ChatId != chatId || item.MessageId != messageId)
                continue;
            if (item.TimeStamp < begin || item.TimeStamp > end)
                continue;
            totalEmojis++;
            rightItems.Add(item);
        }

        rightItems.Sort((p, q) => p.TimeStamp.CompareTo(q.TimeStamp));
        Dictionary<string, List<long>> emojis = new();
        foreach (mruData item in rightItems)
        {
            if (item.OldReactions.Length > 0)
            {
                foreach (string em in item.OldReactions)
                {
                    if (!emojis.ContainsKey(em))
                        continue;
                    if (emojis[em].Contains(item.Uid))
                        emojis[em].Remove(item.Uid);
                }
            }

            if (item.NewReactions.Length > 0)
            {
                foreach (string em in item.NewReactions)
                {
                    if (!emojis.ContainsKey(em))
                        emojis.Add(em, new());
                    emojis[em].Add(item.Uid);
                }
            }
        }


        foreach (KeyValuePair<string, List<long>> kvp in emojis)
        {
            sb.AppendLine($"Emoji: {kvp.Key}");
            sb.AppendLine(string.Join(',', kvp.Value.ToArray()));
            sb.AppendLine("---");
            if (checkEmojis.Length == 0)
            {
                totalVotes += kvp.Value.Count;
                continue;
            }

            foreach (string t in checkEmojis)
            {
                if (!kvp.Key.Equals(t))
                    continue;
                totalVotes += kvp.Value.Count;
            }
        }

        sb.AppendLine($"total votes: {totalVotes}/{totalEmojis}");
        return string.Format("Реакций на сообщение: всего {0}, учтено {1}", totalEmojis, totalVotes);
    }


    public List<long> GetVoters(long chatId, int messageId, DateTime begin, DateTime end, StringBuilder sb, string[] checkEmojis)
    {
        List<mruData> rightItems = new();
        foreach (mruData item in mruDatas)
        {
            if (item.ChatId != chatId || item.MessageId != messageId)
                continue;
            if (item.TimeStamp < begin || item.TimeStamp > end)
                continue;
            rightItems.Add(item);
        }

        rightItems.Sort((p, q) => p.TimeStamp.CompareTo(q.TimeStamp));
        Dictionary<string, List<long>> emojis = new();
        foreach (mruData item in rightItems)
        {
            if (item.OldReactions.Length > 0)
            {
                foreach (string em in item.OldReactions)
                {
                    if (!emojis.ContainsKey(em))
                        continue;
                    if (emojis[em].Contains(item.Uid))
                        emojis[em].Remove(item.Uid);
                }
            }

            if (item.NewReactions.Length > 0)
            {
                foreach (string em in item.NewReactions)
                {
                    if (!emojis.ContainsKey(em))
                        emojis.Add(em, new());
                    emojis[em].Add(item.Uid);
                }
            }
        }

        List<long> result = new();
        foreach (KeyValuePair<string, List<long>> kvp in emojis)
        {
            sb.AppendLine(string.Format("Emoji: {0}", kvp.Key));
            sb.AppendLine(string.Join(',', kvp.Value.ToArray()));
            sb.AppendLine("---");
            if (checkEmojis.Length == 0)
            {
                result.AddRange(kvp.Value);
                continue;
            }

            foreach (string t in checkEmojis)
            {
                if (!kvp.Key.Equals(t))
                    continue;
                result.AddRange(kvp.Value);
            }
        }

        sb.AppendLine(string.Format("total voters: {0}", result.Count));
        return result;
    }
}
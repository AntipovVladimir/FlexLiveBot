using System.Text;

namespace FlexLiveBot;

public class VoteSystem
{
    private Dictionary<long, voteItem> currentVotes = new Dictionary<long, voteItem>();

    public VoteSystem()
    {
    }


    internal string GetVoteResult(voteItem vote, ref int voteScore)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Результат голосования: ");
        voteScore = 0;
        List<string> oneSide = new List<string>();
        List<string> oppositeSide = new List<string>();
        foreach (var voter in vote.Votes.Values)
        {
            voteScore += voter.score;
            if (voter.score > 0)
            {
                oneSide.Add(voter.username);
            }
            else if (voter.score < 0)
                oppositeSide.Add(voter.username);
        }

        sb.Append("Голоса \"за\": ");
        sb.AppendLine(string.Join(", ", oneSide));
        if (oppositeSide.Count > 0)
        {
            sb.Append("Голосоа \"против\": ");
            sb.AppendLine(string.Join(", ", oppositeSide));
        }
        return sb.ToString();
    }

    public void RegisterVoting(long initiatorId, long myMessageId, long targetMessageId, int targetScore)
    {
        if (!currentVotes.ContainsKey(targetMessageId))
            currentVotes.Add(targetMessageId, new voteItem()
            {
                InitiatorId = initiatorId,
                MyMessageId = myMessageId,
                TargetMessageId = targetMessageId,
                TargetScore = targetScore
            });
    }

    public void RegisterVote(long userId, long targetMessageId, string username, int score)
    {
        if (!currentVotes.ContainsKey(targetMessageId))
            return;
        if (currentVotes[targetMessageId].Votes.ContainsKey(userId))
        {
            currentVotes[targetMessageId].CurrentScore -= currentVotes[targetMessageId].Votes[userId].score;
            currentVotes[targetMessageId].Votes[userId].score = score;
        }
        else
        {
            currentVotes[targetMessageId].Votes.Add(userId, new voteUser() {userId = userId, username = username, score = score});
        }
        currentVotes[targetMessageId].CurrentScore += score;
    }
}

internal class voteItem
{
    public long InitiatorId { get; set; }
    public long MyMessageId { get; set; }
    public long TargetMessageId { get; set; }
    public int TargetScore { get; set; }
    public int CurrentScore { get; set; }
    public Dictionary<long, voteUser> Votes { get; set; } = new Dictionary<long, voteUser>();
}

class voteUser
{
    public long userId { get; set; }
    public string username { get; set; } = string.Empty;
    public int score { get; set; }
}
namespace FlexLiveBot;

public static class Helpers
{
    public static bool DetectBool(this string word, bool target)
    {
        int result = word.DetectBool();
        if (result > 0)
            target = true;
        else if (result < 0)
            target = false;
        return target;
    }
    public static int DetectBool(this string word)
    {
        if (word.Equals("1") || word.Equals("true", StringComparison.OrdinalIgnoreCase) || word.Equals("on", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (word.Equals("0") || word.Equals("false", StringComparison.OrdinalIgnoreCase) || word.Equals("off", StringComparison.OrdinalIgnoreCase))
            return -1;
        return 0;
    }

    private static readonly Random rnd = new(DateTime.Now.Second);
    public static Random Rnd => rnd;
    
    /*
    public static IEnumerable<T> Get<T>(List<T> list)
    {
        if ((list is null) || (list.Count == 0))
            return Array.Empty<T>();
        list.Shuffle();
        int _rnd = Rnd.Next(list.Count - 1);
        List<T> newlist = new(_rnd);
        while (_rnd > 0)
        {
            newlist.Add(list[_rnd]);
            _rnd--;
        }
        return newlist;
    }
    */
    
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rnd.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }


    
}
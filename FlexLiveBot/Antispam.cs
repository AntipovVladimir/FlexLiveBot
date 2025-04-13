using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace FlexLiveBot;

[SuppressMessage("ReSharper", "UseStringInterpolation")]
public class Antispam
{
    private readonly Dictionary<int, SpamKeywordWithScore> SpamKeywordWithScores = new();
    private readonly List<string> BlackList = new();
    private readonly List<string> Banwords = new();
    private readonly List<string> ExceptionList = new();
    private readonly Regex pattern;
    public string Version { get; } = "1.2.15";
    private const string file_stringmap = "lastochka.stringmap.txt";
    private const string file_blacklist = "lastochka.antispam.blacklist";
    private const string file_banwords = "lastochka.antispam.banwords";
    private const string file_exceptions = "lastochka.antispam.exceptions";
    private const string file_learning = "lastochka.antispam.learning";
    private const string file_suspects = "lastochka.antispam.suspects";
    private const string file_scoredictionary = "lastochka.antispam.json";

    private readonly List<long> Suspects = new();

    private readonly Regex EmojiPattern;
    private readonly Regex CurrencyPattern;

    public int GetUnicodeChars(string str)
    {
        if (string.IsNullOrEmpty(str))
            return 0;
        int count = 0;
        foreach (int code in str)
        {
            if (code <= 0xff) // latin
                continue;
            //if (code is 0x401 or 0x451) continue;
            //if (code >= 0x410 & code <= 0x44f) continue; // cyrillic base
            //if (code >= 0x2bb & code <= 0x2bc) continue; //uz? 
            //if (code >= 0x2010 & code <= 0x2033) continue; //uz
//            if (code is 0x40E or 0x45E or 0x49A or 0x49B or 0x492 or 0x493 or 0x4B2 or 0x4B3)
            //              continue; //uz
            if (code >= 0x400 & code <= 0x4ff) // cyrillic full
                continue;
            if (code == 700) // ʼ
                continue;


            /* 
            if (code >= 0x500 & code <= 0x52f) // cyrillic suppliment
                continue;
            if (code >= 0x2de0 & code <= 0x2dff) // cyr ext-a
                continue;
            if (code >= 0xa640 & code <= 0xa62b) // cyr ext-b
                continue;
                */

            count++;
        }

        return count;
    }

    private readonly JsonSerializerOptions jso = new();

    private void LoadStringMap()
    {
        string data = LoadData(file_stringmap);
        foreach (string line in data.Split(','))
        {
            string[] kvp = line.ToLower().Split(':');
            if (!stringMapReplacements.ContainsKey(kvp[0][0]))
                stringMapReplacements.Add(kvp[0][0], kvp[1][0]);
            else
            {
                int x = kvp[0][0];
                Console.WriteLine("duplicate key {0} - {1}", kvp[0], x);
            }
        }

        Console.WriteLine("Loaded {0} string map replacements", stringMapReplacements.Count);
    }

    public Antispam()
    {
        LoadStringMap();
        jso.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        jso.WriteIndented = true;
        jso.PropertyNameCaseInsensitive = false;


        pattern = new(@"([ \№\`\~\!\@\#\$\%\^\&\*\(\)\-\=\{\}\[\]\;\:\'\<\>\.\,\/\?\|\\])+", RegexOptions.Compiled); // "[!,;.\t\r]");
        EmojiPattern = new(
            @"[#*0-9]\uFE0F?\u20E3|©\uFE0F?|[®\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA]\uFE0F?|[\u231A\u231B]|[\u2328\u23CF]\uFE0F?|[\u23E9-\u23EC]|[\u23ED-\u23EF]\uFE0F?|\u23F0|[\u23F1\u23F2]\uFE0F?|\u23F3|[\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB\u25FC]\uFE0F?|[\u25FD\u25FE]|[\u2600-\u2604\u260E\u2611]\uFE0F?|[\u2614\u2615]|\u2618\uFE0F?|\u261D(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642]\uFE0F?|[\u2648-\u2653]|[\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E]\uFE0F?|\u267F|\u2692\uFE0F?|\u2693|[\u2694-\u2697\u2699\u269B\u269C\u26A0]\uFE0F?|\u26A1|\u26A7\uFE0F?|[\u26AA\u26AB]|[\u26B0\u26B1]\uFE0F?|[\u26BD\u26BE\u26C4\u26C5]|\u26C8\uFE0F?|\u26CE|[\u26CF\u26D1\u26D3]\uFE0F?|\u26D4|\u26E9\uFE0F?|\u26EA|[\u26F0\u26F1]\uFE0F?|[\u26F2\u26F3]|\u26F4\uFE0F?|\u26F5|[\u26F7\u26F8]\uFE0F?|\u26F9(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\u26FA\u26FD]|\u2702\uFE0F?|\u2705|[\u2708\u2709]\uFE0F?|[\u270A\u270B](?:\uD83C[\uDFFB-\uDFFF])?|[\u270C\u270D](?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\u270F\uFE0F?|[\u2712\u2714\u2716\u271D\u2721]\uFE0F?|\u2728|[\u2733\u2734\u2744\u2747]\uFE0F?|[\u274C\u274E\u2753-\u2755\u2757]|\u2763\uFE0F?|\u2764(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79)|\uFE0F(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79))?)?|[\u2795-\u2797]|\u27A1\uFE0F?|[\u27B0\u27BF]|[\u2934\u2935\u2B05-\u2B07]\uFE0F?|[\u2B1B\u2B1C\u2B50\u2B55]|[\u3030\u303D\u3297\u3299]\uFE0F?|\uD83C(?:[\uDC04\uDCCF]|[\uDD70\uDD71\uDD7E\uDD7F]\uFE0F?|[\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|\uDE01|\uDE02\uFE0F?|[\uDE1A\uDE2F\uDE32-\uDE36]|\uDE37\uFE0F?|[\uDE38-\uDE3A\uDE50\uDE51\uDF00-\uDF20]|[\uDF21\uDF24-\uDF2C]\uFE0F?|[\uDF2D-\uDF35]|\uDF36\uFE0F?|[\uDF37-\uDF7C]|\uDF7D\uFE0F?|[\uDF7E-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93]|[\uDF96\uDF97\uDF99-\uDF9B\uDF9E\uDF9F]\uFE0F?|[\uDFA0-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCB\uDFCC](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCD\uDFCE]\uFE0F?|[\uDFCF-\uDFD3]|[\uDFD4-\uDFDF]\uFE0F?|[\uDFE0-\uDFF0]|\uDFF3(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08)|\uFE0F(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08))?)?|\uDFF4(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7]\uFE0F?|[\uDFF8-\uDFFF])|\uD83D(?:[\uDC00-\uDC07]|\uDC08(?:\u200D\u2B1B)?|[\uDC09-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC3A]|\uDC3B(?:\u200D\u2744\uFE0F?)?|[\uDC3C-\uDC3E]|\uDC3F\uFE0F?|\uDC40|\uDC41(?:\u200D\uD83D\uDDE8\uFE0F?|\uFE0F(?:\u200D\uD83D\uDDE8\uFE0F?)?)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDC70\uDC71](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC88-\uDC8E]|\uDC8F(?:\uD83C[\uDFFB-\uDFFF])?|\uDC90|\uDC91(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC92-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFC]|\uDCFD\uFE0F?|[\uDCFF-\uDD3D]|[\uDD49\uDD4A]\uFE0F?|[\uDD4B-\uDD4E\uDD50-\uDD67]|[\uDD6F\uDD70\uDD73]\uFE0F?|\uDD74(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\uDD75(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD76-\uDD79]\uFE0F?|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]\uFE0F?|\uDD90(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|\uDDA4|[\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA]\uFE0F?|[\uDDFB-\uDE2D]|\uDE2E(?:\u200D\uD83D\uDCA8)?|[\uDE2F-\uDE34]|\uDE35(?:\u200D\uD83D\uDCAB)?|\uDE36(?:\u200D\uD83C\uDF2B\uFE0F?)?|[\uDE37-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5]|\uDECB\uFE0F?|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDECF]\uFE0F?|[\uDED0-\uDED2\uDED5-\uDED7]|[\uDEE0-\uDEE5\uDEE9]\uFE0F?|[\uDEEB\uDEEC]|[\uDEF0\uDEF3]\uFE0F?|[\uDEF4-\uDEFC\uDFE0-\uDFEB])|\uD83E(?:\uDD0C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD34](?:\uD83C[\uDFFB-\uDFFF])?|\uDD35(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD36(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD37-\uDD39](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD3F-\uDD45\uDD47-\uDD76]|\uDD77(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD78\uDD7A-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCB]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD0|\uDDD1(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1|[\uDDAF-\uDDB3\uDDBC\uDDBD]))|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFC-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFD-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|[\uDDD2\uDDD3](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD4(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD5(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDD6-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDDE0-\uDDFF\uDE70-\uDE74\uDE78-\uDE7A\uDE80-\uDE86\uDE90-\uDEA8\uDEB0-\uDEB6\uDEC0-\uDEC2\uDED0-\uDED6])",
            RegexOptions.Compiled);
        CurrencyPattern = new(@"\p{Sc}", RegexOptions.Compiled);
        LoadSuspects();
        LoadExceptions();
        LoadDictionary();
        LoadBlacklist();
        LoadBanwords();
        LoadLearning();
    }

    public void Save()
    {
        SaveLearning();
        SaveBlacklist();
        SaveBanwords();
        SaveDictionary();
        SaveExceptions();
        SaveSuspects();
        // SaveStats();
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_saved));
    }

    private readonly Dictionary<char, char> stringMapReplacements = new();

    public IList<long> GetSuspects => Suspects;

    private string prepareString(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;
        return pattern.Replace(message, " ");
    }

    private string prepareSecondString(string message)
    {
        StringBuilder sb = new();
        foreach (char ch in message)
        {
            sb.Append(stringMapReplacements.TryGetValue(ch, out char value) ? value : ch);
        }

        return sb.ToString();
    }

    public string ClearDictionary(string locale)
    {
        SKWSKey = 0;
        SpamKeywordWithScores.Clear();
        return Localization.GetText(locale, LangEnum.s_antispam_dictionary_cleared);
    }


    public string AddToExceptions(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length > 0)
        {
            int olddic = ExceptionList.Count;
            AddToExeceptions(words.ToArray());
            int newdic = ExceptionList.Count;
            return newdic != olddic
                ? string.Format(Localization.GetText(locale, LangEnum.s_antispam_exceptions_added), newdic - olddic)
                : string.Format(Localization.GetText(locale, LangEnum.s_antispam_exceptions_unchanged), olddic);
        }

        return Localization.GetText(locale, LangEnum.s_antispam_exceptions_put_word);
    }

    private void AddToExeceptions(string[] words)
    {
        if (words.Length == 0)
            return;
        foreach (string word in words)
        {
            if (string.IsNullOrEmpty(word))
                continue;
            string w = word.Replace("!sp!", " ").ToLower();
            if (FoundInSpam.ContainsKey(w))
                FoundInSpam.TryRemove(w, out int _);
            if (!ExceptionList.Contains(w))
                ExceptionList.Add(w);
        }

        SaveExceptions();
    }

    private void AddToDictionary(string[] words)
    {
        if (words.Length == 0)
            return;
        foreach (string word in words)
        {
            string w = prepareSecondString(word.ToLower().Replace("!sp!", " "));
            if (string.IsNullOrWhiteSpace(w))
                continue;
            if (FoundInSpam.ContainsKey(w))
                FoundInSpam.TryRemove(w, out int _);
            bool found = false;
            foreach (SpamKeywordWithScore kw in SpamKeywordWithScores.Values)
            {
                if (kw.Keyword.Equals(w))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                SKWSKey++;
                SpamKeywordWithScores.Add(SKWSKey, new() { Keyword = w, Score = 1.0f });
            }
        }
    }

    public string AdjustScore(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length >= 2 && int.TryParse(words[0], out int index))
        {
            if (!SpamKeywordWithScores.ContainsKey(index))
                return Localization.GetText(locale, LangEnum.s_antispam_index_not_found);
            if (float.TryParse(words[1], NumberStyles.Any, CultureInfo.InvariantCulture, out float score))
            {
                SpamKeywordWithScores[index].Score = score;
                return string.Format(Localization.GetText(locale, LangEnum.s_antispam_word_adjusted), SpamKeywordWithScores[index].Keyword, score);
            }

            return Localization.GetText(locale, LangEnum.s_antispam_weight_not_parsed);
        }

        return Localization.GetText(locale, LangEnum.s_antispam_adjust_weight_requires);
    }

    public bool AdjustScore(int index, float score)
    {
        if (index < 0 || index > SpamKeywordWithScores.Count)
            return false;
        if (!SpamKeywordWithScores.ContainsKey(index))
            return false;
        SpamKeywordWithScores[index].Score = score;
        return true;
    }

    public string ReloadDictionary(string locale)
    {
        LoadDictionary();
        return string.Format(Localization.GetText(locale, LangEnum.s_antispam_dictionary_reloaded), SpamKeywordWithScores.Count);
    }

    private void AddToBlacklist(string[] words)
    {
        if (words.Length == 0)
            return;
        foreach (string word in words)
        {
            if (!word.StartsWith('@'))
                continue;
            string w = word.Trim().ToLower();
            if (w.Length < 3)
                continue;
            if (BlackList.Contains(w))
                continue;
            BlackList.Add(w);
        }

        SaveBlacklist();
    }


    private bool LoadBanwords()
    {
        Banwords.Clear();
        string data = LoadData(file_banwords);
        if (string.IsNullOrWhiteSpace(data))
            return false;
        AddToBanwords(data.Split('\n'));
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_blacklist_loaded), Banwords.Count);
        return true;
    }

    private void AddToBanwords(string[] words)
    {
        if (words.Length == 0)
            return;
        foreach (string word in words)
        {
            string w = prepareSecondString(word.ToLower().Replace("!sp!", " "));
            if (string.IsNullOrWhiteSpace(w))
                continue;

            //string w = word.Trim().ToLower();
            if (w.Length < 1)
                continue;
            if (Banwords.Contains(w))
                continue;
            Banwords.Add(w);
        }

        SaveBanwords();
    }

    private bool LoadBlacklist()
    {
        BlackList.Clear();
        string data = LoadData(file_blacklist);
        if (string.IsNullOrWhiteSpace(data))
            return false;
        AddToBlacklist(data.Split('\n'));
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_blacklist_loaded), BlackList.Count);
        return true;
    }


    private string LoadData(string fileToRead)
    {
        try
        {
            string data = File.ReadAllText(fileToRead);
            return data.Replace("\r\n", "\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return string.Empty;
    }

    private int SKWSKey;

    private bool LoadDictionary()
    {
        SpamKeywordWithScores.Clear();
        SKWSKey = 0;

        List<SpamKeywordWithScore> ilist = JsonHelpers.Load<List<SpamKeywordWithScore>>(file_scoredictionary);
        if (ilist is null) return false;
        for (SKWSKey = 0; SKWSKey < ilist.Count; SKWSKey++)
            SpamKeywordWithScores.Add(SKWSKey, ilist[SKWSKey]);
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_dictionary_loaded), SpamKeywordWithScores.Count);
        return true;
    }

    public void AddSuspect(long uid)
    {
        if (!Suspects.Contains(uid))
            Suspects.Add(uid);
    }

    public void RemoveSuspect(long uid)
    {
        if (Suspects.Contains(uid))
            Suspects.Remove(uid);
    }

    public bool IsSuspect(long uid)
    {
        return Suspects.Contains(uid);
    }

    private bool LoadSuspects()
    {
        Suspects.Clear();
        List<long> items = JsonHelpers.Load<List<long>>(file_suspects);
        if (items is not null)
            Suspects.AddRange(items);
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_suspects_loaded), Suspects.Count);
        return true;
    }

    private bool SaveSuspects()
    {
        try
        {
            string data = JsonSerializer.Serialize(Suspects);
            File.WriteAllText(file_suspects, data);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return false;
    }

    private bool LoadExceptions()
    {
        ExceptionList.Clear();
        string data = LoadData(file_exceptions);
        if (string.IsNullOrWhiteSpace(data))
            return false;
        AddToExeceptions(data.Split('\n'));
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_exceptions_loaded), ExceptionList.Count);
        return true;
    }


    private bool SaveList(IList<string> list, string fileToSave)
    {
        try
        {
            StringBuilder sb = new();
            foreach (string word in list)
                sb.AppendLine(word);
            if (sb.Length > 0)
            {
                File.WriteAllText(fileToSave, sb.ToString().Replace("\r\n", "\n"));
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return false;
    }

    private bool SaveBlacklist()
    {
        return SaveList(BlackList, file_blacklist);
    }

    private bool SaveBanwords()
    {
        return SaveList(Banwords, file_banwords);
    }

    private bool SaveDictionary()
    {
        try
        {
            string data = JsonSerializer.Serialize(SpamKeywordWithScores.Values.ToList(), jso);
            File.WriteAllText(file_scoredictionary, data);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    private bool SaveExceptions()
    {
        return SaveList(ExceptionList, file_exceptions);
    }

    public string AdjustEmojis(string message, string locale, ref int score, out int found, int adjScore = 2, int maxCount = 4)
    {
        MatchCollection matches = EmojiPattern.Matches(message);
        StringBuilder sb = new();
        int overlimit = maxCount * 10;
        int perlimit = maxCount * 5;
        found = 0;
        int foundSafe = 0;
        int total = 0;
        int foundUnsafe = 0;
        if (matches.Count > 0)
        {
            foreach (Match m in matches)
            {
                if (SafeEmojis.Contains(m.Value))
                    foundSafe += m.Length;
                else
                    foundUnsafe++;
                sb.Append(m.Value);
                found += m.Length;
                total++;
            }

            sb.Append(string.Format(Localization.GetText(locale, LangEnum.s_antispam_detected_emojis), total));
            if (foundUnsafe > maxCount)
                score += adjScore;
            if (foundUnsafe > perlimit)
                score += adjScore;
            if (foundUnsafe > overlimit)
                score += adjScore * 2;
            if (total > perlimit)
                score += adjScore;
            if (total > overlimit)
                score += adjScore * 2;
        }

        return sb.ToString();
    }

    public bool HasBanwords(string msg)
    {
        string text = msg.ToLower();
        string message1 = pattern.Replace(text, " ");
        string message2 = prepareSecondString(message1);
        string message3 = pattern.Replace(text, string.Empty);
        string message4 = prepareSecondString(message3);

        foreach (string word in Banwords)
        {
            if (message1.Contains(word) || message2.Contains(word) || message3.Contains(word) || message4.Contains(word))
                return true;
        }

        return false;
    }

    public int GetScore(Message msg, string locale, StringBuilder sb, int maxscore = 5, bool testmode = false)
    {
        if (string.IsNullOrWhiteSpace(msg.Text))
            return 0;
        string message = msg.Text.ToLower();
        string message1 = pattern.Replace(message, " ");
        string message2 = prepareSecondString(message1);
        string message3 = pattern.Replace(message, string.Empty);
        string message4 = prepareSecondString(message3);
        if (testmode && message.Length < 256)
        {
            sb.Append("[test1]: ");
            sb.AppendLine(message1);
            sb.Append("[test2]: ");
            sb.AppendLine(message2);
            sb.Append("[test3]: ");
            sb.AppendLine(message3);
            sb.Append("[test4]: ");
            sb.AppendLine(message4);
        }

        foreach (string word in BlackList)
        {
            if (!message.Contains(word))
                continue;
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_blacklist_found), word));
            return 1000;
        }

        float score = 0;
        int exceptionscore = 0;
        foreach (string word in ExceptionList)
        {
            if (!message1.Contains(word))
                continue;
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_exception_found), word));
            exceptionscore++;
        }

        Dictionary<int, int> ignoredRanges = new();
        string currencyTest = message;
        if (msg.Entities != null)
        {
            foreach (MessageEntity ent in msg.Entities)
            {
                if (ent.Type is MessageEntityType.Code or MessageEntityType.Pre or MessageEntityType.TextLink or MessageEntityType.Url)
                {
                    ignoredRanges.Add(ent.Offset, ent.Length);
                }
            }

            StringBuilder csb = new();
            int index = 0;
            foreach (KeyValuePair<int, int> kvp in ignoredRanges)
            {
                if (message.Length >= index && message.Length >= index + kvp.Key)
                {
                    csb.Append(message.Substring(index, kvp.Key));
                    index += kvp.Value;
                }
            }

            currencyTest = csb.ToString();
        }

        MatchCollection currencys = CurrencyPattern.Matches(currencyTest);

        if (currencys.Count > 0)
        {
            sb.Append(string.Format(Localization.GetText(locale, LangEnum.s_antispam_found_currencys), currencys.Count));
            foreach (Match c in currencys)
            {
                sb.Append(c.ToString());
            }

            if (exceptionscore == 0)
            {
                score += currencys.Count;
                sb.AppendLine(Localization.GetText(locale, LangEnum.s_antispam_no_exceptions));
            }
        }

        string[] words = message.Split(' ');
        int totalWords = words.Length;
        int longWords = 0;
        foreach (string word in words)
        {
            if (word.Length > 5)
                longWords++;
        }

        score -= exceptionscore * 0.5f;
        List<string> foundWords = new();
        foreach (KeyValuePair<int, SpamKeywordWithScore> kvp in SpamKeywordWithScores)
        {
            SpamKeywordWithScore word = kvp.Value;
            if (message1.Contains(word.Keyword) || message2.Contains(word.Keyword) || message3.Contains(word.Keyword) || message4.Contains(word.Keyword))
            {
                foundWords.Add(word.Keyword);
                kvp.Value.Hits++;
                score += word.Score;
            }
        }

        if (foundWords.Count > 0)
        {
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_spam_found), string.Join(", ", foundWords.ToArray())));
        }

        if (foundWords.Count > 0 && score >= maxscore)
        {
            string wowstring = message1;
            foreach (string word in foundWords)
            {
                wowstring = wowstring.Replace(word, string.Empty);
            }

            if (wowstring.Length > 0)
                m_task_factory.StartNew(() => { AddFoundInSpam(wowstring.Split(' ')); });
        }

        if (score >= maxscore)
        {
            if (totalWords >= 60 && longWords >= 40)
                score /= 2;
        }

        return (int)Math.Round(score);
    }

    private ConcurrentDictionary<string, int> FoundInSpam = new();

    private readonly TaskFactory m_task_factory = new();

    private void AddFoundInSpam(string[] words)
    {
        foreach (string word in words)
        {
            if (word.Length < 3)
                continue;
            if (!FoundInSpam.ContainsKey(word))
                FoundInSpam.TryAdd(word, 0);
            FoundInSpam[word]++;
        }
    }

    public string GetSpamUnused(string locale)
    {
        StringBuilder sb = new();
        int i = 0;
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_top10_spam)));
        foreach (KeyValuePair<int, SpamKeywordWithScore> kvp in SpamKeywordWithScores)
        {
            if (kvp.Value.Hits > 0)
                continue;
            i++;
            sb.Append(i);
            sb.Append('[');
            sb.Append(kvp.Key);
            sb.Append("] : ");
            sb.Append(kvp.Value.Keyword);
            sb.Append(" = ");
            sb.AppendLine(kvp.Value.Hits.ToString());
            if (i == 10) break;
        }

        return sb.ToString();
    }

    public string GetSpamTop(string locale)
    {
        StringBuilder sb = new();
        int i = 0;
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_top10_spam)));
        foreach (KeyValuePair<int, SpamKeywordWithScore> kvp in SpamKeywordWithScores.OrderByDescending(key => key.Value.Hits))
        {
            i++;
            sb.Append(i);
            sb.Append('[');
            sb.Append(kvp.Key);
            sb.Append("] : ");
            sb.Append(kvp.Value.Keyword);
            sb.Append(" = ");
            sb.AppendLine(kvp.Value.Hits.ToString());
            if (i == 10) break;
        }

        return sb.ToString();
    }

    public string GetLearningTop(string locale)
    {
        StringBuilder sb = new();
        int i = 0;
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_top10_learn)));
        foreach (KeyValuePair<string, int> kvp in FoundInSpam.OrderByDescending(key => key.Value))
        {
            i++;
            sb.Append(i);
            sb.Append(' ');
            sb.Append(kvp.Key);
            sb.Append(" : ");
            sb.AppendLine(kvp.Value.ToString());

            if (i == 10) break;
        }

        return sb.ToString();
    }

    private bool SaveLearning()
    {
        if (FoundInSpam.Count == 0)
            return false;
        string data = JsonSerializer.Serialize(FoundInSpam);
        try
        {
            File.WriteAllText(file_learning, data);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        return false;
    }

    private bool LoadLearning()
    {
        FoundInSpam.Clear();
        ConcurrentDictionary<string, int> dict = JsonHelpers.Load<ConcurrentDictionary<string, int>>(file_learning);
        if (dict is not null)
            FoundInSpam = dict;
        Console.WriteLine(Localization.GetText("ru", LangEnum.s_antispam_learning_loaded), FoundInSpam.Count);
        return true;
    }

    private readonly List<string> SafeEmojis = new() { "😭", "😂", "🤪", "🙈", "😊", "🤗", "🤣", "🤝", "🫡" };

    public string TestScore(Message message, string locale, int adjScore = 2, int maxCount = 4)
    {
        if (message.Text is null)
        {
            Console.WriteLine("there's no text");
            return string.Empty;
        }

        StringBuilder sb = new();
        try
        {
            int spamscore = GetScore(message, locale, sb, 5, true);
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_text_scoring), spamscore));
            string emojis = AdjustEmojis(message.Text, locale, ref spamscore, out int foundEmojis, 2, 4);
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_emojis_adjusted), emojis, spamscore));
            int foundWrongUnicode = GetUnicodeChars(message.Text);
            int delta = foundWrongUnicode - foundEmojis;
            spamscore += delta / 3;
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_unsupported_unicode), delta));
            sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_total_score_brief), spamscore));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }

        return sb.ToString();
    }

    public string GetStat(string locale)
    {
        StringBuilder sb = new();
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_stat_spamkeywords), SpamKeywordWithScores.Count));
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_stat_blacklist), BlackList.Count, ExceptionList.Count));
        sb.AppendLine(string.Format(Localization.GetText(locale, LangEnum.s_antispam_stat_suspects), Suspects.Count, FoundInSpam.Count));
        return sb.ToString();
    }

    private static readonly List<string> Urls = new()
    {
        "tg://", ".t.me", "http://", "https://", "telegram.me", "telegram.dog", "tonsite://", "telegra.ph"
    };

    public bool ContainsUrl(string message)
    {
        foreach (string url in Urls)
        {
            if (message.Contains(url, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public string AddToBlacklist(ReadOnlySpan<string> words, string locale)
    {
        string result;
        if (words.Length > 0)
        {
            int olddic = BlackList.Count;
            AddToBlacklist(words.ToArray());
            int newdic = BlackList.Count;
            result = (newdic != olddic)
                ? string.Format(Localization.GetText(locale, LangEnum.s_antispam_blacklist_expanded), newdic - olddic)
                : string.Format(Localization.GetText(locale, LangEnum.s_antispam_blacklist_unchanged), olddic);
        }
        else
            result = Localization.GetText(locale, LangEnum.s_antispam_blacklist_put_word_to_add);

        return result;
    }

    public string AddToBanwords(ReadOnlySpan<string> words, string locale)
    {
        string result;
        if (words.Length > 0)
        {
            int olddic = Banwords.Count;
            AddToBanwords(words.ToArray());
            int newdic = Banwords.Count;
            result = (newdic != olddic)
                ? string.Format(Localization.GetText(locale, LangEnum.s_antispam_banwords_expanded), newdic - olddic)
                : string.Format(Localization.GetText(locale, LangEnum.s_antispam_banwords_unchanged), olddic);
        }
        else
            result = Localization.GetText(locale, LangEnum.s_antispam_banwords_put_word_to_add);

        return result;
    }

    private bool RemoveFromBanwords(string word)
    {
        string w = word.Trim().ToLower();
        return w.Length >= 3 && Banwords.Contains(w) && Banwords.Remove(w);
    }


    public string RemoveFromBanwords(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length > 0)
            return Localization.GetText(locale, RemoveFromBanwords(words[0])
                ? LangEnum.s_antispam_banwords_removed
                : LangEnum.s_antispam_banwords_unchanged_notfound);
        return Localization.GetText(locale, LangEnum.s_antispam_banwords_put_word);
    }

    private bool RemoveFromBlacklist(string word)
    {
        if (!word.StartsWith('@'))
            return false;
        string w = word.Trim().ToLower();
        return w.Length >= 3 && BlackList.Contains(w) && BlackList.Remove(w);
    }

    public string RemoveFromBlacklist(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length > 0)
            return Localization.GetText(locale, RemoveFromBlacklist(words[0])
                ? LangEnum.s_antispam_blacklist_removed
                : LangEnum.s_antispam_blacklist_unchanged_notfound);
        return Localization.GetText(locale, LangEnum.s_antispam_blacklist_put_word);
    }

    public string AddAsSpam(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length > 0)
        {
            int olddic = SpamKeywordWithScores.Count;
            AddToDictionary(words.ToArray());
            int newdic = SpamKeywordWithScores.Count;
            return newdic != olddic
                ? string.Format(Localization.GetText(locale, LangEnum.s_antispam_dictionary_expanded), newdic - olddic)
                : string.Format(Localization.GetText(locale, LangEnum.s_antispam_dictionary_unchanged), olddic);
        }

        return Localization.GetText(locale, LangEnum.s_antispam_dictionary_put_word);
    }

    public string RemoveFromSpam(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length > 0)
            if (int.TryParse(words[0], out int index))
                return Localization.GetText(locale, SpamKeywordWithScores.Remove(index)
                    ? LangEnum.s_antispam_removed_from_dictionary
                    : LangEnum.s_antispam_dictionary_unchanged_notfound);
        return Localization.GetText(locale, LangEnum.s_antispam_dictionary_put_index_to_remove);
    }

    public string Search(ReadOnlySpan<string> words, string locale)
    {
        if (words.Length == 0)
            return string.Empty;
        StringBuilder sb = new();
        sb.AppendLine(Localization.GetText(locale, LangEnum.s_antispam_search_header));
        foreach (string word in words)
        {
            if (string.IsNullOrEmpty(word))
                continue;
            string message = prepareString(word);
            string message2 = prepareSecondString(message).ToLower();
            string message1 = message.ToLower();

            string w = prepareSecondString(word.Replace("!sp!", " ")).ToLower();
            foreach (KeyValuePair<int, SpamKeywordWithScore> dic in SpamKeywordWithScores)
            {
                if (message1.Contains(dic.Value.Keyword, StringComparison.Ordinal)
                    || message2.Contains(dic.Value.Keyword, StringComparison.Ordinal)
                    || dic.Value.Keyword.Contains(w)
                    || dic.Value.Keyword.Contains(message1)
                    || dic.Value.Keyword.Contains(message2))
                    sb.AppendLine(string.Format("[{0}]\t{1}\t[{2}]\t[{3}]", dic.Key, dic.Value.Keyword, dic.Value.Score, dic.Value.Hits));
            }
        }

        return sb.ToString();
    }

    private readonly Dictionary<long, ReactionFlood> ReactionFloods = new();

    public bool CheckReactionFlood(long uid, int maxcount, int timedelta)
    {
        if (!ReactionFloods.ContainsKey(uid))
        {
            ReactionFloods.Add(uid, new() { reactions = 1, tickCount = Environment.TickCount64 });
            return false;
        }

        ReactionFloods[uid].reactions++;
        if (ReactionFloods[uid].tickCount + timedelta > Environment.TickCount64)
        {
            if (ReactionFloods[uid].reactions >= maxcount)
                return true;
        }
        else
        {
            ReactionFloods[uid].tickCount = Environment.TickCount64;
            ReactionFloods[uid].reactions = 0;
        }

        return false;
    }
}
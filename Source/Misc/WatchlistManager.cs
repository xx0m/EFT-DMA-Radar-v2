using System.Text.RegularExpressions;

namespace eft_dma_radar
{
    public static class WatchlistManager
    {
        static Dictionary<string, string> StreamerList = new Dictionary<string, string>
        {
            {"835112", "lvndmark"},
            {"376689", "summit1g"},
            {"11387881", "tigz"},
            {"165994", "willerz"},
            {"1049972", "hutchmf"},
            {"1090448", "DrLupo"},
            {"2989797", "viibiin"},
            {"354136", "klean"},
            {"1717857", "jessekazam"},
            {"1448970", "glorious_e"},
            {"3391828", "nyxia"},
            {"2438239", "xblazed"},
            {"831617", "velion"},
            {"3526004", "gingy"},
            {"4637816", "trey24k"},
            {"763945", "aquafps"},
            {"2111653", "bakeezy"},
            {"1663669", "desmondpilak"},
            {"1153634", "2thy"},
            {"2165308", "smittystone"},
            {"2095752", "blueberrygabi"},
            {"3982736", "gl40labsrat"},
            {"971133", "rengawr"},
            {"1080203", "hyperrattv"},
            {"1425172", "axel_tv"},
            {"4897027", "annemunition"},
            {"9347828", "honeyxxo"},
            {"2058310", "moman"},
            {"5006226", "cooldee__"},
            {"927745", "goes"},
            {"3424723", "binoia"},
            {"4609337", "ponch"},
            {"8484894", "nogenerals"},
            {"4764608", "tobytwofaced"},
            {"2043138", "kkersanovtv"},
            {"2334119", "jaybaybay"},
            {"1942597", "cwis0r"},
            {"1294950", "wildez"},
            {"6541088", "shoes"},
            {"3351793", "nohelmetchad"},
            {"2250762", "mismagpie"},
            {"654070", "cryodrollic"},
            {"5158172", "undeadessence"},
            {"3928278", "aims"},
            {"9351502", "burgaofps"},
            {"739353", "knueppelpaste"},
            {"1312997", "vonza"},
            {"4168016", "endra"},
            {"2739217", "volayethor"},
            {"2763053", "mzdunk"},
            {"2329796", "philbo"},
            {"3400742", "fudgexl"},
            {"859833", "baxbeast"},
            {"766970", "genooo"},
            {"1758499", "someman"},
            {"2773520", "skidohunter"},
            {"3569522", "realkraftyy"},
            {"2554678", "rileyarmageddon"},
            {"3998491", "kongstyle101"},
            {"1632126", "wenotrat"},
            {"5550265", "tomrander"},
            {"790774", "thepoolshark"},
            {"2673247", "shotsofvaca_"},
            {"2991546", "smol"},
            {"11404544", "suddenly_toast"},
            {"4825441", "doubledstroyer"},
            {"2755056", "valarman"},
            {"1481309", "anton"},
            {"5311265", "vazquez66"},
            {"10799845", "ashnue"},
            {"7225268", "crylixblooom"},
            {"8336334", "swirrrly"},
            {"1712951", "mvze_"},
            {"4194405", "shwiftyfps"},
            {"885958", "switch360tv"},
            {"6567825", "strongeo"},
            {"5711540", "jewlee"},
            {"926010", "toastracktv"},
            {"2277116", "imbobby__"},
            {"851122", "cocaoo_"},
            {"4034904", "verybadscav"},
            {"1706161", "furl3x"},
            {"2031346", "maza4kst"},
            {"3042051", "wardell"},
            {"2207216", "logicalsolutions"},
            {"3515629", "daskicosin"},
            {"10480940", "chi_chaan"},
            {"2971732", "myst1s"},
            {"2592389", "pixel8_ttv"},
            {"1827749", "applebr1nger"},
            {"6170674", "wo1f_gg"},
            {"4544185", "impatiya"},
            {"1126512", "torkie"},
            {"5602537", "schmidttyb"},
            {"3330252", "blinge1"},
            {"1526877", "trentau"},
            {"3581557", "tqmo__"},
            {"1161451", "gerysenior"},
            {"1002256", "wondows"},
            {"922156", "hadess31"},
            {"7674224", "cujoman"},
            {"11468155", "butecodofranco"},
            {"11013668", "joeliain2310"},
            {"11118058", "moonshinefps"},
            {"8115752", "renalakec"},
            {"3118179", "soultura86"},
            {"7085963", "notoriouspdx"},
            {"10959843", "ry784"},
            {"3047477", "strngerping"},
            {"5646257", "mushamaru_"},
            {"3539914", "rguardian"},
            {"9827614", "codex011"},
            {"5463289", "wabrat"},
            {"7104272", "fiathegemini"},
            {"839191", "notechniquetv"},
            {"5051655", "dkaye23"},
            {"8788838", "mrbubblyttv"},
            {"2799174", "sweetyboom"},
            {"5308968", "oggyshoggy"},
            {"427222", "steeyo"},
            {"364768", "hayz"},
            {"4411189", "hayz"},
            {"5353635", "stankrat_"},
            {"2614961", "oberst0m"},
            {"6815534", "thatfriendlyguy"},
            {"3441806", "JohnBBeta"},
            {"3285221", "Siisco_ttv"},
            {"2238335", "zchum"},
            {"8016990", "mistofhazmat"},
            {"858816", "hipperpyah"},
            {"380648", "sektenspinner"},
            {"408825", "bubbinger"},
            {"2215415", "raggelton"},
            {"2693789", "zcritic"},
            {"9283718", "triple_g"},
            {"546813", "pepp"},
            {"4432653", "hexloom"},
            {"9826933", "satsuki_hotaru"},
            {"2699481", "headleyy"},
            {"2366827", "thomaspaste"},
            {"1699605", "taxfree_"},
            {"2536192", "Airwingmarine"},
            {"5378845", "thepridge"}
        };

        public static bool IsStreamer(string accountID, string username, out string streamerName)
        {
            if (WatchlistManager.StreamerList.ContainsKey(accountID))
            {
                streamerName = StreamerList[accountID];
                return true;
            }

            if (WatchlistManager.StreamerList.ContainsValue(username))
            {
                streamerName = username;
                return true;
            }

            if (Regex.IsMatch(username, @"ttv", RegexOptions.IgnoreCase))
            {
                Match match = Regex.Match(username, @"(.*?)ttv|ttv(.*)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    streamerName = match.Groups[1].Value + match.Groups[2].Value;
                    return true;
                }
            }

            streamerName = null;
            return false;
        }

        public static async Task<bool> IsLive(string username)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync($"https://twitch.tv/{username}");
                    var sourceCode = await response.Content.ReadAsStringAsync();

                    return sourceCode.Contains("isLiveBroadcast");
                }
                catch (Exception ex)
                {
                    Program.Log("Error occurred: " + ex.Message);
                    return false;
                }
            }
        }
    }
}

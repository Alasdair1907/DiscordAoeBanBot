using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DiscordAoeBanBot
{
    class Fetcher
    {
        public static readonly string lobbyRequest = "https://aoe2.net/api/lobbies?game=aoe2de";
        public static readonly string playersByNameRequestPartial = "https://aoe2.net/api/leaderboard?game=aoe2de&leaderboard_id=0&start=1&count=20&search=";
        public static readonly string playerBySteamIdRequestPartial = "https://aoe2.net/api/leaderboard?game=aoe2de&leaderboard_id=0&start=1&count=1&steam_id=";
        public static readonly string playerByProfileIdRequestPartial = "https://aoe2.net/api/leaderboard?game=aoe2de&leaderboard_id=0&start=1&count=1&profile_id=";
        public static readonly string playersMatchHistoryRequestPartial = "https://aoe2.net/api/player/matches?game=aoe2de&count=2&start=0&profile_id=";

        public static List<Lobby> GetLobbies()
        {
            string data;
            using (WebClient client = new MyWebClient())
            {
                client.Encoding = Encoding.UTF8;
                data = client.DownloadString(lobbyRequest);
            }

            if (string.IsNullOrWhiteSpace(data))
            {
                return new List<Lobby>();
            }

            List<Lobby> lobbies = JsonConvert.DeserializeObject<List<Lobby>>(data);
            return lobbies;
        }

        public static List<Lobby> GetPlayersMatchHistory(string profileId)
        {
            try
            {
                int.Parse(profileId);
            }
            catch (Exception ex)
            {
                return null;
            }

            string data = DownloadString(playersMatchHistoryRequestPartial + profileId);

            if (string.IsNullOrWhiteSpace(data))
            {
                return new List<Lobby>();
            }

            List<Lobby> lobbies = JsonConvert.DeserializeObject<List<Lobby>>(data);
            return lobbies;
        }

        public static List<Player> FindPlayersByName(string nickName)
        {
            string encodedNickname = HttpUtility.UrlEncode(nickName);
            if (nickName.Contains('\r') || nickName.Contains('\n'))
            {
                return null;
            }

            string request = playersByNameRequestPartial + encodedNickname;
            string data = DownloadString(request);

            if (string.IsNullOrWhiteSpace(data))
            {
                return new List<Player>();
            }

            LeaderboardResult leaderboardResult = JsonConvert.DeserializeObject<LeaderboardResult>(data);
            return leaderboardResult.Leaderboard;
        }

        public static Player FindPlayerById(string steamId, string profileId)
        {
            if (steamId == null && profileId == null)
            {
                return null;
            }

            if (steamId != null && !Regex.IsMatch(steamId, @"[0-9]{5,20}"))
            {
                return null;
            }

            if (profileId != null && !Regex.IsMatch(profileId, @"[0-9]{3,10}"))
            {
                return null;
            }

            if (profileId != null)
            {
                try
                {
                    int.Parse(profileId);
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
            

            string request;
            if (!string.IsNullOrWhiteSpace(steamId))
            {
                request = playerBySteamIdRequestPartial + steamId;
            } else
            {
                request = playerByProfileIdRequestPartial + profileId;
            }
            
            string data = DownloadString(request);
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            LeaderboardResult leaderboardResult = JsonConvert.DeserializeObject<LeaderboardResult>(data);
            if (leaderboardResult.Leaderboard != null && leaderboardResult.Count == 1)
            {
                return leaderboardResult.Leaderboard[0];
            }

            return null;
        }

        

        private static string DownloadString(string request)
        {
            string data;
            using (WebClient client = new MyWebClient())
            {
                client.Encoding = Encoding.UTF8;
                data = client.DownloadString(request);
            }

            return data;
        }


        private class MyWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest webRequest = base.GetWebRequest(address);
                webRequest.Timeout = 20 * 1000;
                return webRequest;
            }
        }


    }
}

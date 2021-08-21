using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class LeaderboardResult
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("leaderboard")]
        public List<Player> Leaderboard { get; set; }
    }
}

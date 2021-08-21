using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Lobby
    {
        [JsonProperty("lobby_id")]
        public String LobbyId { get; set; }

        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("match_id")]
        public String MatchId { get; set; }

        [JsonProperty("players")]
        public List<Player> Players { get; set; }

        [JsonProperty("num_players")]
        public int NumPlayers { get; set; }

        [JsonProperty("num_slots")]
        public int NumSlots { get; set; }

    }
}

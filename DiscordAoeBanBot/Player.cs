using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Player
    {

        [JsonProperty("profile_id")]
        public String ProfileId { get; set; }

        [JsonProperty("steam_id")]
        public String SteamId { get; set; }

        [JsonProperty("name")]
        public String Name { get; set; }

        [JsonProperty("country")]
        public String Country { get; set; }

    }
}

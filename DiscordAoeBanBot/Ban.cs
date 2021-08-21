using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Ban
    {
        public int BanId { get; set; }
        public string ProfileId { get; set; }
        public string SteamId { get; set; }
        public string NickWhenBanned { get; set; }
        public string BannedBy { get; set; }
        public string Reason { get; set; }
    }
}

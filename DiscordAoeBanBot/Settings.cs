using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Settings
    {
        public string DiscordToken { get; set; } // obtained from https://discord.com/developers/applications
        public string BansChannelName { get; set; } // channel where bot will receive commands
        public string NotificationsChannelName { get; set; } // channel where bot will notify users about bad players
        public string ServerName { get; set; } // if bot gets connected to multiple guilds, it will interact only with this one
        public List<string> UnbanRoles { get; set; } // list of role names, users of which are allowed to unban
    }
}

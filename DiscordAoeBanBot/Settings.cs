using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Settings
    {
        public String DiscordToken { get; set; } // obtained from https://discord.com/developers/applications
        public String BansChannelName { get; set; } // channel where bot will receive commands
        public String NotificationsChannelName { get; set; } // channel where bot will notify users about bad players
        public String ServerName { get; set; } // if bot gets connected to multiple guilds, it will interact only with this one
    }
}

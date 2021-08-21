using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Discord.WebSocket;

namespace DiscordAoeBanBot
{
    public class Warning
    {
        public Lobby Lobby { get; set; }
        public List<Player> BadPlayers { get; set; }
        public List<Player> GoodPlayers { get; set; }

        public List<Ban> Bans { get; set; }

        public string WarningHash()
        {
            string badPlayers = string.Join(",", BadPlayers.Select(p => p.Name).ToList());
            string goodPlayers = string.Join(",", GoodPlayers.Select(p => p.Name).ToList());
            string warningIdentifier = Lobby.LobbyId + badPlayers + goodPlayers;
            return Util.SHA256Str(warningIdentifier);
        }

        public string ToMessage(List<SocketGuildUser> guildUsers, List<Ban> banList)
        {
            List<string> goodPlayersNames = GoodPlayers.Select(p => p.Name).ToList();
            List<SocketGuildUser> usersToWarn = guildUsers.Where(gU => goodPlayersNames.Contains(gU.Nickname ?? gU.Username)).ToList();

            StringBuilder sb = new StringBuilder();
            foreach (SocketGuildUser sgu in usersToWarn)
            {
                sb.Append(sgu.Mention + " ");
            }

            sb.Append(" Following banned users have been detected in the lobby \"**");
            sb.Append(Lobby.Name);
            sb.Append("**\":\r\n");


            foreach (Player banned in BadPlayers)
            {
                List<Ban> correspondingBans = banList.Where(b => b.ProfileId == banned.ProfileId || b.SteamId == banned.SteamId).ToList();
                sb.Append(string.Format("player \"**{0}**\" originally known as \"{1}\" (Steam ID {2}) (aoe2.net Profile ID {3})\r\n",
                    banned.Name, correspondingBans[0].NickWhenBanned, correspondingBans[0].SteamId, correspondingBans[0].ProfileId));
                foreach (Ban ban in correspondingBans)
                {
                    sb.Append("banned by " + ban.BannedBy + " for reason: __" + ban.Reason + "__\r\n");
                }

            }

            return sb.ToString();
        }
    }
}

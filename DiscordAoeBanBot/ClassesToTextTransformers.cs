using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace DiscordAoeBanBot
{
    public class ClassesToTextTransformers
    {
        public static string PlayersToLookupCandidates(List<Player> players)
        {
            if (players == null || players.Count < 1)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();

            sb.Append("Player name (Country) [Steam ID] [aoe2.net Profile ID]\r\n");

            foreach (Player player in players)
            {
                sb.Append(string.Format("{0} ({1}) [{2}] [{3}]\r\n", player.Name, player.Country, player.SteamId, player.ProfileId));
            }

            return sb.ToString();
        }

        public static string LobbiesToHistory(List<Lobby> lobbies)
        {
            StringBuilder sb = new StringBuilder();

            //sb.Append("[Lobby name](Player name/ProfileID/SteamID)\r\n");

            foreach (Lobby lobby in lobbies)
            {
                sb.Append("__"+lobby.Name + "__\r\n");
                foreach (Player player in lobby.Players)
                {
                    sb.Append("**" + player.Name + "** Profile ID: " + player.ProfileId + " Steam ID: " + player.SteamId + "\r\n");
                }
                sb.Append("\r\n");
            }

            return sb.ToString();
        }
    }
}

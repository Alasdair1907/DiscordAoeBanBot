using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

namespace DiscordAoeBanBot
{
    class Program
    {
        private DiscordSocketClient discordSocketClient;
        private static string discordToken = "DISCORD_TOKEN_GOES_THERE";
        private static string channelName = "bans"; // channel where the bot will respond to commands
        private static string notificationsChannelName = "game-warnings"; // channel where the bot will warn discord users
        private static string serverName = "AoeDiscordBotTest"; // server name - bot will respond only to this server


        private static Regex lookupCommandRegex = new Regex(@"\!lookup (?'nick'.+)");
        private static Regex banCommandRegex = new Regex(@"\!(ban|bansteam) (?'id'[0-9]{3,20}) (?'reason'.+)");
        private static Regex historyNicknameRegex = new Regex(@"\!history (?'nick'.+)");
        private static Regex historySteamRegex = new Regex(@"\!historysteam (?'id'[0-9]{3,20})");
        private static Regex historyProfileRegex = new Regex(@"\!historyprofile (?'id'[0-9]{3,10})");

        private volatile bool userListInUse = false;
        List<SocketGuildUser> guildUsers = new List<SocketGuildUser>();
        List<string> guildUsersNames = new List<string>();

        private volatile bool banListBeingUpdated = false;
        List<Ban> banList = null;
        HashSet<string> banListSteamIds = null;
        HashSet<string> banListProfileIds = null;

        HashSet<string> notificationsSent = new HashSet<string>();

        static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {

            banList = Util.LoadBanList(Util.GetPathToFile());
            banListSteamIds = banList.Select(b => b.SteamId).ToHashSet();
            banListProfileIds = banList.Select(b => b.ProfileId).ToHashSet();

            var config = new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true
            };

            discordSocketClient = new DiscordSocketClient(config);

            discordSocketClient.Log += Log;
            await discordSocketClient.LoginAsync(Discord.TokenType.Bot, discordToken);
            await discordSocketClient.StartAsync();
            discordSocketClient.MessageReceived += MessageReceived;

            KeepUserListUpdated();
            MonitorAoeLobbies();


            await Task.Delay(-1);
        }

        private async Task KeepUserListUpdated()
        {
            while (true)
            {
                if (discordSocketClient.Guilds.Count < 1)
                {
                    await Task.Delay(2 * 1000);
                    continue;
                }

                while (userListInUse)
                {
                    await Task.Delay(100);
                }
                userListInUse = true;

                SocketGuild guild = discordSocketClient.Guilds.Where(g=>g.Name == serverName).First();
                
                await guild.DownloadUsersAsync();
                
                guildUsers.Clear();
                guildUsersNames.Clear();
                foreach (SocketGuildUser user in guild.Users)
                {
                    guildUsers.Add(user);
                    guildUsersNames.Add(user.Nickname ?? user.Username);
                }

                userListInUse = false;
                await Task.Delay(60 * 1000);
            }
        }

        private async Task MonitorAoeLobbies()
        {
            // lets wait while discord is ready
            while (discordSocketClient.Guilds.Count < 1)
            {
                await Task.Delay(2 * 1000);
                continue;
            }

            while (true)
            {

                await Task.Delay(25 * 1000);

                List<Lobby> lobbies = null;
                try
                {
                    lobbies = Fetcher.GetLobbies();
                } catch (Exception ex)
                {
                    Console.WriteLine("Error fetching lobbies: " + ex.Message + "\r\n" + ex.StackTrace);
                }

                if (lobbies == null || lobbies.Count < 1)
                {
                    continue;
                }

                List<Warning> warnings = new List<Warning>();

                while (userListInUse)
                {
                    await Task.Delay(100);
                }
                userListInUse = true;

                foreach (Lobby lobby in lobbies)
                {
                    if (lobby.Players == null)
                    {
                        continue;
                    }

                    
                    List<Player> bannedPlayersInTheLobby = new List<Player>();
                    List<Player> goodGuysInTheLobby = new List<Player>();

                    foreach (Player player in lobby.Players)
                    {
                        if ( (player.SteamId != null && banListSteamIds.Contains(player.SteamId)) ||
                            (player.ProfileId != null && banListProfileIds.Contains(player.ProfileId)) )
                        {
                            bannedPlayersInTheLobby.Add(player);
                        }

                        if (player.Name != null && guildUsersNames.Contains(player.Name))
                        {
                            goodGuysInTheLobby.Add(player);
                        }

                    }

                    if (bannedPlayersInTheLobby.Count > 0 && goodGuysInTheLobby.Count > 0)
                    {
                        Warning warning = new Warning
                        {
                            Lobby = lobby,
                            BadPlayers = bannedPlayersInTheLobby,
                            GoodPlayers = goodGuysInTheLobby
                        };
                        warnings.Add(warning);
                    }
                    
                }

                userListInUse = false;

                SocketGuildChannel sgc = discordSocketClient.Guilds.Where(g => g.Name == serverName).First().Channels.Where(c => c.Name == notificationsChannelName || c.Name == "#" + notificationsChannelName).First();
                var chnl = discordSocketClient.GetChannel(sgc.Id) as ISocketMessageChannel;
                foreach (Warning warning in warnings)
                {
                    String warningHash = warning.WarningHash();
                    if (notificationsSent.Contains(warningHash))
                    {
                        continue;
                    }

                    await chnl.SendMessageAsync(warning.ToMessage(guildUsers, banList));
                    notificationsSent.Add(warningHash);
                }
            }

        }

        private Task Log(Discord.LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceived(SocketMessage message)
        {
            try
            {
                await MessageReceivedHandler(message);
            } catch (Exception ex)
            {
                await message.Channel.SendMessageAsync("Error processing command: " + ex.Message);
                Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        private async Task MessageReceivedHandler(SocketMessage message)
        {
            string msg = message.Content;

            if (message.Author == null)
            {
                return;
            }

            if (message.Channel == null)
            {
                return;
            }

            if (message.Author.Id == discordSocketClient.CurrentUser.Id || message.Author.IsBot) return;
            if (message.Channel.Name != channelName && message.Channel.Name == "#" + channelName) return;
            if ((message.Channel as SocketGuildChannel).Guild.Name != serverName) return;
            
            if (msg.StartsWith("!ping", true, null))
            {
                await message.Channel.SendMessageAsync("pong!");
            }


            if (msg.StartsWith("!history", true, null))
            {
                // TODO
                // list players from last 5 lobbies the player played at

                Player targetPlayer = null;

                // !history
                if (Regex.IsMatch(msg, @"\!history\s*"))
                {
                    string userName = (message.Author as SocketGuildUser).Nickname;
                    if (string.IsNullOrWhiteSpace(userName))
                    {
                        userName = message.Author.Username;
                    }

                    List<Player> potentialPlayers = Fetcher.FindPlayersByName(userName);

                    if (potentialPlayers == null || potentialPlayers.Count < 1)
                    {
                        await message.Channel.SendMessageAsync(message.Author.Mention + " I can't find you (" + userName + ") among AOE players. Is your discord nickname same as your in-game nickname?");
                        return;
                    }

                    if (potentialPlayers.Count > 1)
                    {
                        await message.Channel.SendMessageAsync(message.Author.Mention + " There is a number of nicknames identical to yours (" + userName + "), which one are you? Use !history <steam_id> to specify." + 
                            ClassesToTextTransformers.PlayersToLookupCandidates(potentialPlayers));
                        return;
                    }

                    targetPlayer = potentialPlayers[0];
                }

                // !history <nickname>
                Match historyNicknameMatch = historyNicknameRegex.Match(msg);
                if (historyNicknameMatch.Success)
                {
                    string nickName = historyNicknameMatch.Groups["nick"].Value;
                    List<Player> potentialPlayers = Fetcher.FindPlayersByName(nickName);

                    if (potentialPlayers == null || potentialPlayers.Count < 1)
                    {
                        await message.Channel.SendMessageAsync("I can't find " + nickName + " among AOE players.");
                        return;
                    }

                    if (potentialPlayers.Count > 1)
                    {
                        await message.Channel.SendMessageAsync("There is a number of nicknames identical to the one you provided (" + nickName + "). Use !historysteam <steam_id> or !historyprofile <aoe2.net profile_id> to specify:\r\n" +
                            ClassesToTextTransformers.PlayersToLookupCandidates(potentialPlayers));
                        return;
                    }

                    targetPlayer = potentialPlayers[0];
                }

                // !historysteam <steam_id>
                Match historySteamMatch = historySteamRegex.Match(msg);
                if (historySteamMatch.Success)
                {
                    string steamId = historySteamMatch.Groups["id"].Value;
                    targetPlayer = Fetcher.FindPlayerById(steamId, null);

                    if (targetPlayer == null)
                    {
                        await message.Channel.SendMessageAsync("I can't find player with steam ID " + steamId + " among AOE players.");
                        return;
                    }
                }

                // !historyprofile <profile_id>
                Match historyProfileMatch = historyProfileRegex.Match(msg);
                if (historyProfileMatch.Success)
                {
                    string profileId = historyProfileMatch.Groups["id"].Value;
                    targetPlayer = Fetcher.FindPlayerById(null, profileId);

                    if (targetPlayer == null)
                    {
                        await message.Channel.SendMessageAsync("I can't find player with profile ID " + profileId + " among AOE players.");
                        return;
                    }
                }

                if (targetPlayer == null)
                {
                    await message.Channel.SendMessageAsync("No target player specified.\r\n" + HelpMessages.playedWithMessage);
                    return;
                }

                List<Lobby> history = Fetcher.GetPlayersMatchHistory(targetPlayer.ProfileId);
                if (history == null || history.Count < 1)
                {
                    await message.Channel.SendMessageAsync("I can't find history for player " + targetPlayer.Name + "(aoe2.net Profile ID " + targetPlayer.ProfileId + ")");
                    return;
                }

                await message.Channel.SendMessageAsync("Player " + targetPlayer.Name + " last played in these games: \r\n" + ClassesToTextTransformers.LobbiesToHistory(history));
                return;
            }

            if (msg.StartsWith("!bansteam ", true, null) || msg.StartsWith("!ban ", true, null))
            {
                // ban steam ID (!bansteam steamID reason)
                Match match = banCommandRegex.Match(msg);

                if (!match.Success)
                {
                    // TODO error messages and help
                    await message.Channel.SendMessageAsync(HelpMessages.banMessage);
                    return;
                }

                string idToBan = match.Groups["id"].Value;
                string reason = match.Groups["reason"].Value;
                string banAuthor = (message.Author as SocketGuildUser).Nickname;
                if (banAuthor == null)
                {
                    banAuthor = message.Author.Username;
                }

                Player toBan;
                string idType;

                if (msg.StartsWith("!bansteam ", true, null))
                {
                    toBan = Fetcher.FindPlayerById(idToBan, null);
                    idType = "steam";
                } else
                {
                    toBan = Fetcher.FindPlayerById(null, idToBan);
                    idType = "aoe2.net profile";
                }

                if (toBan == null)
                {
                    await message.Channel.SendMessageAsync("Error: player with " + idType + " ID " + idToBan + " not found!");
                    return;
                }

                Ban newBan = new Ban
                {
                    ProfileId = toBan.ProfileId,
                    SteamId = toBan.SteamId,
                    NickWhenBanned = toBan.Name,
                    BannedBy = banAuthor,
                    Reason = reason
                };

                await PerformBan(newBan);
                await message.Channel.SendMessageAsync(string.Format("Player with Profile ID {3}/Steam ID {0} ({1}) has been added to the ban list by {2}.", newBan.SteamId, newBan.NickWhenBanned, newBan.BannedBy, newBan.ProfileId));
            }

            if (msg.StartsWith("!lookup ", true, null))
            {
                // lookup profile and steam ID of the given nick (!lookup nick)
                Match match = lookupCommandRegex.Match(msg);
                if (match.Success)
                {
                    string nickToLookup = match.Groups["nick"].Value;

                    if (string.IsNullOrWhiteSpace(nickToLookup) || nickToLookup.Contains('\r') || nickToLookup.Contains('\n'))
                    {
                        await message.Channel.SendMessageAsync("Invalid nickname provided.\r\n" + HelpMessages.lookupMessage);
                        return;
                    }
                    
                    nickToLookup = nickToLookup.Trim();

                    List<Player> potentialMatches;
                    try
                    {
                        potentialMatches = Fetcher.FindPlayersByName(nickToLookup);
                    } catch (Exception ex)
                    {
                        await message.Channel.SendMessageAsync("Error looking up players: " + ex.Message);
                        return;
                    }

                    if (potentialMatches == null || potentialMatches.Count < 1)
                    {
                        await message.Channel.SendMessageAsync(HelpMessages.lookupNoResults + "\r\n" + HelpMessages.lookupMessage);
                        return;
                    }

                    await message.Channel.SendMessageAsync(ClassesToTextTransformers.PlayersToLookupCandidates(potentialMatches));
                    return;
                } else
                {
                    await message.Channel.SendMessageAsync("Invalid nickname provided.\r\n" + HelpMessages.lookupMessage);
                    return;
                }
            }

            if (msg.Equals("!export", StringComparison.OrdinalIgnoreCase))
            {
                if (banList == null || banList.Count < 1)
                {
                    await message.Channel.SendMessageAsync("Ban list is empty! Nothing to export.");
                    return;
                }

                string fullPathToExportedTmpFile = Util.SaveBanListToExcelTmp(banList);
                await message.Channel.SendFileAsync(fullPathToExportedTmpFile);
                return;
            }

            if (msg.Equals("!help", StringComparison.OrdinalIgnoreCase))
            {
                await message.Channel.SendMessageAsync(HelpMessages.mainHelp);
                return;
            }
        }

        private async Task PerformBan(Ban ban)
        {

            while (banListBeingUpdated)
            {
                await Task.Delay(50);
            }

            banListBeingUpdated = true;

            int nextId = 0;
            if (banList.Count > 0)
            {
                nextId = banList.Max(b => b.BanId) + 1;
            }

            ban.BanId = nextId;

            banList.Add(ban);
            Util.SaveBanList(banList, Util.GetPathToFile());
            banListSteamIds.Add(ban.SteamId);
            banListProfileIds.Add(ban.ProfileId);

            banListBeingUpdated = false;
        }



    }
}

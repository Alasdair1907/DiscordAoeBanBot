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
        private string discordToken;
        private string channelName; // channel where the bot will respond to commands
        private string notificationsChannelName; // channel where the bot will warn discord users
        private string serverName; // server name - bot will respond only to this server
        private List<string> unbanRoles; // list of role names, users of which are allowed to use !unban command

        private static string STEAM_ID_TYPE = "steam";
        private static string AOE_PROFILE_ID_TYPE = "aoe2.net profile";

        private static Regex lookupCommandRegex = new Regex(@"\!lookup (?'nick'.+)");
        private static Regex banCommandRegex = new Regex(@"\!(ban|bansteam) (?'id'[0-9]{3,20}) (?'reason'.+)");
        private static Regex unbanCommandRegex = new Regex(@"\!(unban|unbanprofile) (?'id'[0-9]{1,10})");
        private static Regex historyNicknameRegex = new Regex(@"\!history (?'nick'.+)");
        private static Regex historySteamRegex = new Regex(@"\!historysteam (?'id'[0-9]{3,20})");
        private static Regex historyProfileRegex = new Regex(@"\!historyprofile (?'id'[0-9]{3,10})");

        private object userListLock = new object();
        private object banListLock = new object ();

        List<DiscordUser> guildUsers = new List<DiscordUser>();
        List<string> guildUsersNames = new List<string>();

        List<Ban> banList = null;
        HashSet<string> banListSteamIds = null;
        HashSet<string> banListProfileIds = null;

        HashSet<string> notificationsSent = new HashSet<string>();

        static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {

            Settings settings = Util.LoadSettings();
            if (settings == null)
            {
                throw new Exception("Error loading settings.");
            }

            discordToken = settings.DiscordToken;
            channelName = settings.BansChannelName;
            notificationsChannelName = settings.NotificationsChannelName;
            serverName = settings.ServerName;
            unbanRoles = settings.UnbanRoles ?? new List<string>();

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

                SocketGuild guild = discordSocketClient.Guilds.Where(g => g.Name == serverName).First();
                await guild.DownloadUsersAsync();

                lock (userListLock)
                {
                    guildUsers.Clear();
                    guildUsersNames.Clear();
                    foreach (SocketGuildUser user in guild.Users)
                    {
                        var discordUser = new DiscordUser
                        {
                            Name = user.Nickname ?? user.Username,
                            Mention = user.Mention
                        };

                        guildUsers.Add(discordUser);
                        guildUsersNames.Add(discordUser.Name);
                    }
                }

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

                var warnings = new List<Warning>();
                var toSend = new List<string>();

                lock (userListLock)
                {
                    lock (banListLock)
                    {
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
                                if ((player.SteamId != null && banListSteamIds.Contains(player.SteamId)) ||
                                    (player.ProfileId != null && banListProfileIds.Contains(player.ProfileId)))
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

                        foreach (Warning warning in warnings)
                        {
                            string warningHash = warning.WarningHash();
                            if (notificationsSent.Contains(warningHash))
                            {
                                continue;
                            }

                            toSend.Add(warning.ToMessage(guildUsers, banList));
                            notificationsSent.Add(warningHash);
                        }
                    }
                }

                SocketGuildChannel sgc = discordSocketClient.Guilds.Where(g => g.Name == serverName).First().Channels.Where(c => c.Name == notificationsChannelName || c.Name == "#" + notificationsChannelName).First();
                var chnl = discordSocketClient.GetChannel(sgc.Id) as ISocketMessageChannel;
                foreach (var message in toSend)
                {
                    await chnl.SendMessageAsync(message);
                }

                toSend.Clear();
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
            if (message.Channel.Name != channelName) return;
            if ((message.Channel as SocketGuildChannel).Guild.Name != serverName) return;
            
            if (msg.StartsWith("!ping", true, null))
            {
                await message.Channel.SendMessageAsync("pong!");
            }

            if (msg.StartsWith("!history", true, null))
            {
                List<Lobby> history = null;
                string steamId = null;
                string profileId = null;

                // !history
                if (Regex.IsMatch(msg, @"\!history\s*$"))
                {
                    string userName = (message.Author as SocketGuildUser).Nickname ?? message.Author.Username;
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
                        await message.Channel.SendMessageAsync(message.Author.Mention + " There is a number of nicknames identical to yours (" + userName + "), which one are you? Use !historyprofile <aoe2.net profile id> or !historysteam <steam id> to specify:\r\n" + 
                            ClassesToTextTransformers.PlayersToLookupCandidates(potentialPlayers));
                        return;
                    }

                    profileId = potentialPlayers[0].ProfileId;
                    history = Fetcher.GetPlayersMatchHistory(profileId);
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
                        await message.Channel.SendMessageAsync("There is a number of nicknames identical to the one you provided (" + nickName + "). Use !historyprofile <aoe2.net profile_id> or !historysteam <steam_id> to specify:\r\n" +
                            ClassesToTextTransformers.PlayersToLookupCandidates(potentialPlayers));
                        return;
                    }

                    profileId = potentialPlayers[0].ProfileId;
                    history = Fetcher.GetPlayersMatchHistory(profileId);
                }

                // !historysteam <steam_id>
                Match historySteamMatch = historySteamRegex.Match(msg);
                if (historySteamMatch.Success)
                {
                    steamId = historySteamMatch.Groups["id"].Value;
                    history = Fetcher.GetPlayersMatchHistorySteamId(steamId);
                }

                // !historyprofile <profile_id>
                Match historyProfileMatch = historyProfileRegex.Match(msg);
                if (historyProfileMatch.Success)
                {
                    profileId = historyProfileMatch.Groups["id"].Value;
                    
                    try
                    {
                        int.Parse(profileId);
                    } catch (Exception ex)
                    {
                        await message.Channel.SendMessageAsync("Invalid profile ID: " + profileId);
                        return;
                    }

                    history = Fetcher.GetPlayersMatchHistory(profileId);
                }

                if (history == null)
                {
                    await message.Channel.SendMessageAsync("Player's history not found.\r\n" + HelpMessages.playedWithMessage);
                    return;
                }

                Player targetPlayer = null;
                if (profileId != null)
                {
                    targetPlayer = history.SelectMany(l => l.Players).Where(p => p.ProfileId == profileId).First();
                } else
                {
                    targetPlayer = history.SelectMany(l => l.Players).Where(p => p.SteamId == steamId).First();
                }

                await message.Channel.SendMessageAsync("Player " + targetPlayer.Name + " last played in these games: \r\n" + ClassesToTextTransformers.LobbiesToHistory(history));
                return;
            }

            if (msg.StartsWith("!bansteam", true, null) || msg.StartsWith("!ban", true, null))
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
                    idType = STEAM_ID_TYPE;
                } else
                {
                    toBan = Fetcher.FindPlayerById(null, idToBan);
                    idType = AOE_PROFILE_ID_TYPE;
                }

                if (toBan == null)
                {
                    // maybe the player isn't on the unranked ladder yet. Try to obtain
                    // the Player object from that player's match history

                    List<Lobby> playersLobbies = null;
                    if (idType == STEAM_ID_TYPE)
                    {
                        playersLobbies = Fetcher.GetPlayersMatchHistorySteamId(idToBan);
                    } else
                    {
                        playersLobbies = Fetcher.GetPlayersMatchHistory(idToBan);
                    }

                    if (playersLobbies == null || playersLobbies.Count < 1)
                    {
                        await message.Channel.SendMessageAsync("Error: player with " + idType + " ID " + idToBan + " not found!\r\nIf you're not sure how to use this, use !help command.");
                        return;
                    }

                    toBan = playersLobbies.SelectMany(l => l.Players).Where(p => (idType == STEAM_ID_TYPE) ? p.SteamId == idToBan : p.ProfileId == idToBan).First();

                    if (toBan == null)
                    {
                        await message.Channel.SendMessageAsync("Error: player with " + idType + " ID " + idToBan + " not found!\r\nIf you're not sure how to use this, use !help command.");
                        return;
                    }
                }

                Ban newBan = new Ban
                {
                    ProfileId = toBan.ProfileId,
                    SteamId = toBan.SteamId,
                    NickWhenBanned = toBan.Name,
                    BannedBy = banAuthor,
                    Reason = reason
                };

                PerformBan(newBan);
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
                        await message.Channel.SendMessageAsync(HelpMessages.lookupNoResults);
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

            if (msg.StartsWith("!unban", StringComparison.OrdinalIgnoreCase))
            {
                if (unbanRoles == null || unbanRoles.Count < 1)
                {
                    await message.Channel.SendMessageAsync(HelpMessages.unbanMessageNoRoles);
                    return;
                }

                Match m = unbanCommandRegex.Match(msg);
                if (!m.Success)
                {
                    await message.Channel.SendMessageAsync(HelpMessages.unbanMessagePartial + string.Join("; ", unbanRoles));
                    return;
                }

                int banId;
                try
                {
                    banId = int.Parse(m.Groups["id"].Value);
                } catch (Exception ex)
                {
                    await message.Channel.SendMessageAsync("Invalid ID for unbanning: " + m.Groups["id"].Value);
                    return;
                }

                var roles = (message.Author as SocketGuildUser).Roles;
                if (roles == null || !roles.Where(r => unbanRoles.Contains(r.Name.ToLower())).Any())
                {
                    await message.Channel.SendMessageAsync("Only users with following roles are allowed to unban: " + string.Join("; ", unbanRoles));
                    return;
                }

                if (msg.StartsWith("!unbanprofile"))
                {
                    List<Ban> unbanned = PerformProfileUnban(m.Groups["id"].Value);
                    if (unbanned != null && unbanned.Count > 0)
                    {
                        await message.Channel.SendMessageAsync(ClassesToTextTransformers.UnbannedListToMessage(unbanned));
                        return;
                    }
                } else
                {
                    Ban unbanned = PerformUnban(banId);
                    if (unbanned != null)
                    {
                        await message.Channel.SendMessageAsync(string.Format("Following ban has been removed:\r\nUser nickname when banned: {0}\r\nAOE2.NET profile ID: {1}\r\nReason for ban: {2}",
                            unbanned.NickWhenBanned, unbanned.ProfileId, unbanned.Reason));
                        return;
                    }
                }

                await message.Channel.SendMessageAsync("No bans have been removed.");
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

        private Ban PerformUnban(int banId)
        {
            Ban ban = null;
            lock (banListLock)
            {
                ban = banList.Where(b => b.BanId == banId).First();

                banList.Remove(ban);
                banListProfileIds.Remove(ban.ProfileId);
                banListSteamIds.Remove(ban.SteamId);

                Util.SaveBanList(banList, Util.GetPathToFile());
            }
            return ban;
        }

        private List<Ban> PerformProfileUnban(string profileId)
        {
            List<Ban> bans = null;
            lock (banListLock)
            {
                bans = banList.Where(b => b.ProfileId == profileId).ToList();

                foreach (var ban in bans)
                {
                    banList.Remove(ban);
                    banListSteamIds.Remove(ban.SteamId);
                    banListProfileIds.Remove(ban.ProfileId);
                }

                Util.SaveBanList(banList, Util.GetPathToFile());
            }

            return bans;
        }

        private void PerformBan(Ban ban)
        {
            lock (banListLock)
            {
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
            }
        }



    }
}

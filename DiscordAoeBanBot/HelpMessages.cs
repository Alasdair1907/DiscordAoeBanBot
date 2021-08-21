using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordAoeBanBot
{
    public class HelpMessages
    {
        public static string mainHelp = @"
I maintain the list of banned users.
I also keep monitoring aoe2.net lobbies, and whenever I spot a user from this discord server who is joined in a lobby by a banned player, I notify that user.

Following commands are available:
!ban <aoe2.net profile id> <reason> -- bans the user identified by their aoe2.net profile id
!bansteam <steam id> <reason> -- bans the user identified by their steam id
!lookup <nickname> -- searches for users on the unranked ladder, provides their aoe2.net profile ids and steam ids
!history -- returns the list of players the discord user has played with in the last 2 games (note: discord user's nick must be the same as in-game nickname)
!history <nickname> -- return the list of players the player identified by <nickname> has played with in the last 2 games
!history <steam id> -- same as above, but the user is identified by their steam id
!history <aoe2.net profile id> -- same as above, but the user is identified by their aoe2.net profile id
!help -- prints this message
!export -- exports the entire ban list into the excel spreadsheet and posts it into the discord channel


Examples:
To find aoe2.net profile ID of user named ALT+F4 use this command:

!lookup ALT+F4

In the result, you will find his steam ID (if he has a steam account), and his aoe2.net profile ID (which is 2666035).
Next, ban him by using the following command:

!ban 2666035 Thinks it's very funny to disconnect mid game

";

        public static string lookupMessage = @"
!lookup <nickname>
This command will attempt to lookup players by their nicknames, returning the list of potential matches, if any are found.
Example usage:
!lookup ALT+F4

For more information, use !help command.
";

        public static string lookupNoResults = @"
No results found for provided nickname.
Try using command !playedwith to obtain the list of last players you played with/against.

For more information, use !help command.
";
        public static string playedWithMessage = @"
!history <nickname>
!history <steam_id>
!history <aoe2.net profile id>
!history
This command will list last players the player specified with nickname or steamid played with.
If your discord name matches your in-game name exactly, you can call command !playedwith without any options.

For more information, use !help command.
";

        public static string banMessage = @"
!ban <aoe2.net profile_id> <reason>
!bansteam <steam_id> <reason>

Use one of these commands to ban a user.
You can find user's aoe2.net profile ID or steam ID by using following command:
!lookup <nickname>

You can also obtain the list of players (including their IDs) you or someone else last played with with one of the following commands:
!history
!history <nickname>

For more information, use !help command.
";
    }
}

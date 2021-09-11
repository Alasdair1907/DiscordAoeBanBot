# DiscordAoeBanBot
Bot for Age of Empires II DE Discord communities, that maintains list of unwanted players and notifies members when they are joined in game lobbies by the unwanted players.\
The bot accepts commands for finding players and banning them on the channel specified by bans_channel_name in the settings.\
The bot warns discord server users (whose names are exact matches to their in-game names) in the channel specified by notifications_channel_name in the settings.\
One instance of bot will work only with one server.\
\
Note:\
For this bot to work correctly, following settings must be enabled for the bot in the discord developer portal: (https://discord.com/developers/applications)
\
Presence intent\
Server members intent\
\
\
To start the bot it is enough to launch the executable.\
The settings file must be placed in the same directory as the executable, and must be called "discord_aoe_bans.settings".\
Example contents of the settings file:\
\
discord_token=AAAA-BBBBB-CCCCC-DDDDDD\
bans_channel_name=bans\
notifications_channel_name=game-warnings\
server_name=CommunityServer123
unban_roles=role name1;extended role;another privileged role

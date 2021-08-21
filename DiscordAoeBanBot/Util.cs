using Newtonsoft.Json;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiscordAoeBanBot
{
    public class Util
    {
        public static string banFileName = "discord_aoe_bans.json"; // stored in home folder
        public static string settingsFileName = "discord_aoe_bans.settings"; // looked for in the current directory

        public static Settings LoadSettings()
        {
            TextReader reader = null;

            try
            {
                if (!File.Exists(settingsFileName))
                {
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                reader = new StreamReader(settingsFileName, Encoding.UTF8);
                string contents = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(contents))
                {
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                var settingsLines = new List<string>(contents.Split('\n'));

                var settings = new Settings();
                foreach (var line in settingsLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var tokens = new List<string>(line.Trim().Split('='));
                    if (tokens.Count != 2)
                    {
                        Console.WriteLine("Error parsing settings line: " + line + "\r\n");
                        Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                        return null;
                    }
                    
                    switch (tokens[0])
                    {
                        case "discord_token":
                            settings.DiscordToken = tokens[1];
                            break;
                        case "bans_channel_name":
                            settings.BansChannelName = tokens[1];
                            break;
                        case "notifications_channel_name":
                            settings.NotificationsChannelName = tokens[1];
                            break;
                        case "server_name":
                            settings.ServerName = tokens[1];
                            break;
                        default:
                            Console.WriteLine("Error: unknown settings key: " + tokens[0]);
                            return null;
                    }
                }

                if (string.IsNullOrWhiteSpace(settings.DiscordToken))
                {
                    Console.WriteLine("Error: discord_token setting must be provided");
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(settings.BansChannelName))
                {
                    Console.WriteLine("Error: bans_channel_name setting must be provided");
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(settings.NotificationsChannelName))
                {
                    Console.WriteLine("Error: notifications_channel_name setting must be provided");
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(settings.ServerName))
                {
                    Console.WriteLine("Error: server_name setting must be provided");
                    Console.WriteLine(HelpMessages.settingsPartial + settingsFileName);
                    return null;
                }

                return settings;
            } catch (Exception ex)
            {
                Console.WriteLine("Error attempting to load settings file " + settingsFileName + ": " + ex.Message);
                return null;
            }
        }

        public static string GetPathToFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), banFileName);
        }

        public static List<Ban> LoadBanList(string pathToFile)
        {
            TextReader reader = null;

            try
            {
                if (!File.Exists(pathToFile))
                {
                    return new List<Ban>();
                }

                reader = new StreamReader(pathToFile, Encoding.UTF8);
                string contents = reader.ReadToEnd();
                
                if (string.IsNullOrWhiteSpace(contents))
                {
                    return new List<Ban>();
                }

                return JsonConvert.DeserializeObject<List<Ban>>(contents);
            } finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
        }

        public static void SaveBanList(List<Ban> banList, string pathToFile)
        {
            TextWriter writer = null;

            try
            {
                string contents = JsonConvert.SerializeObject(banList);
                writer = new StreamWriter(pathToFile, false, Encoding.UTF8);
                writer.Write(contents);
            } finally
            {
                if (writer != null)
                {
                    writer.Close();
                }
            }
        }
        
        public static string SaveBanListToExcelTmp(List<Ban> banList)
        {
            string fileNameTmp = Guid.NewGuid().ToString() + ".xlsx";
            string path = Path.GetTempPath();
            string fullPath = Path.Combine(path, fileNameTmp);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            DataTable table = (DataTable)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(banList), (typeof(DataTable)));

            FileInfo fileInfo = new FileInfo(fullPath);
            using (var excelPack = new ExcelPackage(fileInfo))
            {
                var ws = excelPack.Workbook.Worksheets.Add("Ban List");
                ws.Cells.LoadFromDataTable(table, true, OfficeOpenXml.Table.TableStyles.Light1);
                for (int i = 1; i <= 6; i++)
                {
                    ws.Column(i).Width = 25;
                }
                excelPack.Save();
            }

            return fullPath;
        }
        
        public static string SHA256Str(String input)
        {
            using SHA256 sha256Hash = SHA256.Create();
            byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            return sBuilder.ToString();
        }
    }
}

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
        public static string banFileName = "discord_aoe_bans.json";

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

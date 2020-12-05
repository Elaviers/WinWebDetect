using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace WinWebDetect
{
    class CookieReader
    {
        private static string browserDir = string.Empty;

        private static AesGCM256 aes;

        public enum CookieSource
        {
            NONE,
            CHROME,
            EDGE_CHROMIUM
        }

        public static void SetCookieSource(CookieSource source)
        {
            string localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (source == CookieSource.CHROME)
                browserDir = localAppdata + "\\Google\\Chrome\\User Data";
            else if (source == CookieSource.EDGE_CHROMIUM)
                browserDir = localAppdata + "\\Microsoft\\Edge\\User Data";
            else
            {
                browserDir = string.Empty;
                aes = null;
                return;
            }

            if (!System.IO.Directory.Exists(browserDir))
                throw new System.IO.DirectoryNotFoundException($"Browser data directory \"{browserDir}\" does not exist!");

            var json = new JsonTextReader(System.IO.File.OpenText(browserDir + "\\Local State"));
            while (json.Read())
            {
                if (json.TokenType == JsonToken.PropertyName && (string)json.Value == "encrypted_key")
                {
                    json.Read();

                    if (json.TokenType == JsonToken.String)
                    {
                        char[] b64encryptedKey = ((string)json.Value).ToCharArray();
                        byte[] encryptedKey = Convert.FromBase64CharArray(b64encryptedKey, 0, b64encryptedKey.Length);

                        //Strip the first 5 bytes ("DPAPI")
                        byte[] eKey = new byte[encryptedKey.Length - 5];
                        Array.Copy(encryptedKey, 5, eKey, 0, eKey.Length);

                        aes = new AesGCM256
                        {
                            key = ProtectedData.Unprotect(eKey, null, DataProtectionScope.LocalMachine)
                        };
                    }
                }
            }
        }

        private static void ReadSQLToCookieString(SqliteConnection sql, string name, ref string cookies)
        {
            using (var cmd = sql.CreateCommand())
            {
                cmd.CommandText = $"SELECT name, encrypted_value, path FROM cookies WHERE host_key LIKE '%{name}%';";

                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    byte[] nonce = new byte[12];

                    while (reader.Read())
                    {
                        if (reader.GetString(2) == "/") //Only bother with root path cookies for now, should suffice
                        {
                            byte[] encodedvalue = SQLReadBytes(reader, 1);
                            byte[] blob = new byte[encodedvalue.Length - (3 + nonce.Length)];

                            //VERSION(3 bytes) NONCE(12 bytes) BLOB(...) MAC(16 bytes)
                            Array.Copy(encodedvalue, 3, nonce, 0, nonce.Length);
                            Array.Copy(encodedvalue, 3 + nonce.Length, blob, 0, blob.Length);

                            if (cookies.Length != 0)
                                cookies += ';';

                            cookies += reader.GetString(0) + '=' + aes.Decode(blob, nonce, 128); //128 MAC bits
                        }
                    }
                }
            }
        }

        public static string GetCookieString(string url)
        {
            if (aes == null || browserDir.Length == 0)
                return string.Empty;

            var connectionbuilder = new SqliteConnectionStringBuilder
            {
                DataSource = browserDir + "\\Default\\Cookies",
                Mode = SqliteOpenMode.ReadOnly
            };

            string domain = Regex.Match(url, @"[^\.]+\.([^\/]+)").Groups[1].Captures[0].Value;
            string host = Regex.Match(url, @"[^\/]*[\/]*(.*\..*\.[^\/]*)").Groups[1].Captures[0].Value;

            string cookies = string.Empty;
            using (var sql = new SqliteConnection(connectionbuilder.ConnectionString))
            {
                sql.Open();
                    
                ReadSQLToCookieString(sql, host, ref cookies);
                ReadSQLToCookieString(sql, domain, ref cookies);
            }

            return cookies;
        }

        private static byte[] SQLReadBytes(SqliteDataReader reader, int ordinal)
        {
            byte[] buffer = new byte[128];
            long bytesRead;
            long dataOffset = 0;
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                while ((bytesRead = reader.GetBytes(ordinal, dataOffset, buffer, 0, buffer.Length)) > 0)
                {
                    stream.Write(buffer, 0, (int)bytesRead);
                    dataOffset += bytesRead;
                }
                return stream.ToArray();
            }
        }
    }
}

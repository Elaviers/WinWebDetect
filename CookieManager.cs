using System;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace WinWebDetect
{
    class CookieManager
    {
        private static CookieContainer _cookies;

        public static int CookieCount { get => _cookies.Count; }

        public enum CookieSource
        {
            NONE,
            AUTO, //Chrome else Edge else None
            CHROME,
            EDGE_CHROMIUM
        }

        //Returns null if cannot load cookies
        public static bool LoadCookiesFromSource(CookieSource source)
        {
            _cookies = null;

            string localAppdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string chromePath = localAppdata + "\\Google\\Chrome\\User Data";
            string edgePath = localAppdata + "\\Microsoft\\Edge\\User Data";
            if (source == CookieSource.AUTO)
            {
                Console.Write("(AUTO COOKIE FINDER): ");

                if (System.IO.Directory.Exists(chromePath)) source = CookieSource.CHROME;
                else if (System.IO.Directory.Exists(edgePath)) source = CookieSource.EDGE_CHROMIUM;
                else
                {
                    Console.WriteLine("No Chrome or Edge cookies found.");
                    return false;
                }

                Console.WriteLine($"Using cookies from {source}");
            }


            string browserDir;
            if (source == CookieSource.CHROME)
                browserDir = chromePath;
            else if (source == CookieSource.EDGE_CHROMIUM)
                browserDir = edgePath;
            else
                return false;

            if (!System.IO.Directory.Exists(browserDir))
            {
                Console.WriteLine($"Browser data directory for {source} ({browserDir}) does not exist!");
                return false;
            }

            AesGCM256 aes = null;

            //Init AES with decrypted key
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

                        break;
                    }
                }
            }

            if (aes != null)
            {
                //Grab cookies from SQL db and decrypt values
                string cookiesDir = System.IO.Directory.GetCurrentDirectory() + "\\lib";
                string cookiesPath = cookiesDir + "\\_cookies";
                System.IO.Directory.CreateDirectory(cookiesDir);

                System.IO.File.Copy(browserDir + "\\Default\\Cookies", cookiesPath, true);

                var connectionbuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = cookiesPath,
                    Mode = SqliteOpenMode.ReadOnly
                };

                using (var sql = new SqliteConnection(connectionbuilder.ConnectionString))
                {
                    sql.Open();

                    using (var cmd = sql.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT COUNT(*) FROM cookies;";

                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        if (count == 0)
                            return false;

                        if (count > 0)
                        {
                            cmd.CommandText = $"SELECT name, encrypted_value, path, host_key FROM cookies;";

                            using (SqliteDataReader reader = cmd.ExecuteReader())
                            {
                                int capacity = count + 512;
                                _cookies = new CookieContainer(capacity, capacity, 4096);
                                byte[] nonce = new byte[12];

                                while (reader.Read())
                                {
                                    byte[] encodedvalue = SQLReadBytes(reader, 1);
                                    byte[] blob = new byte[encodedvalue.Length - (3 + nonce.Length)];

                                    //VERSION(3 bytes) NONCE(12 bytes) BLOB(...) MAC(16 bytes)
                                    Array.Copy(encodedvalue, 3, nonce, 0, nonce.Length);
                                    Array.Copy(encodedvalue, 3 + nonce.Length, blob, 0, blob.Length);

                                    string decodedValue = aes.Decode(blob, nonce, 128);

                                    string name = reader.GetString(0);
                                    string path = reader.GetString(2);
                                    string host = reader.GetString(3);

                                    try
                                    {
                                        _cookies.Add(new Cookie(name, decodedValue, path, host));
                                    }
                                    catch (CookieException e)
                                    {
                                        Console.WriteLine($"Cookie ({host}, {path}, {name}) could not be loaded: \"{e.Message}\"");
                                    }
                                }

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
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

        public static string GetCookieHeader(Uri uri)
        {
            return _cookies.GetCookieHeader(uri);
        }

        public static void SetCookies(Uri uri, string cookieHeader)
        {
            _cookies.SetCookies(uri, cookieHeader);
        }
    }
}

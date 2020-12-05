using System;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace WinWebDetect
{
    class AesGCM256
    {
        public AesGCM256() { }
        public byte[] key;

        public string Decode(byte[] data, byte[] nonce, int macBits)
        {
            string sR = "";
            try
            {
                GcmBlockCipher cipher = new GcmBlockCipher(new AesEngine());

                AeadParameters parameters = new AeadParameters(new KeyParameter(key), macBits, nonce);

                cipher.Init(false, parameters);
                byte[] plainBytes = new byte[cipher.GetOutputSize(data.Length)];
                int retLen = cipher.ProcessBytes(data, 0, data.Length, plainBytes, 0);
                cipher.DoFinal(plainBytes, retLen);

                sR = System.Text.Encoding.UTF8.GetString(plainBytes).TrimEnd("\r\n\0".ToCharArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            return sR;
        }

    }
}

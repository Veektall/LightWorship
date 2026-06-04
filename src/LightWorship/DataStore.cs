using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace LightWorship
{
    public class DataStore
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };

        public string Root { get; private set; }
        public string BiblePath { get { return Path.Combine(Root, "Bibles", "kjv-verses-1769.json"); } }
        public string SongsPath { get { return Path.Combine(Root, "Songs", "songs.json"); } }
        public string MediaPath { get { return Path.Combine(Root, "Media", "media.json"); } }
        public string SchedulePath { get { return Path.Combine(Root, "Schedules", "current-service.json"); } }
        public string SettingsPath { get { return Path.Combine(Root, "settings.json"); } }

        public DataStore()
        {
            Root = FindDataRoot();
            Directory.CreateDirectory(Path.Combine(Root, "Bibles"));
            Directory.CreateDirectory(Path.Combine(Root, "Songs"));
            Directory.CreateDirectory(Path.Combine(Root, "Media"));
            Directory.CreateDirectory(Path.Combine(Root, "Schedules"));
        }

        public T Load<T>(string path, T fallback)
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            var json = File.ReadAllText(path);
            if (String.IsNullOrWhiteSpace(json))
            {
                return fallback;
            }

            return _serializer.Deserialize<T>(json);
        }

        public void Save<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, _serializer.Serialize(value));
        }

        public Dictionary<string, string> GetBibleFiles()
        {
            var folder = Path.Combine(Root, "Bibles");
            Directory.CreateDirectory(folder);
            var files = Directory.GetFiles(folder, "*.json");
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var key = name.IndexOf("kjv", StringComparison.OrdinalIgnoreCase) >= 0 ? "KJV" : name.ToUpperInvariant();
                if (!result.ContainsKey(key))
                {
                    result[key] = file;
                }
            }

            return result.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value, StringComparer.OrdinalIgnoreCase);
        }

        public string ImportBibleFile(string sourcePath, string abbreviation)
        {
            var safe = new string((abbreviation ?? "").Where(c => Char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (String.IsNullOrWhiteSpace(safe))
            {
                safe = Path.GetFileNameWithoutExtension(sourcePath);
            }

            var destination = Path.Combine(Root, "Bibles", safe.ToUpperInvariant() + ".json");
            File.Copy(sourcePath, destination, true);
            return destination;
        }

        private static string FindDataRoot()
        {
            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; current != null && i < 8; i++)
            {
                var candidate = Path.Combine(current.FullName, "data");
                if (File.Exists(Path.Combine(candidate, "Bibles", "kjv-verses-1769.json")))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LightWorship");
        }
    }
}

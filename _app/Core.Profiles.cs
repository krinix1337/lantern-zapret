using System;
using System.Collections.Generic;
using System.Linq;

namespace ZapretStudio
{
    // Профили конфигурации: стратегия + игровой режим + ipset + DoH.
    static partial class Core
    {
        public class Profile
        {
            public string Name;
            public string Strategy;
            public string GameMode;
            public bool Ipset;
            public int Doh;
        }

        public static List<Profile> GetProfiles()
        {
            var list = new List<Profile>();
            string raw = Get("profiles", "");
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var entry in raw.Split('|'))
            {
                if (entry.Length == 0) continue;
                var parts = entry.Split(';');
                if (parts.Length < 5) continue;
                list.Add(new Profile
                {
                    Name = parts[0],
                    Strategy = parts[1],
                    GameMode = parts[2],
                    Ipset = parts[3] == "1",
                    Doh = 0
                });
                int doh;
                if (parts.Length > 4 && int.TryParse(parts[4], out doh))
                    list[list.Count - 1].Doh = doh;
            }
            return list;
        }

        public static void SaveProfile(string name, string strategy, string gameMode, bool ipset, int doh)
        {
            var profiles = GetProfiles();
            profiles.RemoveAll(p => p.Name == name);
            profiles.Add(new Profile { Name = name, Strategy = strategy, GameMode = gameMode, Ipset = ipset, Doh = doh });
            WriteProfiles(profiles);
        }

        public static void DeleteProfile(string name)
        {
            var profiles = GetProfiles();
            profiles.RemoveAll(p => p.Name == name);
            WriteProfiles(profiles);
        }

        static void WriteProfiles(List<Profile> profiles)
        {
            var parts = profiles.Select(p =>
                p.Name + ";" + (p.Strategy ?? "") + ";" + (p.GameMode ?? "off") + ";" + (p.Ipset ? "1" : "0") + ";" + p.Doh);
            Set("profiles", string.Join("|", parts.ToArray()));
            SaveConfig();
        }
    }
}

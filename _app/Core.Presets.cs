using System;
using System.Collections.Generic;

namespace ZapretStudio
{
    static partial class Core
    {
        // Популярные сайты и сервисы — встроенный список для быстрой проверки.
        public static List<Target> PopularTargets()
        {
            var l = new List<Target>();
            // Discord
            Add(l, "Discord",         "Discord",     "https://discord.com", "HTTP");
            Add(l, "Discord CDN",     "Discord",     "https://cdn.discordapp.com", "HTTP");
            Add(l, "Discord Media",   "Discord",     "https://media.discordapp.net", "HTTP");
            Add(l, "Discord Gateway", "Discord",     "https://gateway.discord.gg", "HTTP");
            Add(l, "Discord Status",  "Discord",     "https://discordstatus.com", "HTTP");
            // YouTube / Google
            Add(l, "YouTube",         "YouTube",     "https://www.youtube.com", "HTTP");
            Add(l, "YouTube (m)",     "YouTube",     "https://m.youtube.com", "HTTP");
            Add(l, "YouTube images",  "YouTube",     "https://i.ytimg.com", "HTTP");
            Add(l, "YouTube Music",   "YouTube",     "https://music.youtube.com", "HTTP");
            Add(l, "Google",          "Google",      "https://www.google.com", "HTTP");
            Add(l, "Gmail",           "Google",      "https://mail.google.com", "HTTP");
            Add(l, "Google Play",     "Google",      "https://play.google.com", "HTTP");
            Add(l, "Google Drive",    "Google",      "https://drive.google.com", "HTTP");
            // Seti / DNS
            Add(l, "Cloudflare",      Loc.T("net.grp.networks"),        "https://www.cloudflare.com", "HTTP");
            Add(l, "Cloudflare DNS",  Loc.T("net.grp.networks"),        "PING:1.1.1.1", "PING");
            Add(l, "Google DNS",      Loc.T("net.grp.networks"),        "PING:8.8.8.8", "PING");
            Add(l, "Quad9 DNS",       Loc.T("net.grp.networks"),        "PING:9.9.9.9", "PING");
            Add(l, "AdGuard DNS",     Loc.T("net.grp.networks"),        "PING:94.140.14.14", "PING");
            // Media / soc. seti
            Add(l, "Twitch",          Loc.T("net.grp.media"),       "https://www.twitch.tv", "HTTP");
            Add(l, "X (Twitter)",     Loc.T("net.grp.media"),       "https://x.com", "HTTP");
            Add(l, "Instagram",       Loc.T("net.grp.media"),       "https://www.instagram.com", "HTTP");
            Add(l, "Facebook",        Loc.T("net.grp.media"),       "https://www.facebook.com", "HTTP");
            Add(l, "Spotify",         Loc.T("net.grp.media"),       "https://www.spotify.com", "HTTP");
            Add(l, "SoundCloud",      Loc.T("net.grp.media"),       "https://soundcloud.com", "HTTP");
            Add(l, "TikTok",          Loc.T("net.grp.media"),       "https://www.tiktok.com", "HTTP");
            Add(l, "Reddit",          Loc.T("net.grp.media"),       "https://www.reddit.com", "HTTP");
            Add(l, "Twitch video",    Loc.T("net.grp.media"),       "https://usher.ttvnw.net", "HTTP");
            // Messengery
            Add(l, "Telegram Web",    Loc.T("net.grp.msg"),  "https://web.telegram.org", "HTTP");
            Add(l, "Telegram API",    Loc.T("net.grp.msg"),  "https://api.telegram.org", "HTTP");
            Add(l, "WhatsApp",        Loc.T("net.grp.msg"),  "https://www.whatsapp.com", "HTTP");
            Add(l, "Signal",          Loc.T("net.grp.msg"),  "https://signal.org", "HTTP");
            Add(l, "Viber",           Loc.T("net.grp.msg"),  "https://www.viber.com", "HTTP");
            // Servisy / AI / razrabotka
            Add(l, "ChatGPT",         Loc.T("net.grp.services"),     "https://chatgpt.com", "HTTP");
            Add(l, "OpenAI",          Loc.T("net.grp.services"),     "https://api.openai.com", "HTTP");
            Add(l, "Claude",          Loc.T("net.grp.services"),     "https://claude.ai", "HTTP");
            Add(l, "GitHub",          Loc.T("net.grp.services"),     "https://github.com", "HTTP");
            Add(l, "GitHub raw",      Loc.T("net.grp.services"),     "https://raw.githubusercontent.com", "HTTP");
            Add(l, "Wikipedia",       Loc.T("net.grp.services"),     "https://www.wikipedia.org", "HTTP");
            Add(l, "Netflix",         Loc.T("net.grp.services"),     "https://www.netflix.com", "HTTP");
            return l;
        }

        // Игровые сервисы и платформы.
        public static List<Target> GameTargets()
        {
            var l = new List<Target>();
            // Steam
            Add(l, "Steam Store",      "Steam",     "https://store.steampowered.com", "HTTP");
            Add(l, "Steam Community",  "Steam",     "https://steamcommunity.com", "HTTP");
            Add(l, "Steam CDN",        "Steam",     "https://cdn.cloudflare.steamstatic.com", "HTTP");
            Add(l, "Steam API",        "Steam",     "https://api.steampowered.com", "HTTP");
            // Epic
            Add(l, "Epic Games Store", "Epic",      "https://store.epicgames.com", "HTTP");
            Add(l, "Epic Games API",   "Epic",      "https://www.epicgames.com", "HTTP");
            // Riot
            Add(l, "Riot Games",       "Riot",      "https://www.riotgames.com", "HTTP");
            Add(l, "Valorant",         "Riot",      "https://playvalorant.com", "HTTP");
            Add(l, "League of Legends","Riot",      "https://www.leagueoflegends.com", "HTTP");
            // Blizzard
            Add(l, "Battle.net",       "Blizzard",  "https://www.blizzard.com", "HTTP");
            Add(l, "Blizzard CDN",     "Blizzard",  "https://blzddist1-a.akamaihd.net", "HTTP");
            // Izdateli
            Add(l, "EA",               Loc.T("net.grp.publishers"),  "https://www.ea.com", "HTTP");
            Add(l, "EA Origin",        Loc.T("net.grp.publishers"),  "https://www.origin.com", "HTTP");
            Add(l, "Ubisoft Connect",  Loc.T("net.grp.publishers"),  "https://www.ubisoft.com", "HTTP");
            Add(l, "Rockstar Games",   Loc.T("net.grp.publishers"),  "https://www.rockstargames.com", "HTTP");
            Add(l, "Rockstar Social",  Loc.T("net.grp.publishers"),  "https://socialclub.rockstargames.com", "HTTP");
            Add(l, "GOG",              Loc.T("net.grp.publishers"),  "https://www.gog.com", "HTTP");
            Add(l, "GOG CDN",          Loc.T("net.grp.publishers"),  "https://gog-cdn-fastly.gog.com", "HTTP");
            // Konsoli
            Add(l, "PlayStation Network", Loc.T("net.grp.consoles"), "https://www.playstation.com", "HTTP");
            Add(l, "PSN Store",        Loc.T("net.grp.consoles"),   "https://store.playstation.com", "HTTP");
            Add(l, "Xbox Live",        Loc.T("net.grp.consoles"),   "https://www.xbox.com", "HTTP");
            Add(l, "Xbox Cloud",       Loc.T("net.grp.consoles"),   "https://xbox.com", "HTTP");
            Add(l, "Nintendo",         Loc.T("net.grp.consoles"),   "https://www.nintendo.com", "HTTP");
            // Aziatskie / prochee
            Add(l, "HoYoverse",        Loc.T("net.grp.other"),   "https://www.hoyoverse.com", "HTTP");
            Add(l, "Genshin Impact",   Loc.T("net.grp.other"),   "https://genshin.hoyoverse.com", "HTTP");
            Add(l, "Roblox",           Loc.T("net.grp.other"),   "https://www.roblox.com", "HTTP");
            Add(l, "Minecraft",        Loc.T("net.grp.other"),   "https://www.minecraft.net", "HTTP");
            Add(l, "Faceit",           Loc.T("net.grp.other"),   "https://www.faceit.com", "HTTP");
            return l;
        }

        static void Add(List<Target> l, string name, string group, string url, string kind)
        {
            var t = new Target { Key = name, Name = name, Group = group, Kind = kind, Url = url };
            if (kind == "PING") t.Host = url.Substring(5).Trim();
            else { try { t.Host = new Uri(url).Host; } catch { t.Host = url; } }
            l.Add(t);
        }
    }
}

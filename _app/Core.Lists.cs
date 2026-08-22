using System;
using System.IO;
using System.Net;

namespace ZapretStudio
{
    // Обновление списков с GitHub и получение changelog.
    static partial class Core
    {
        public const string IpsetServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        public const string HostsServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts";
        public const string ReleasesApiUrl = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";

        // Скачать и обновить ipset-all.txt.
        public static bool UpdateIpsetList(out string error)
        {
            error = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "ZapretStudio");
                    string content = wc.DownloadString(IpsetServiceUrl);
                    string dest = Path.Combine(Lists, "ipset-all.txt");
                    File.WriteAllText(dest, content);
                }
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        // Скачать hosts-файл (для справки, не заменяем системный автоматически).
        public static bool DownloadHostsFile(out string error, out string content)
        {
            error = null; content = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "ZapretStudio");
                    content = wc.DownloadString(HostsServiceUrl);
                }
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        // Получить changelog (release notes) последней версии.
        public static string FetchChangelog()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(ReleasesApiUrl);
                req.UserAgent = "ZapretStudio";
                req.Accept = "application/vnd.github.v3+json";
                req.Timeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rs = resp.GetResponseStream())
                using (var sr = new StreamReader(rs))
                {
                    string json = sr.ReadToEnd();
                    var m = System.Text.RegularExpressions.Regex.Match(json,
                        "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                        System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (!m.Success) return null;
                    string body = UnescapeJson(m.Groups[1].Value).Trim();
                    return body.Length > 0 ? body : null;
                }
            }
            catch { return null; }
        }

        static string UnescapeJson(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == 'r') { i++; continue; }
                    if (next == 't') { sb.Append('\t'); i++; continue; }
                    if (next == '"' || next == '\\' || next == '/') { sb.Append(next); i++; continue; }
                    if (next == 'u' && i + 5 < s.Length)
                    {
                        string hex = s.Substring(i + 2, 4);
                        int cp;
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out cp))
                        { sb.Append((char)cp); i += 5; continue; }
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}

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
                    // Простой парсинг "body": "..." без внешних зависимостей.
                    int idx = json.IndexOf("\"body\"", StringComparison.Ordinal);
                    if (idx < 0) return null;
                    int start = json.IndexOf('"', idx + 6);
                    if (start < 0) return null;
                    start++;
                    var sb = new System.Text.StringBuilder();
                    for (int i = start; i < json.Length; i++)
                    {
                        char c = json[i];
                        if (c == '\\' && i + 1 < json.Length)
                        {
                            char next = json[i + 1];
                            if (next == 'n') { sb.Append('\n'); i++; continue; }
                            if (next == 'r') { i++; continue; }
                            if (next == '"') { sb.Append('"'); i++; continue; }
                            if (next == '\\') { sb.Append('\\'); i++; continue; }
                            sb.Append(c); continue;
                        }
                        if (c == '"') break;
                        sb.Append(c);
                    }
                    string body = sb.ToString().Trim();
                    return body.Length > 0 ? body : null;
                }
            }
            catch { return null; }
        }
    }
}

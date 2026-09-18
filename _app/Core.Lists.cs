using System;
using System.IO;
using System.Net;

namespace ZapretStudio
{
    // Обновление списков с GitHub и получение changelog.
    static partial class Core
    {
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
                    string content = wc.DownloadString(Endpoints.IpsetServiceUrl);
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
                    content = wc.DownloadString(Endpoints.HostsServiceUrl);
                }
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        // Получить changelog (release notes) последней версии zapret.
        public static string FetchChangelog()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(Endpoints.ZapretReleaseApi);
                req.UserAgent = "ZapretStudio";
                req.Accept = "application/vnd.github.v3+json";
                req.Timeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rs = resp.GetResponseStream())
                using (var sr = new StreamReader(rs))
                {
                    return Endpoints.ReleaseNotes(sr.ReadToEnd());
                }
            }
            catch { return null; }
        }

        // Нормализация домена или IP-адреса для списков zapret:
        // 1. Очистка начальных и конечных пробелов.
        // 2. Для доменов — отсечение схем http(s):// и путей /url, приведение к нижнему регистру.
        // 3. Конвертация кириллических / IDN доменов (например «яндекс.рф») в формат Punycode («xn--...»).
        // 4. Для списков IP (ipset) — сохранение IP-адресов и CIDR-масок (например 1.2.3.4/24).
        public static string NormalizeHostEntry(string entry, bool isIpSet)
        {
            if (string.IsNullOrEmpty(entry)) return "";
            string val = entry.Trim();
            if (val.StartsWith("#")) return val;

            if (isIpSet)
            {
                return val;
            }

            if (val.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) val = val.Substring(8);
            else if (val.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) val = val.Substring(7);
            int slash = val.IndexOf('/');
            if (slash >= 0) val = val.Substring(0, slash);
            val = val.Trim().TrimEnd('.');

            try
            {
                var idn = new System.Globalization.IdnMapping();
                val = idn.GetAscii(val);
            }
            catch { }

            return val.ToLowerInvariant();
        }
    }
}

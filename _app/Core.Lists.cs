using System;
using System.IO;
using System.Net;

namespace ZapretStudio
{
    // Обновление списков с GitHub и получение changelog.
    static partial class Core
    {
        // Параллельные нажатия «Обновить списки» писали в один и тот же файл,
        // а catch { } в View.Filters глотал IOException, статус висел вечно.
        static readonly object _ipsetUpdateLock = new object();

        // Адресная строка ipset: IPv4 или IPv6, с маской или без.
        // Маску для IPv6 нельзя ограничивать /128 — в рабочем списке есть
        // записи вида 2001:428:6400::/48 и 2a00:1450:4000::/36.
        static readonly System.Text.RegularExpressions.Regex IpLineRe =
            new System.Text.RegularExpressions.Regex(
                @"^([0-9]{1,3}(\.[0-9]{1,3}){3}|[0-9A-Fa-f:]*:[0-9A-Fa-f:]*)(/[0-9]{1,3})?$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Что изменилось в списке после загрузки — без этих цифр «Обновление
        // списков…» выглядело как ничего не делающая кнопка.
        public sealed class IpsetUpdateStat
        {
            public int Before;
            public int After;
            public int Added;
            public int Removed;
            public bool Changed { get { return Added != 0 || Removed != 0; } }
        }

        // Скачать и обновить ipset-all.txt.
        public static bool UpdateIpsetList(out string error)
        {
            IpsetUpdateStat s;
            return UpdateIpsetList(out error, out s);
        }

        public static bool UpdateIpsetList(out string error, out IpsetUpdateStat stat)
        {
            error = null; stat = null;
            lock (_ipsetUpdateLock)
            {
                try
                {
                    string content;
                    using (var wc = new TimedWebClient { TimeoutMilliseconds = 60000 })
                    {
                        wc.Encoding = System.Text.Encoding.UTF8;
                        wc.Headers.Add("User-Agent", "ZapretStudio");
                        content = wc.DownloadString(Endpoints.IpsetServiceUrl);
                    }
                    // Приложение работает в сети с DPI-инъекциями, и каптивный
                    // портал отвечает «200 OK» собственной HTML-страницей. Без проверки
                    // это тело уезжало в рабочий список, после чего winws поднимался
                    // с мусором, а интерфейс рапортовал «списки обновлены (N)».
                    int bad;
                    if (!LooksLikeIpsetList(content, out bad))
                    {
                        error = string.Format(Loc.T("lists.rejected"), bad);
                        Warn(error);
                        return false;
                    }
                    string dest = Path.Combine(Lists, "ipset-all.txt");
                    var before = ReadAddressSet(dest);
                    // Запись через temp+replace: прежняя версия остаётся в
                    // ipset-all.txt.bak, и обрыв на середине не оставляет пустой файл.
                    WriteFileAtomic(dest, content);
                    stat = DiffAddresses(before, content);
                    // Копия от режима none/any устарела. Если её не убрать,
                    // цепочка «none → обновить списки → any → loaded» вернёт
                    // прежнюю пачку IP вместо свежей.
                    try
                    {
                        string bak = dest + ".backup";
                        if (File.Exists(bak)) File.Delete(bak);
                    }
                    catch { }
                    return true;
                }
                catch (Exception ex) { error = Short(ex.Message); return false; }
            }
        }

        static System.Collections.Generic.HashSet<string> ReadAddressSet(string path)
        {
            var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return set;
                foreach (var raw in File.ReadLines(path))
                {
                    string s = raw.Trim();
                    if (s.Length > 0 && s[0] != '#') set.Add(s);
                }
            }
            catch { }
            return set;
        }

        static IpsetUpdateStat DiffAddresses(System.Collections.Generic.HashSet<string> before, string newContent)
        {
            var now = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in newContent.Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length > 0 && s[0] != '#') now.Add(s);
            }
            var st = new IpsetUpdateStat { Before = before.Count, After = now.Count };
            foreach (var a in now) if (!before.Contains(a)) st.Added++;
            foreach (var a in before) if (!now.Contains(a)) st.Removed++;
            return st;
        }

        // Список обязан быть большим и состоять из адресов. Порог в 10% мусора
        // допускает комментарии и редкие служебные строки, но не HTML каптивного портала.
        static bool LooksLikeIpsetList(string content, out int badLines)
        {
            badLines = 0;
            if (string.IsNullOrEmpty(content) || content.Length <= 1024) return false;
            int good = 0;
            foreach (var raw in content.Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0 || s[0] == '#') continue;
                if (IpLineRe.IsMatch(s)) good++;
                else badLines++;
            }
            if (good == 0) return false;
            return badLines * 10 <= good;
        }

        // Скачать hosts-файл (для справки, не заменяем системный автоматически).
        public static bool DownloadHostsFile(out string error, out string content)
        {
            error = null; content = null;
            try
            {
                using (var wc = new TimedWebClient { TimeoutMilliseconds = 30000 })
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
                var req = (HttpWebRequest)WebRequest.Create(Endpoints.ZapretReleaseApi);
                req.UserAgent = "ZapretStudio";
                req.Accept = "application/vnd.github.v3+json";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
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

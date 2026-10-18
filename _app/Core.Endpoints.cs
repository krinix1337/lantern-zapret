using System;
using System.Collections.Generic;
using System.Text;

namespace ZapretStudio
{
    // Единственный источник всех внешних ссылок и JSON-хелперов.
    // Раньше URL были размазаны по Core.Diag/Core.TgProxy/Core.Lists/Core.Download,
    // а разбор JSON-строк дублировался в двух реализациях.
    static class Endpoints
    {
        // ---- Lantern (это приложение) ----
        public const string AppRepo = "https://github.com/krinix1337/lantern-zapret";
        public const string AppReleaseApi = "https://api.github.com/repos/krinix1337/lantern-zapret/releases/latest";
        public const string AppReleaseUrl = "https://github.com/krinix1337/lantern-zapret/releases/latest";
        // Имена ассетов релиза и прямая ссылка «последний релиз» на случай, если
        // API GitHub недоступен (лимит запросов, блокировка api.github.com).
        public const string AppInstallerAsset = "Lantern-Setup.exe";
        public const string AppInstallerSha256Asset = "Lantern-Setup.exe.sha256";
        public const string AppInstallerLatestUrl = "https://github.com/krinix1337/lantern-zapret/releases/latest/download/Lantern-Setup.exe";
        // История релизов (несколько последних) для кнопки «Что обновлялось».
        public const string AppReleasesApi = "https://api.github.com/repos/krinix1337/lantern-zapret/releases?per_page=6";
        public const string AppReleasesPage = "https://github.com/krinix1337/lantern-zapret/releases";

        // ---- zapret (движок, Flowseal-сборка) ----
        public const string ZapretRepo = "https://github.com/Flowseal/zapret-discord-youtube";
        public const string ZapretReleaseApi = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";
        public const string ZapretReleaseUrl = "https://github.com/Flowseal/zapret-discord-youtube/releases/latest";
        public const string ZapretVersionUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/version.txt";
        public const string ZapretReleasesApi = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases?per_page=6";
        public const string ZapretReleasesPage = "https://github.com/Flowseal/zapret-discord-youtube/releases";

        // ---- Списки (ipset / hosts) ----
        public const string IpsetServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        public const string HostsServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts";

        // ---- TG-WS-Proxy (Flowseal) ----
        public const string TgProxyRepo = "https://github.com/Flowseal/tg-ws-proxy";
        public const string TgProxyReleaseApi = "https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/latest";
        public const string TgProxyReleasePage = "https://github.com/Flowseal/tg-ws-proxy/releases/latest";
        public const string TgProxyReleasesApi = "https://api.github.com/repos/Flowseal/tg-ws-proxy/releases?per_page=6";
        public const string TgProxyReleasesAll = "https://github.com/Flowseal/tg-ws-proxy/releases";

        // ---- Прочие ссылки «О проекте» ----
        public const string EngineRepo = "https://github.com/bol-van/zapret";
        public const string WinDivertRepo = "https://github.com/basil00/Divert";

        // ---- Определение провайдера (ISP) ----
        // Оба источника — по HTTPS: раньше вторым шёл plaintext http://ip-api.com/json.
        public const string IspPrimaryUrl = "https://ipwho.is/";
        public const string IspFallbackUrl = "https://ipinfo.io/json";

        // ---------- JSON ----------
        // Пары «имя ассета → ссылка на скачивание» из ответа GitHub Releases API.
        //
        // Регулярками это не решается, и обе прежние попытки были нерабочими:
        //   * "\{([^{}]+)\}" не описывает объект ассета — внутри него вложенный
        //     объект "uploader", поэтому совпадали только его URL-шаблоны;
        //   * "name" ... "browser_download_url" через ленивый [\s\S]*? цепляет
        //     первым делом ЗАГОЛОВОК релиза (это тоже поле "name") и по пути
        //     съедает настоящее имя файла.
        // Поэтому идём по массиву "assets" со счётчиком скобок, собираем только
        // поля верхнего уровня каждого элемента (вложенный "uploader" пропускаем)
        // и уже в них ищем "name" и "browser_download_url".
        public static List<KeyValuePair<string, string>> ReleaseAssets(string json)
        {
            var result = new List<KeyValuePair<string, string>>();
            foreach (string body in ReleaseAssetBodies(json))
            {
                string name = JsonField(body, "name");
                string url = JsonField(body, "browser_download_url");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(url))
                    result.Add(new KeyValuePair<string, string>(name, url));
            }
            return result;
        }

        // ---------- История релизов ----------
        public sealed class ReleaseInfo
        {
            public string Tag;
            public string Date;    // published_at как прислал GitHub (ISO 8601 UTC)
            public string Notes;
        }

        // Ответ /releases?per_page=N — это МАССИВ объектов релизов. Поля берём
        // только с верхнего уровня каждого элемента: внутри релиза есть "assets" и
        // "author" со своими "name", и поиск по всему объекту цепляет их вместо
        // настоящего тега.
        public static List<ReleaseInfo> Releases(string json)
        {
            var result = new List<ReleaseInfo>();
            if (string.IsNullOrEmpty(json)) return result;
            int open = -1;
            for (int i = 0; i < json.Length; i++)
            {
                if (char.IsWhiteSpace(json[i])) continue;
                if (json[i] == '[') open = i;
                break;
            }
            if (open < 0) return result;
            foreach (string el in ObjectElements(json, open))
            {
                var r = new ReleaseInfo
                {
                    Tag = TopLevelField(el, "tag_name"),
                    Date = TopLevelField(el, "published_at"),
                    Notes = TopLevelField(el, "body")
                };
                if (!string.IsNullOrEmpty(r.Tag)) result.Add(r);
            }
            return result;
        }

        // «2026-09-30T12:34:56Z» → «30.09.2026» в местном времени. Пустая строка,
        // если дата не читается: лучше без даты, чем с «01.01.0001».
        public static string ReleaseDate(string isoUtc)
        {
            DateTime dt;
            if (!string.IsNullOrEmpty(isoUtc) &&
                DateTime.TryParse(isoUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal, out dt))
                return dt.ToLocalTime().ToString("dd.MM.yyyy");
            return "";
        }

        // Объекты верхнего уровня JSON-массива, начиная с его открывающей скобки.
        static List<string> ObjectElements(string json, int openBracket)
        {
            var list = new List<string>();
            int depth = 0, start = -1;
            for (int i = openBracket; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int e = SkipString(json, i);
                    if (e < 0) break;
                    i = e;
                    continue;
                }
                if (c == '{') { if (depth == 0) start = i; depth++; continue; }
                if (c == '}')
                {
                    depth--;
                    if (depth == 0 && start >= 0) { list.Add(json.Substring(start, i - start + 1)); start = -1; }
                }
            }
            return list;
        }

        // Значение свойства верхнего уровня объекта. Ключ ищется только там, где
        // глубина равна 1 (собственные свойства), вложенные объекты и массивы
        // перешагиваются целиком.
        static string TopLevelField(string obj, string key)
        {
            if (string.IsNullOrEmpty(obj)) return null;
            int depth = 0;
            for (int i = 0; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c == '"')
                {
                    int end = SkipString(obj, i);
                    if (end < 0) break;
                    if (depth == 1)
                    {
                        string name = obj.Substring(i + 1, end - i - 1);
                        if (name == key)
                        {
                            int j = end + 1;
                            while (j < obj.Length && char.IsWhiteSpace(obj[j])) j++;
                            if (j < obj.Length && obj[j] == ':') return ReadValue(obj, j + 1);
                        }
                    }
                    i = end;
                    continue;
                }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return null;
        }

        static string ReadValue(string obj, int from)
        {
            int k = from;
            while (k < obj.Length && char.IsWhiteSpace(obj[k])) k++;
            if (k >= obj.Length) return null;
            if (obj[k] == '"')
            {
                int end = SkipString(obj, k);
                if (end < 0 || end >= obj.Length) return null;
                return JsonUnescape(obj.Substring(k + 1, end - k - 1));
            }
            int e = k;
            while (e < obj.Length && obj[e] != ',' && obj[e] != '}' && obj[e] != ']') e++;
            return obj.Substring(k, e - k).Trim();
        }

        // Индекс закрывающей кавычки строки, начинающейся в quoteIdx, с учётом
        // экранирования; -1, если строка не закрыта.
        static int SkipString(string s, int quoteIdx)
        {
            for (int i = quoteIdx + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') { i++; continue; }
                if (c == '"') return i;
            }
            return -1;
        }

        // Тела элементов массива "assets" — только поля верхнего уровня каждого
        // ассета, вложенный "uploader" отброшен вместе со своими "name"/"login".
        static List<string> ReleaseAssetBodies(string json)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(json)) return result;
            int k = json.IndexOf("\"assets\"", StringComparison.Ordinal);
            if (k < 0) return result;
            int open = json.IndexOf('[', k);
            if (open < 0) return result;

            int depth = 0;                 // вложенность { } внутри массива assets
            bool inStr = false, esc = false;
            var elem = new StringBuilder();
            for (int i = open + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    if (depth == 1) elem.Append(c);
                    continue;
                }
                if (c == '"') { inStr = true; if (depth == 1) elem.Append(c); continue; }
                if (c == '{') { depth++; if (depth == 1) elem.Length = 0; continue; }
                if (c == '}')
                {
                    depth--;
                    if (depth == 0) { result.Add(elem.ToString()); elem.Length = 0; }
                    continue;
                }
                if (c == ']' && depth == 0) break;
                if (depth == 1) elem.Append(c);
            }
            return result;
        }

        // Ссылка на ассет релиза по имени файла (без учёта регистра) или null.
        public static string ReleaseAssetUrl(string json, string assetName)
        {
            foreach (var a in ReleaseAssets(json))
                if (string.Equals(a.Key, assetName, StringComparison.OrdinalIgnoreCase))
                    return a.Value;
            return null;
        }

        // SHA-256 ассета из поля "digest" ответа GitHub API ("sha256:<hex>") или null.
        // Это единственный источник контрольной суммы для чужих релизов: ни
        // tg-ws-proxy, ни zapret-discord-youtube не публикуют SHA256SUMS-ассет,
        // зато digest GitHub считает для каждого загруженного файла.
        public static string ReleaseAssetDigest(string json, string assetName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(assetName)) return null;
            foreach (string body in ReleaseAssetBodies(json))
            {
                if (!string.Equals(JsonField(body, "name"), assetName, StringComparison.OrdinalIgnoreCase)) continue;
                string digest = JsonField(body, "digest");
                if (string.IsNullOrEmpty(digest)) return null;
                int colon = digest.IndexOf(':');
                string algo = colon >= 0 ? digest.Substring(0, colon) : "";
                string hex = colon >= 0 ? digest.Substring(colon + 1) : digest;
                if (!algo.Equals("sha256", StringComparison.OrdinalIgnoreCase)) return null;
                return hex.Length == 64 ? hex : null;
            }
            return null;
        }

        // Достать значение строкового поля верхнего уровня из ответа GitHub API
        // без подключения сериализаторов: достаточно и надёжно для tag_name/body.
        public static string JsonField(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(json,
                "\"" + System.Text.RegularExpressions.Regex.Escape(field) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return m.Success ? JsonUnescape(m.Groups[1].Value) : null;
        }

        // Числовое поле верхнего уровня (config.json tg-ws-proxy хранит port числом,
        // а не строкой; JsonField для него не подходит).
        public static string JsonNumberField(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(json,
                "\"" + System.Text.RegularExpressions.Regex.Escape(field) + "\"\\s*:\\s*(-?\\d+)",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value : null;
        }

        // Разбор escape-последовательностей JSON-строки.
        public static string JsonUnescape(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length)
                {
                    char next = text[i + 1];
                    if (next == 'n') { sb.Append('\n'); i++; continue; }
                    if (next == 'r') { i++; continue; }
                    if (next == 't') { sb.Append('\t'); i++; continue; }
                    if (next == '"' || next == '\\' || next == '/') { sb.Append(next); i++; continue; }
                    if (next == 'u' && i + 5 < text.Length)
                    {
                        string hex = text.Substring(i + 2, 4);
                        int cp;
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out cp))
                        { sb.Append((char)cp); i += 5; continue; }
                    }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // GitHub API возвращает тело Release как JSON-строку. MessageBox не умеет
        // Markdown, поэтому превращаем текст в обычный читаемый список. Заодно
        // восстанавливаем старые заметки, где UTF-8 уже был ошибочно сохранён как
        // Windows-1252 (типичные символы "Ð" и "Ñ" на экране).
        public static string ReleaseNotes(string json)
        {
            string body = JsonField(json, "body");
            if (string.IsNullOrEmpty(body))
            {
                // Фолбэк: старая реализация через Regex.Unescape
                try
                {
                    var m = System.Text.RegularExpressions.Regex.Match(json ?? "",
                        "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", System.Text.RegularExpressions.RegexOptions.Singleline);
                    if (!m.Success) return null;
                    body = System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value);
                }
                catch { return null; }
            }
            body = RepairUtf8Mojibake(body).Trim();
            return body.Length > 0 ? FormatReleaseNotes(body) : null;
        }

        public static string RepairUtf8Mojibake(string text)
        {
            if (string.IsNullOrEmpty(text) || (text.IndexOf('Ð') < 0 && text.IndexOf('Ñ') < 0 && text.IndexOf('ð') < 0))
                return text;
            try
            {
                string fixedText = Encoding.UTF8.GetString(Encoding.GetEncoding(1252).GetBytes(text));
                // Применяем замену только когда результат действительно похож на русский
                // текст или содержит emoji. Так не портятся обычные западноевропейские буквы.
                foreach (char c in fixedText)
                    if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё' || char.IsSurrogate(c)) return fixedText;
            }
            catch { }
            return text;
        }

        public static string FormatReleaseNotes(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var result = new StringBuilder();
            int blank = 0;
            foreach (string raw in lines)
            {
                string line = CleanMarkdown(raw);
                if (line.Length == 0)
                {
                    // Пустых строк подряд — максимум одна: релизные заметки любят
                    // «\n\n\n» между секциями, и в карточке это превращается в дыру.
                    blank++;
                    if (blank > 1) continue;
                    result.AppendLine();
                    continue;
                }
                blank = 0;
                result.AppendLine(line);
            }
            return result.ToString().Trim();
        }

        // Markdown GitHub'а в читаемый текст: в TextBlock «**Fix** `bug`
        // [ссылка](http://…)» выглядело как мусор из звёздочек и скобок.
        // Жирный и код теряют маркеры, ссылки оставляют подпись, картинки и
        // HTML-теги (бейджи релизов) убираются целиком, списки получают «•».
        static readonly System.Text.RegularExpressions.Regex MdImage =
            new System.Text.RegularExpressions.Regex(@"!\[[^\]]*\]\([^)]*\)", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdLink =
            new System.Text.RegularExpressions.Regex(@"\[([^\]]*)\]\(([^)\s]+)[^)]*\)", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdAutoLink =
            new System.Text.RegularExpressions.Regex(@"<((?:https?|ftp|mailto)://[^>]+)>", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdBold =
            new System.Text.RegularExpressions.Regex(@"(\*\*|__)(.+?)\1", System.Text.RegularExpressions.RegexOptions.Compiled);
        // Курсив «*текст*». Подчёркивания не трогаем: в релизных заметках полно
        // имён вида fix_some_bug, и жадный шаблон съел бы их середину.
        static readonly System.Text.RegularExpressions.Regex MdItalic =
            new System.Text.RegularExpressions.Regex(@"(?<![\w*])\*([^*\r\n]+?)(?<![\s*])\*(?![\w*])", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdStrike =
            new System.Text.RegularExpressions.Regex(@"~~(.+?)~~", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdCode =
            new System.Text.RegularExpressions.Regex(@"`([^`]*)`", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex MdHtml =
            new System.Text.RegularExpressions.Regex(@"</?[a-zA-Z][^>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

        static string CleanMarkdown(string rawLine)
        {
            string line = (rawLine ?? "").TrimEnd();
            if (line.Length == 0) return "";

            int indent = line.Length - line.TrimStart().Length;
            string pad = indent > 4 ? line.Substring(0, 4) : "";   // вложенность списков — до 4 пробелов
            line = line.TrimStart();

            // Fence блокового кода: ``` без содержимого просто убираем.
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                string rest = line.Substring(3).Trim();
                if (rest.Length == 0) return "";
                line = rest;
            }
            // Горизонтальная линия «---.»
            if (line.Length >= 3 && line[0] == '-' && line.Trim('-').Length == 0) return "";
            if (line.Length >= 3 && line[0] == '*' && line.Trim('*', ' ').Length == 0) return "";

            // Цитата «> текст»
            while (line.StartsWith(">", StringComparison.Ordinal)) line = line.Substring(1).TrimStart();

            // Заголовок «## Текст»
            int hashes = 0;
            while (hashes < line.Length && line[hashes] == '#') hashes++;
            if (hashes > 0 && hashes < line.Length && (line[hashes] == ' ' || char.IsLetterOrDigit(line[hashes])))
                line = line.Substring(hashes).TrimStart();

            line = MdImage.Replace(line, "");
            line = MdAutoLink.Replace(line, "$1");
            line = MdLink.Replace(line, m =>
            {
                string text = m.Groups[1].Value.Trim();
                return text.Length > 0 ? text : m.Groups[2].Value;
            });
            line = MdBold.Replace(line, "$2");
            line = MdStrike.Replace(line, "$1");
            line = MdCode.Replace(line, "$1");
            line = MdHtml.Replace(line, "");
            line = MdItalic.Replace(line, "$1");

            // Маркеры списков — в нормальный «•» с сохранением вложенности.
            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal) ||
                line.StartsWith("+ ", StringComparison.Ordinal))
                line = "• " + line.Substring(2).TrimStart();

            return (pad + line).TrimEnd();
        }
    }
}

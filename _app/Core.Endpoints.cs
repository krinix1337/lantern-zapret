using System;
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

        // ---- zapret (движок, Flowseal-сборка) ----
        public const string ZapretRepo = "https://github.com/Flowseal/zapret-discord-youtube";
        public const string ZapretReleaseApi = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";
        public const string ZapretReleaseUrl = "https://github.com/Flowseal/zapret-discord-youtube/releases/latest";
        public const string ZapretVersionUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/version.txt";
        public const string ZapretZipUrl = "https://github.com/Flowseal/zapret-discord-youtube/archive/refs/heads/main.zip";

        // ---- Списки (ipset / hosts) ----
        public const string IpsetServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/ipset-service.txt";
        public const string HostsServiceUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/refs/heads/main/.service/hosts";

        // ---- TG-WS-Proxy (Flowseal) ----
        public const string TgProxyRepo = "https://github.com/Flowseal/tg-ws-proxy";
        public const string TgProxyReleaseApi = "https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/latest";
        public const string TgProxyReleasePage = "https://github.com/Flowseal/tg-ws-proxy/releases/latest";

        // ---- Прочие ссылки «О проекте» ----
        public const string EngineRepo = "https://github.com/bol-van/zapret";
        public const string WinDivertRepo = "https://github.com/basil00/Divert";

        // ---------- JSON ----------
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
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var result = new StringBuilder();
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                int hashes = 0;
                while (hashes < line.Length && line[hashes] == '#') hashes++;
                if (hashes > 0 && hashes < line.Length && line[hashes] == ' ')
                    line = line.Substring(hashes + 1).TrimStart();
                if (line.StartsWith("- ")) line = "• " + line.Substring(2);
                result.AppendLine(line);
            }
            return result.ToString().Trim();
        }
    }
}

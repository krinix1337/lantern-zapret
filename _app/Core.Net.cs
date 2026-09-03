using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace ZapretStudio
{
    // Модель одной цели проверки
    public class Target
    {
        public string Key;      // DiscordMain
        public string Name;     // человекочитаемое
        public string Group;    // Discord / YouTube / Google / Cloudflare / Public DNS
        public string Host;     // discord.com  или IP
        public string Kind;     // "HTTP"  или "PING"
        public string Url;      // исходное значение
    }

    // Результат проверки. Реальный, не подменяется.
    public class CheckResult
    {
        public string State;    // stable key: reachable/partial/unreachable/timeout/errDns/errTls/err (localize via net.<state>)
        public long   Ms = -1;  // задержка
        public string Detail;   // текст ошибки/пояснение
        public DateTime? When;
    }

    static partial class Core
    {
        // Разбор utils/targets.txt
        public static List<Target> LoadTargets()
        {
            var list = new List<Target>();
            if (!File.Exists(TargetsFile)) return list;
            string group = Loc.T("net.grp.other");
            string[] lines;
            try { lines = File.ReadAllLines(TargetsFile); } catch { return list; }
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("###")) { group = line.Substring(3).Trim(); continue; }
                if (line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim().Trim('"');
                if (key.Length == 0 || val.Length == 0) continue;

                var t = new Target { Key = key, Group = MapGroup(group), Url = val, Name = HumanName(key) };
                if (val.StartsWith("PING:", StringComparison.OrdinalIgnoreCase))
                {
                    t.Kind = "PING";
                    t.Host = val.Substring(5).Trim();
                }
                else
                {
                    t.Kind = "HTTP";
                    try { t.Host = new Uri(val).Host; } catch { t.Host = val; }
                }
                list.Add(t);
            }
            return list;
        }

        static string MapGroup(string g)
        {
            string s = g.ToLowerInvariant();
            if (s.Contains("discord")) return "Discord";
            if (s.Contains("youtube")) return "YouTube";
            if (s.Contains("google")) return "Google";
            if (s.Contains("cloudflare")) return "Cloudflare";
            if (s.Contains("dns")) return Loc.T("net.grp.dns");
            return g;
        }

        static string HumanName(string key)
        {
            // DiscordMain -> Discord Main; CloudflareDNS1111 -> Cloudflare DNS 1111
            string s = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            s = Regex.Replace(s, "([A-Za-z])([0-9])", "$1 $2");
            return s;
        }

        // ---- Реальные проверки ----

        // Ping. Возвращает результат по факту.
        public static CheckResult TestPing(string host, int timeoutMs)
        {
            var r = new CheckResult { When = DateTime.Now };
            try
            {
                using (var p = new Ping())
                {
                    var reply = p.Send(host, timeoutMs);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        r.State = "reachable"; r.Ms = reply.RoundtripTime;
                        r.Detail = Loc.T("net.d.icmpOk");
                    }
                    else if (reply != null && reply.Status == IPStatus.TimedOut)
                    { r.State = "timeout"; r.Detail = Loc.T("net.d.icmpNone"); }
                    else
                    { r.State = "unreachable"; r.Detail = reply == null ? Loc.T("net.d.noReply") : reply.Status.ToString(); }
                }
            }
            catch (Exception ex) { r.State = "err"; r.Detail = Short(ex.Message); }
            return r;
        }

        // Проверка доступности URL (HttpWebRequest с поддержкой VPN и браузерных заголовков + curl + прямой сокет)
        public static CheckResult TestHttp(string url, int timeoutMs)
        {
            var r = new CheckResult { When = DateTime.Now };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1. Быстрый HTTP-запрос через HttpWebRequest (поддерживает VPN-адаптеры, прокси и стандартный браузерный стек)
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /*Tls12*/ | (SecurityProtocolType)12288 /*Tls13*/ | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
                req.Accept = "*/*";
                req.AddRange(0, 1024);
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AllowAutoRedirect = false;
                req.KeepAlive = false;

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    sw.Stop();
                    r.Ms = sw.ElapsedMilliseconds;
                    int code = (int)resp.StatusCode;
                    r.Detail = "HTTP " + code + ", TLS OK";
                    r.State = (code >= 200 && code < 500) ? "reachable" : "partial";
                    return r;
                }
            }
            catch (WebException wex)
            {
                var errResp = wex.Response as HttpWebResponse;
                if (errResp != null)
                {
                    sw.Stop();
                    r.Ms = sw.ElapsedMilliseconds;
                    int code = (int)errResp.StatusCode;
                    try { errResp.Close(); } catch { }
                    // HTTP 3xx, 4xx (например 403 от Cloudflare/Discord Gateway или 404) означают успешное TCP/TLS соединение через DPI/VPN
                    if (code >= 200 && code < 500)
                    {
                        r.Detail = "HTTP " + code + ", TLS OK";
                        r.State = "reachable";
                        return r;
                    }
                }
            }
            catch { }

            // 2. Фолбэк на curl.exe (наиболее устойчив к DPI-десинхронизации и особенностям сокетов)
            try
            {
                int sec = Math.Max(3, timeoutMs / 1000);
                var cr = CurlCheck(url, sec);
                if (cr.Verdict == "ok")
                {
                    sw.Stop();
                    r.Ms = cr.Ms >= 0 ? cr.Ms : sw.ElapsedMilliseconds;
                    r.Detail = "HTTP " + (cr.BestCode ?? "200") + ", TLS OK";
                    r.State = "reachable";
                    return r;
                }
            }
            catch { }

            // 3. Если и curl не смог — замеряем детально стадии (DNS, TCP, TLS) для отображения точной причины
            string host; int port = 443; bool tls = true; string path = "/";
            try
            {
                var uri = new Uri(url);
                host = uri.Host; port = uri.Port;
                path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
                tls = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
            }
            catch { r.State = "err"; r.Detail = Loc.T("net.d.badUrl"); return r; }

            // DNS
            IPAddress[] addrs;
            try
            {
                var dns = Dns.BeginGetHostAddresses(host, null, null);
                if (!dns.AsyncWaitHandle.WaitOne(timeoutMs))
                { r.State = "timeout"; r.Detail = "DNS timeout"; return r; }
                addrs = Dns.EndGetHostAddresses(dns);
                if (addrs.Length == 0) throw new Exception("empty");
            }
            catch (Exception ex) { r.State = "errDns"; r.Detail = Short(ex.Message); return r; }

            TcpClient sock = null;
            System.Net.Security.SslStream ssl = null;
            try
            {
                var sortedAddrs = addrs.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).ToArray();
                bool connected = false;
                int perIpTimeout = Math.Min(timeoutMs, 2500);
                foreach (var ip in sortedAddrs)
                {
                    try
                    {
                        sock = new TcpClient();
                        var ar = sock.BeginConnect(ip, port, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(perIpTimeout))
                        {
                            sock.EndConnect(ar);
                            connected = true;
                            break;
                        }
                        else
                        {
                            try { sock.Client.Close(); } catch { }
                            try { sock.Close(); } catch { }
                        }
                    }
                    catch { try { sock.Close(); } catch { } }
                }
                if (!connected)
                {
                    r.State = "timeout"; r.Detail = Loc.T("net.d.tcpNone");
                    return r;
                }
                sock.ReceiveTimeout = timeoutMs;
                sock.SendTimeout = timeoutMs;

                Stream stream = sock.GetStream();
                if (tls)
                {
                    ssl = new System.Net.Security.SslStream(stream, false, delegate { return true; });
                    try
                    {
                        var protos = (System.Security.Authentication.SslProtocols)3072 /*Tls12*/ | (System.Security.Authentication.SslProtocols)12288 /*Tls13*/ | System.Security.Authentication.SslProtocols.Tls;
                        ssl.AuthenticateAsClient(host, null, protos, false);
                    }
                    catch (Exception ex) { r.State = "errTls"; r.Detail = Short(ex.Message); return r; }
                    stream = ssl;
                }

                string req = "GET " + path + " HTTP/1.1\r\nHost: " + host + "\r\nUser-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\r\nRange: bytes=0-1024\r\nConnection: close\r\n\r\n";
                byte[] rb = Encoding.ASCII.GetBytes(req);
                stream.Write(rb, 0, rb.Length); stream.Flush();
                var buf = new byte[256];
                int n = stream.Read(buf, 0, buf.Length);
                sw.Stop(); r.Ms = sw.ElapsedMilliseconds;
                string resp = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n));
                var m = Regex.Match(resp, @"HTTP/1\.[01]\s+(\d{3})");
                if (m.Success)
                {
                    int code = int.Parse(m.Groups[1].Value);
                    r.Detail = "HTTP " + code + (tls ? ", TLS OK" : "");
                    r.State = (code >= 200 && code < 500) ? "reachable" : "partial";
                }
                else if (n > 0) { r.State = "partial"; r.Detail = Loc.T("net.d.noStatus"); }
                else { r.State = "unreachable"; r.Detail = Loc.T("net.d.empty"); }
            }
            catch (Exception ex)
            {
                if (r.State == null) { r.State = "unreachable"; r.Detail = Short(ex.Message); }
            }
            finally { try { if (ssl != null) ssl.Dispose(); } catch { } try { sock.Close(); } catch { } }
            return r;
        }

        public static CheckResult TestTarget(Target t, int timeoutMs)
        {
            if (t.Kind == "PING") return TestPing(t.Host, timeoutMs);
            return TestHttp(t.Url, timeoutMs);
        }

        // Результат curl-проверки одного URL (3 протокола как в test zapret.ps1)
        public class CurlResult
        {
            public string Url;
            public int OkCount;       // сколько протоколов вернули HTTP 2xx-4xx
            public int Total = 3;
            public string BestCode;   // лучший HTTP-код
            public string Verdict;    // "ok" | "ssl" | "error"
            public string Detail;
            public long Ms;
        }

        // Проверка URL через curl.exe с тремя протоколами: HTTP/1.1, TLS1.2, TLS1.3.
        // Протоколы запускаются параллельно.
        public static CurlResult CurlCheck(string url, int timeoutSec)
        {
            var r = new CurlResult { Url = url, Verdict = "error", Ms = -1 };
            string[] protoFlags = { "-k --http1.1 --ssl-no-revoke", "-k --tlsv1.2 --tls-max 1.2 --ssl-no-revoke", "-k --tlsv1.3 --tls-max 1.3 --ssl-no-revoke" };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int[] exits = new int[protoFlags.Length];
            string[] outs = new string[protoFlags.Length];
            using (var done = new System.Threading.ManualResetEvent(false))
            {
                int remaining = protoFlags.Length;
                for (int i = 0; i < protoFlags.Length; i++)
                {
                    int idx = i;
                    // Без -I (иначе заголовки идут в stdout и ломают парсинг), запрашиваем 1 КБ через -r, пишем тело в NUL
                    string args = "-k -s -m " + timeoutSec + " -o NUL -r 0-1024 -w \"%{http_code}\" " + protoFlags[idx] + " \"" + url + "\"";
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            string output;
                            exits[idx] = RunCurl(args, out output);
                            outs[idx] = output;
                        }
                        catch { exits[idx] = -1; }
                        if (System.Threading.Interlocked.Decrement(ref remaining) == 0) done.Set();
                    });
                }
                done.WaitOne(timeoutSec * 1000 + 1500);
            }
            for (int i = 0; i < protoFlags.Length; i++)
            {
                string output = outs[i] ?? "";
                int httpCode = 0;
                var m = Regex.Match(output, @"\b([1-5]\d\d)\b");
                if (m.Success) int.TryParse(m.Groups[1].Value, out httpCode);

                if (exits[i] == 0 && httpCode >= 200 && httpCode < 500)
                {
                    r.OkCount++;
                    if (r.BestCode == null) r.BestCode = httpCode.ToString();
                }
                else if (exits[i] == 35 || exits[i] == 60 || (output.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (r.Verdict == "error") r.Verdict = "ssl";
                }
            }
            sw.Stop(); r.Ms = sw.ElapsedMilliseconds;
            if (r.OkCount > 0) { r.Verdict = "ok"; r.Detail = "HTTP " + r.BestCode + " (" + r.OkCount + "/3)"; }
            else if (r.Verdict == "ssl") r.Detail = "SSL/TLS error";
            else r.Detail = "Connection failed";
            return r;
        }

        static int RunCurl(string args, out string output)
        {
            output = "";
            try
            {
                string curlExe = "curl.exe";
                string localBinCurl = Path.Combine(Bin, "curl.exe");
                string sysCurl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
                if (File.Exists(localBinCurl)) curlExe = localBinCurl;
                else if (File.Exists(sysCurl)) curlExe = sysCurl;

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = curlExe, Arguments = args,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                var sb = new StringBuilder();
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.OutputDataReceived += delegate(object s, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.Append(e.Data); };
                    p.ErrorDataReceived += delegate(object s, System.Diagnostics.DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.Append(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } output = "timeout"; return -1; }
                    p.WaitForExit();
                    lock (sb) output = sb.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        // ---- ICMP Ping (для отображения задержки на странице проверки) ----
        public static long PingHost(string host, int timeoutMs)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(host, timeoutMs);
                    if (reply.Status == IPStatus.Success) return reply.RoundtripTime;
                }
            }
            catch { }
            return -1;
        }

        // ---- Определение провайдера (ISP) для рекомендации стратегии ----
        public class IspInfo
        {
            public string Name;
            public string Org;
            public string Asn;
            public string City;
            public override string ToString()
            {
                string s = !string.IsNullOrEmpty(Name) ? Name : Org ?? "Unknown";
                if (!string.IsNullOrEmpty(City)) s += " (" + City + ")";
                return s;
            }
        }

        public static IspInfo DetectIspInfo()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            // 1. Попытка через ipwho.is
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString("https://ipwho.is/");
                    if (json.Contains("\"success\":true") || json.Contains("\"ip\""))
                    {
                        var info = new IspInfo();
                        var m = Regex.Match(json, "\"isp\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.Name = m.Groups[1].Value;
                        m = Regex.Match(json, "\"org\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.Org = m.Groups[1].Value;
                        m = Regex.Match(json, "\"asn\"\\s*:\\s*(\\d+)");
                        if (m.Success) info.Asn = "AS" + m.Groups[1].Value;
                        m = Regex.Match(json, "\"city\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.City = m.Groups[1].Value;
                        if (!string.IsNullOrEmpty(info.Name) || !string.IsNullOrEmpty(info.Org))
                            return info;
                    }
                }
            }
            catch { }

            // 2. Фолбэк через ip-api.com
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString("http://ip-api.com/json");
                    if (json.Contains("\"status\":\"success\""))
                    {
                        var info = new IspInfo();
                        var m = Regex.Match(json, "\"isp\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.Name = m.Groups[1].Value;
                        m = Regex.Match(json, "\"org\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.Org = m.Groups[1].Value;
                        m = Regex.Match(json, "\"as\"\\s*:\\s*\"(AS\\d+)");
                        if (m.Success) info.Asn = m.Groups[1].Value;
                        m = Regex.Match(json, "\"city\"\\s*:\\s*\"([^\"]+)\"");
                        if (m.Success) info.City = m.Groups[1].Value;
                        return info;
                    }
                }
            }
            catch { }

            return null;
        }

        public static string DetectIsp()
        {
            var info = DetectIspInfo();
            return info != null ? info.ToString() : null;
        }

        public static List<string> GetCandidateStrategies(IspInfo info)
        {
            var all = GetStrategyFiles();
            if (all.Count == 0) return new List<string>();

            string text = (info != null ? (info.Name + " " + info.Org + " " + info.Asn).ToLowerInvariant() : "");
            var candidates = new List<string>();

            Action<string> addMatch = pattern =>
            {
                foreach (var f in all)
                {
                    if (f.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 && !candidates.Contains(f))
                        candidates.Add(f);
                }
            };

            // Профиль 1: Ростелеком / Tele2 / Т2 (AS12389, AS42697, AS15944)
            if (text.Contains("rostelecom") || text.Contains("rtk") || text.Contains("tele2") || text.Contains("t2") || text.Contains("as12389") || text.Contains("as15944"))
            {
                addMatch("general (FAKE TLS AUTO ALT).bat");
                addMatch("general (ALT5).bat");
                addMatch("general (ALT2).bat");
                addMatch("general (FAKE TLS AUTO).bat");
                addMatch("general (ALT).bat");
            }
            // Профиль 2: МТС / МГТС (AS8359, AS25513)
            else if (text.Contains("mts") || text.Contains("mobile telesystems") || text.Contains("mgts") || text.Contains("as8359"))
            {
                addMatch("general (FAKE TLS AUTO ALT).bat");
                addMatch("general (ALT).bat");
                addMatch("general (ALT3).bat");
                addMatch("general (FAKE TLS AUTO).bat");
            }
            // Профиль 3: Билайн / Вымпелком (AS3216)
            else if (text.Contains("beeline") || text.Contains("vimpelcom") || text.Contains("veon") || text.Contains("as3216"))
            {
                addMatch("general (ALT6).bat");
                addMatch("general (ALT).bat");
                addMatch("general (FAKE TLS AUTO ALT2).bat");
                addMatch("general (ALT11).bat");
            }
            // Профиль 4: Мегафон / Yota (AS31133, AS25159)
            else if (text.Contains("megafon") || text.Contains("yota") || text.Contains("as31133") || text.Contains("as25159"))
            {
                addMatch("general (FAKE TLS AUTO ALT2).bat");
                addMatch("general (FAKE TLS AUTO ALT).bat");
                addMatch("general (ALT).bat");
                addMatch("general (ALT4).bat");
            }
            // Профиль 5: Дом.ру / Эр-Телеком (AS42610)
            else if (text.Contains("er-telecom") || text.Contains("dom.ru") || text.Contains("er telecom") || text.Contains("as42610"))
            {
                addMatch("general (ALT).bat");
                addMatch("general (ALT3).bat");
                addMatch("general (FAKE TLS AUTO).bat");
                addMatch("general (ALT2).bat");
            }
            // Профиль 6: Уфанет (AS34563) / Таттелеком (AS34533)
            else if (text.Contains("ufanet") || text.Contains("tattel") || text.Contains("as34563") || text.Contains("as34533"))
            {
                addMatch("general (ALT).bat");
                addMatch("general (ALT5).bat");
                addMatch("general (FAKE TLS AUTO ALT).bat");
            }

            // Общие базовые стратегии для перебора
            addMatch("general (ALT).bat");
            addMatch("general (FAKE TLS AUTO ALT).bat");
            addMatch("general (ALT5).bat");
            addMatch("general (SIMPLE FAKE).bat");
            addMatch("general (ALT2).bat");
            addMatch("general (ALT6).bat");

            // Добавляем остальные
            foreach (var f in all)
                if (!candidates.Contains(f)) candidates.Add(f);

            return candidates;
        }

        public static string RecommendStrategy(string isp)
        {
            var info = new IspInfo { Name = isp };
            var list = GetCandidateStrategies(info);
            return list.Count > 0 ? list[0] : (GetStrategyFiles().Count > 0 ? GetStrategyFiles()[0] : null);
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Length <= 140) return s;
            int limit = 140;
            if (limit > 0 && char.IsHighSurrogate(s[limit - 1])) limit--;
            return s.Substring(0, limit) + "\u2026";
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

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

        // TCP + TLS + HTTP HEAD. Разделяем стадии, чтобы честно показать где сломалось.
        public static CheckResult TestHttp(string url, int timeoutMs)
        {
            var r = new CheckResult { When = DateTime.Now };
            string host; int port = 443; bool tls = true;
            try
            {
                var uri = new Uri(url);
                host = uri.Host; port = uri.Port;
                tls = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
            }
            catch { r.State = "err"; r.Detail = Loc.T("net.d.badUrl"); return r; }

            // DNS
            IPAddress[] addrs;
            try { addrs = Dns.GetHostAddresses(host); if (addrs.Length == 0) throw new Exception("empty"); }
            catch (Exception ex) { r.State = "errDns"; r.Detail = Short(ex.Message); return r; }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var sock = new TcpClient();
            System.Net.Security.SslStream ssl = null;
            try
            {
                var ar = sock.BeginConnect(host, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    r.State = "timeout"; r.Detail = Loc.T("net.d.tcpNone");
                    try { sock.Client.Close(); } catch { }
                    return r;
                }
                sock.EndConnect(ar);
                sock.ReceiveTimeout = timeoutMs;
                sock.SendTimeout = timeoutMs;

                Stream stream = sock.GetStream();
                if (tls)
                {
                    ssl = new System.Net.Security.SslStream(stream, false,
                        (s, cert, chain, err) => true); // проверяем доступность рукопожатия, не валидность цепочки
                    try { ssl.AuthenticateAsClient(host); }
                    catch (Exception ex) { r.State = "errTls"; r.Detail = Short(ex.Message); return r; }
                    stream = ssl;
                }

                // Минимальный HTTP HEAD
                string req = "HEAD / HTTP/1.1\r\nHost: " + host + "\r\nConnection: close\r\nUser-Agent: ZapretStudio\r\n\r\n";
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

        // Проверка URL через curl.exe с тремя протоколами: HTTP/1.1, TLS1.2, TLS1.3
        public static CurlResult CurlCheck(string url, int timeoutSec)
        {
            var r = new CurlResult { Url = url, Verdict = "error", Ms = -1 };
            string[] protoFlags = { "--http1.1", "--tlsv1.2 --tls-max 1.2", "--tlsv1.3 --tls-max 1.3" };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var flags in protoFlags)
            {
                string args = "-I -s -m " + timeoutSec + " -o NUL -w \"%{http_code}\" --show-error " + flags + " \"" + url + "\"";
                string output;
                int exit = RunCurl(args, out output);
                if (exit == 0)
                {
                    r.OkCount++;
                    if (r.BestCode == null) r.BestCode = output.Trim();
                }
                else if (exit == 35 || exit == 60 || (output != null && (output.IndexOf("certificate", StringComparison.OrdinalIgnoreCase) >= 0
                    || output.IndexOf("SSL", StringComparison.OrdinalIgnoreCase) >= 0)))
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
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "curl.exe", Arguments = args,
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
        public static string DetectIsp()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString("http://ip-api.com/json/?fields=isp,org,as");
                    var m = Regex.Match(json, "\"isp\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value;
                    m = Regex.Match(json, "\"org\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            return null;
        }

        // Рекомендация стратегии по провайдеру (эвристика).
        public static string RecommendStrategy(string isp)
        {
            if (string.IsNullOrEmpty(isp)) return null;
            string low = isp.ToLowerInvariant();
            var files = GetStrategyFiles();
            if (files.Count == 0) return null;
            // Ростелеком/МТС/Билайн — часто нужен FAKE TLS
            if (low.Contains("rostelecom") || low.Contains("mts") || low.Contains("beeline") || low.Contains("megafon"))
            {
                foreach (var f in files)
                    if (f.IndexOf("FAKE TLS AUTO", StringComparison.OrdinalIgnoreCase) >= 0) return f;
            }
            // По умолчанию — ALT стратегии
            foreach (var f in files)
                if (f.IndexOf("ALT", StringComparison.OrdinalIgnoreCase) >= 0) return f;
            return files[0];
        }

        static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 140 ? s.Substring(0, 140) + "…" : s;
        }
    }
}

using System;
using System.Net.NetworkInformation;

namespace ZapretStudio
{
    // Статистика сетевого трафика (общая по всем интерфейсам).
    static partial class Core
    {
        static long _prevSent, _prevRecv;
        static DateTime _prevTime;
        static bool _hasPrev;
        static readonly object _trafficLock = new object();

        public class TrafficSnapshot
        {
            public long TotalSent;
            public long TotalRecv;
            public double SpeedSent;   // байт/с
            public double SpeedRecv;   // байт/с
            public TimeSpan Uptime;
        }

        public static TrafficSnapshot GetTraffic()
        {
            long sent = 0, recv = 0;
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var st = ni.GetIPv4Statistics();
                    sent += st.BytesSent;
                    recv += st.BytesReceived;
                }
            }
            catch { }

            var now = DateTime.Now;
            double spSent = 0, spRecv = 0;
            lock (_trafficLock)
            {
                if (_hasPrev)
                {
                    double dt = (now - _prevTime).TotalSeconds;
                    if (dt > 0.5)
                    {
                        spSent = (sent - _prevSent) / dt;
                        spRecv = (recv - _prevRecv) / dt;
                        if (spSent < 0) spSent = 0;
                        if (spRecv < 0) spRecv = 0;
                    }
                }
                _prevSent = sent; _prevRecv = recv; _prevTime = now; _hasPrev = true;
            }

            var snap = new TrafficSnapshot
            {
                TotalSent = sent,
                TotalRecv = recv,
                SpeedSent = spSent,
                SpeedRecv = spRecv,
                Uptime = StartedAt.HasValue ? now - StartedAt.Value : TimeSpan.Zero
            };
            return snap;
        }
    }
}

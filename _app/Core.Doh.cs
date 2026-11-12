using System;
using Microsoft.Win32;

namespace ZapretStudio
{
    // Управление DNS-over-HTTPS через реестр Windows.
    //
    // Раньше писался только EnableAutoDOH=1 в параметры Dnscache. Это
    // недокументированный ключ инсайдерских сборок Windows 10, и включался он
    // значением 2, а не 1; в Windows 11 он не действует вовсе. Теперь пишется
    // политика DoHPolicy (то же, что «Настроить разрешение имён DNS через HTTPS»
    // в gpedit): 2 = разрешить DoH, 3 = требовать. EnableAutoDOH=2 оставлен для
    // Windows 10. DoH применяется только к DNS-серверам из встроенного списка
    // Windows (1.1.1.1, 8.8.8.8, 9.9.9.9 и т. п.).
    static partial class Core
    {
        const string DohRegPath = @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
        const string DohRegKey = "EnableAutoDOH";
        const string DohPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";
        const string DohPolicyKey = "DoHPolicy";

        static int ReadDword(string path, string name)
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (k == null) return 0;
                    object v = k.GetValue(name);
                    return v == null ? 0 : Convert.ToInt32(v);
                }
            }
            catch { return 0; }
        }

        // 0 = off, 1 = auto, 2 = require
        public static int DohMode
        {
            get
            {
                int policy = ReadDword(DohPolicyPath, DohPolicyKey);
                if (policy == 3) return 2;
                if (policy == 2) return 1;
                // Значение 1 писали прежние версии приложения — оно ничего не
                // включало, поэтому и показывать его включённым нельзя.
                return ReadDword(DohRegPath, DohRegKey) == 2 ? 1 : 0;
            }
            set
            {
                try
                {
                    using (var k = Registry.LocalMachine.CreateSubKey(DohPolicyPath))
                    {
                        if (value == 0) k.DeleteValue(DohPolicyKey, false);
                        else k.SetValue(DohPolicyKey, value == 2 ? 3 : 2, RegistryValueKind.DWord);
                    }
                    using (var k = Registry.LocalMachine.CreateSubKey(DohRegPath))
                    {
                        if (value == 0) k.DeleteValue(DohRegKey, false);
                        else k.SetValue(DohRegKey, 2, RegistryValueKind.DWord);
                    }
                    // Службу Dnscache в Windows 10/11 перезапустить нельзя:
                    // сбрасываем кэш, а полностью настройка может примениться
                    // только после перезагрузки (об этом пишет журнал).
                    string err;
                    Run("ipconfig", "/flushdns", 10000, out err);
                }
                catch (Exception ex) { Fail(string.Format(Loc.T("doh.err"), ex.Message)); }
            }
        }

        public static string DohModeLabel()
        {
            switch (DohMode)
            {
                case 1: return Loc.T("doh.auto");
                case 2: return Loc.T("doh.require");
                default: return Loc.T("doh.off");
            }
        }
    }
}

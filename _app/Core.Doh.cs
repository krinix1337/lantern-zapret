using System;
using Microsoft.Win32;

namespace ZapretStudio
{
    // Управление DNS-over-HTTPS через реестр Windows.
    // EnableAutoDOH: 0 = выкл, 1 = авто (предпочитать DoH), 2 = требовать DoH.
    static partial class Core
    {
        const string DohRegPath = @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
        const string DohRegKey = "EnableAutoDOH";

        // 0 = off, 1 = auto, 2 = require
        public static int DohMode
        {
            get
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(DohRegPath))
                    {
                        if (k == null) return 0;
                        object v = k.GetValue(DohRegKey);
                        if (v == null) return 0;
                        return Convert.ToInt32(v);
                    }
                }
                catch { return 0; }
            }
            set
            {
                try
                {
                    using (var k = Registry.LocalMachine.CreateSubKey(DohRegPath))
                    {
                        if (value == 0) k.DeleteValue(DohRegKey, false);
                        else k.SetValue(DohRegKey, value, RegistryValueKind.DWord);
                    }
                    // Перезапустить DNS-кэш для применения
                    Run("ipconfig", "/flushdns", 10000);
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

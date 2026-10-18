using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ZapretStudio
{
    // Локальные шрифты интерфейса. Источник — официальный Google Fonts CDN.
    // Ссылки зафиксированы на конкретные версии, а содержимое проверяется SHA-256,
    // поэтому подмена ответа сети не попадает в приложение.
    //
    // Семейства: Google Sans — дефолт (как и раньше), плюс четыре на выбор
    // (Unbounded, Onest, Golos Text, Literata). Все варианты скачиваются один
    // раз сразу при запуске (фоновый EnsureUiFonts), поэтому смена шрифта в
    // настройках применяется мгновенно, без ожидания загрузки.
    static partial class Core
    {
        public sealed class UiFontAsset
        {
            public readonly string Name, Url, Sha256;
            public UiFontAsset(string name, string url, string sha256)
            {
                Name = name; Url = url; Sha256 = sha256;
            }
        }

        // Семейство целиком. Title — оно же имя семейства внутри ttf-файлов и
        // фрагмент FontFamily ("./#" + Title) в Theme.ConfigureDownloadedFonts.
        public sealed class UiFontFamily
        {
            public readonly string Key;    // значение gui-config (ui_font)
            public readonly string Title;  // имя в настройках и во фрагменте WPF
            public readonly UiFontAsset[] Assets;
            public UiFontFamily(string key, string title, UiFontAsset[] assets)
            {
                Key = key; Title = title; Assets = assets;
            }
        }

        static readonly UiFontAsset[] GoogleSansAssets =
        {
            new UiFontAsset("GoogleSans-Regular.ttf", "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrwEIKli.ttf", "1DB8FE3048D1B15519999C1B0372285DED006BF49E109455DF1A09E42876EC64"),
            new UiFontAsset("GoogleSans-Medium.ttf",  "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrw2IKli.ttf", "E9156F50951740B525F8E6D110E0BE344214CB6D5FCE1E76CD3E828A604997E9"),
            new UiFontAsset("GoogleSans-Bold.ttf",    "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrzjJ6li.ttf", "E7A3BAEF8D5E96974A25D537CA253C5E4F25ED3BB0D9167D1F4773EA11D74F71"),
            new UiFontAsset("GoogleSansCode-Regular.ttf", "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNcmTVsw.ttf", "8552D7BC51103CA2A2DE1829CAC824B8FA3DD3D8E7F8A415C80FA19241FB14D1"),
            new UiFontAsset("GoogleSansCode-Medium.ttf",  "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNQGTVsw.ttf", "2612F74CE848A030999F053A596371C6F30A357EA1AF228573D233F2D9EC63CE"),
            new UiFontAsset("GoogleSansCode-Bold.ttf",    "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNlWPVsw.ttf", "17C5753834AC8D929ABEFFCE464233D349202F60AF6A81DCB6DA97BF38C2869F")
        };

        static readonly UiFontAsset[] UnboundedAssets =
        {
            new UiFontAsset("Unbounded-Regular.ttf", "https://fonts.gstatic.com/s/unbounded/v12/Yq6F-LOTXCb04q32xlpat-6uR42XTqtG6xjx043Hhv6K.ttf", "20E6E53C7CF98A32D5DED33F0686AEDB0277D9D9848B07FA9FBBD36C0D81196C"),
            new UiFontAsset("Unbounded-Medium.ttf",  "https://fonts.gstatic.com/s/unbounded/v12/Yq6F-LOTXCb04q32xlpat-6uR42XTqtG6yrx043Hhv6K.ttf", "314E1ADBA3AF7CE9AADE1B71EB829C82F6C147278C9AE653B7626738669D6744"),
            new UiFontAsset("Unbounded-Bold.ttf",    "https://fonts.gstatic.com/s/unbounded/v12/Yq6F-LOTXCb04q32xlpat-6uR42XTqtG6__2043Hhv6K.ttf", "99704D87ABEEEEF1B1E02AD2DFDC6BB83CDC14F73952AA546C368E2BFD2ED81D")
        };

        static readonly UiFontAsset[] OnestAssets =
        {
            new UiFontAsset("Onest-Regular.ttf", "https://fonts.gstatic.com/s/onest/v11/gNMZW3F-SZuj7zOT0IfSjTS16cPh9R-ZshFKQWQ.ttf", "DC8C59E566D4A81A08F8F1F16305B6BABC5E23F66A50FDC5E7F3A20CFCFEA2B0"),
            new UiFontAsset("Onest-Medium.ttf",  "https://fonts.gstatic.com/s/onest/v11/gNMZW3F-SZuj7zOT0IfSjTS16cPhxx-ZshFKQWQ.ttf", "576B9928F216C96FB0123D2CA40F29C16A3E7A56ABFCCCCC865B76EEC7690A6F"),
            new UiFontAsset("Onest-Bold.ttf",    "https://fonts.gstatic.com/s/onest/v11/gNMZW3F-SZuj7zOT0IfSjTS16cPhEhiZshFKQWQ.ttf", "47DAAFE3966E85A79983A68F21C406D5714778A76508BC8619ED119D6F1275B5")
        };

        static readonly UiFontAsset[] GolosTextAssets =
        {
            new UiFontAsset("GolosText-Regular.ttf", "https://fonts.gstatic.com/s/golostext/v7/q5uXsoe9Lv5t7Meb31EcOR9UdVTNs822plVRRQ5cErkzXA.ttf", "22708024491C2EB3F852E335CDDC9A75C4ECCFD3CCE0D5F671BED4DDFA8E8549"),
            new UiFontAsset("GolosText-Medium.ttf",  "https://fonts.gstatic.com/s/golostext/v7/q5uXsoe9Lv5t7Meb31EcOR9UdVTNs822plVjRQ5cErkzXA.ttf", "5CA281CD7FCC737D89391BF8BD597247416D581A15D2C796E502F06F2D9E444E"),
            new UiFontAsset("GolosText-Bold.ttf",    "https://fonts.gstatic.com/s/golostext/v7/q5uXsoe9Lv5t7Meb31EcOR9UdVTNs822plW2Qg5cErkzXA.ttf", "10A47E2C00B070BDE14C9188173147C2F26FF9E134346372282989ABFE91D223")
        };

        static readonly UiFontAsset[] LiterataAssets =
        {
            new UiFontAsset("Literata-Regular.ttf", "https://fonts.gstatic.com/s/literata/v40/or3PQ6P12-iJxAIgLa78DkrbXsDgk0oVDaDPYLanFLHpPf2TbBG_F_bcS2CX.ttf", "DF89561F5555D0674CEE91736EE18D6F85CA50E6A280E53BEC37507EAE5706DA"),
            new UiFontAsset("Literata-Medium.ttf",  "https://fonts.gstatic.com/s/literata/v40/or3PQ6P12-iJxAIgLa78DkrbXsDgk0oVDaDPYLanFLHpPf2TbCO_F_bcS2CX.ttf", "6DF469837BC38531FFC4D60CE7106D9116EEA96A8777A826CF6E854E00B629C6"),
            new UiFontAsset("Literata-Bold.ttf",    "https://fonts.gstatic.com/s/literata/v40/or3PQ6P12-iJxAIgLa78DkrbXsDgk0oVDaDPYLanFLHpPf2TbPa4F_bcS2CX.ttf", "FFA4A76CFFE8C3DCDAF5ECD1737F9921907AA4D6FD5D866D1FD99F1ADB46C452")
        };

        // Реестр семейств. Порядок = порядок в списке настроек; дефолт — первый.
        public static readonly UiFontFamily[] FontFamilies =
        {
            new UiFontFamily("default",   "Google Sans",   GoogleSansAssets),
            new UiFontFamily("unbounded", "Unbounded",     UnboundedAssets),
            new UiFontFamily("onest",     "Onest",         OnestAssets),
            new UiFontFamily("golos",     "Golos Text",    GolosTextAssets),
            new UiFontFamily("literata",  "Literata",      LiterataAssets)
        };

        public static UiFontFamily DefaultFontFamily { get { return FontFamilies[0]; } }

        public static string FontsDir { get { return Path.Combine(UtilsDir, "fonts"); } }

        static UiFontFamily FindFontFamily(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var f in FontFamilies)
                if (string.Equals(f.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)) return f;
            return null;
        }

        public static UiFontFamily ResolveUiFontFamily()
        {
            var fam = FindFontFamily(Get("ui_font", null));
            return fam != null ? fam : DefaultFontFamily;
        }

        // Незнакомое значение ui_font (правленый конфиг или убранное в новой
        // версии семейство) заменяем на дефолт и разово пишем в файл, чтобы
        // каждый запуск не проделывал одну и ту же подстановку мимо конфига.
        public static void NormalizeSavedUiFont()
        {
            string raw = Get("ui_font", null);
            if (string.IsNullOrEmpty(raw) || FindFontFamily(raw) != null) return;
            Set("ui_font", DefaultFontFamily.Key);
            SaveConfig();
            Info(Loc.T("fonts.migrated"));
        }

        static bool FamilyFilesMatch(UiFontFamily fam)
        {
            if (string.IsNullOrEmpty(Root)) return false;
            foreach (var asset in fam.Assets)
                if (!FileMatches(Path.Combine(FontsDir, asset.Name), asset.Sha256)) return false;
            return true;
        }

        public static bool ConfigureUiFontsFromCache()
        {
            if (string.IsNullOrEmpty(Root)) return false;
            if (!FamilyFilesMatch(DefaultFontFamily)) return false;
            var active = ResolveUiFontFamily();
            // Выбранного семейства нет на диске (снесли utils\fonts) — до докачки
            // показываем дефолт; фоном его вернёт EnsureUiFonts.
            if (active.Key != DefaultFontFamily.Key && !FamilyFilesMatch(active))
                active = DefaultFontFamily;
            return Theme.ConfigureDownloadedFonts(FontsDir, active.Title);
        }

        public static void EnsureUiFontsInBackground()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { EnsureUiFonts(); }
                catch (Exception ex) { Fail("EnsureUiFontsInBackground: " + ex.Message); }
            });
        }

        // Скачивание сериализуем: два быстрых переключения шрифта в настройках
        // не должны гоняться за одними и теми же файлами.
        static readonly object UiFontsLock = new object();

        // Выполняется в фоне при запуске и при смене шрифта в настройках.
        // Возвращает true, если шрифты готовы к применению; отсутствие сети
        // не блокирует запуск Lantern.
        public static bool EnsureUiFonts()
        {
            lock (UiFontsLock)
            {
                if (string.IsNullOrEmpty(Root)) return false;
                try { Directory.CreateDirectory(FontsDir); }
                catch { return false; }

                // Качаем сразу все семейства: выбор в настройках потом
                // применяется мгновенно, без фоновой докачки.
                bool allReady = true;
                foreach (var fam in FontFamilies)
                {
                    foreach (var asset in fam.Assets)
                    {
                        string destination = Path.Combine(FontsDir, asset.Name);
                        if (FileMatches(destination, asset.Sha256)) continue;

                        allReady = false;
                        try { if (File.Exists(destination)) File.Delete(destination); }
                        catch { return false; }

                        if (!DownloadFile(asset.Url, destination, null, null) || !FileMatches(destination, asset.Sha256))
                        {
                            try { if (File.Exists(destination)) File.Delete(destination); }
                            catch { }
                            Warn(string.Format(Loc.T("fonts.dlFail"), asset.Name));
                            return false;
                        }
                    }
                }

                // Активное семейство могли переключить, пока качалось: применяем
                // то, что стоит в конфиге сейчас (недоступное — дефолт).
                var cur = ResolveUiFontFamily();
                if (!FamilyFilesMatch(cur)) cur = DefaultFontFamily;
                string curTitle = cur.Title;

                bool configured = false;
                var app = System.Windows.Application.Current;
                if (app != null && app.Dispatcher != null)
                {
                    // №59 осознанно оставлен синхронный Invoke, а не BeginInvoke, как
                    // предлагало ТЗ: результат ConfigureDownloadedFonts возвращается
                    // наружу, а FontFamily пересоздаётся в диспетчере из-за аффинитети
                    // UI-объектов. Deadlock при этом невозможен: из UI-потока Invoke
                    // выполняется inline (CheckAccess == true), а фоновый поток,
                    // дождавшийся ответа, ничему в UI не блокирует.
                    app.Dispatcher.Invoke((Action)delegate
                    {
                        configured = Theme.ConfigureDownloadedFonts(FontsDir, curTitle);
                        if (configured && !allReady)
                        {
                            Theme.Apply(Theme.Mode);
                        }
                    });
                }
                else
                {
                    configured = Theme.ConfigureDownloadedFonts(FontsDir, curTitle);
                }
                if (configured && !allReady) Info(Loc.T("fonts.downloaded"));
                return configured;
            }
        }

        // Смена шрифта интерфейса из настроек: пишем конфиг; если файлы уже на
        // диске — применяем сразу, иначе докачиваем фоном (как при старте).
        public static void RequestUiFont(string key)
        {
            var fam = FindFontFamily(key);
            if (fam == null) fam = DefaultFontFamily;
            Set("ui_font", fam.Key);
            SaveConfig();
            if (FamilyFilesMatch(fam))
            {
                Theme.ConfigureDownloadedFonts(FontsDir, fam.Title);
                Theme.Apply(Theme.Mode);
            }
            else
            {
                Info(string.Format(Loc.T("fonts.downloading"), fam.Title));
                EnsureUiFontsInBackground();
            }
        }

        static bool FileMatches(string path, string expectedHash)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using (var sha = SHA256.Create())
                using (var file = File.OpenRead(path))
                {
                    byte[] hash = sha.ComputeHash(file);
                    var actual = BitConverter.ToString(hash).Replace("-", "");
                    return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }
    }
}

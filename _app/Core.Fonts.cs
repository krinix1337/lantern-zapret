using System;
using System.IO;
using System.Security.Cryptography;

namespace ZapretStudio
{
    // Локальные шрифты интерфейса. Источник — официальный Google Fonts CDN.
    // Ссылки зафиксированы на конкретные версии, а содержимое проверяется SHA-256,
    // поэтому подмена ответа сети не попадает в приложение.
    static partial class Core
    {
        sealed class UiFontAsset
        {
            public readonly string Name, Url, Sha256;
            public UiFontAsset(string name, string url, string sha256)
            {
                Name = name; Url = url; Sha256 = sha256;
            }
        }

        static readonly UiFontAsset[] UiFontAssets =
        {
            new UiFontAsset("GoogleSans-Regular.ttf", "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrwEIKli.ttf", "1DB8FE3048D1B15519999C1B0372285DED006BF49E109455DF1A09E42876EC64"),
            new UiFontAsset("GoogleSans-Medium.ttf",  "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrw2IKli.ttf", "E9156F50951740B525F8E6D110E0BE344214CB6D5FCE1E76CD3E828A604997E9"),
            new UiFontAsset("GoogleSans-Bold.ttf",    "https://fonts.gstatic.com/s/googlesans/v70/4Ua_rENHsxJlGDuGo1OIlJfC6l_24rlCK1Yo_Iqcsih3SAyH6cAwhX9RFD48TE63OOYKtrzjJ6li.ttf", "E7A3BAEF8D5E96974A25D537CA253C5E4F25ED3BB0D9167D1F4773EA11D74F71"),
            new UiFontAsset("GoogleSansCode-Regular.ttf", "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNcmTVsw.ttf", "8552D7BC51103CA2A2DE1829CAC824B8FA3DD3D8E7F8A415C80FA19241FB14D1"),
            new UiFontAsset("GoogleSansCode-Medium.ttf",  "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNQGTVsw.ttf", "2612F74CE848A030999F053A596371C6F30A357EA1AF228573D233F2D9EC63CE"),
            new UiFontAsset("GoogleSansCode-Bold.ttf",    "https://fonts.gstatic.com/s/googlesanscode/v17/pxihyogzv91QhV44Z_GQBHsGf5PuckJMZfIVTPZaiXEp_ht12EVEHsN1sCQNlWPVsw.ttf", "17C5753834AC8D929ABEFFCE464233D349202F60AF6A81DCB6DA97BF38C2869F")
        };

        public static string FontsDir { get { return Path.Combine(UtilsDir, "fonts"); } }

        // Выполняется при запуске до создания MainWindow. Возвращает true, если
        // шрифты готовы к применению; отсутствие сети не блокирует запуск Lantern.
        public static bool EnsureUiFonts()
        {
            if (string.IsNullOrEmpty(Root)) return false;
            try { Directory.CreateDirectory(FontsDir); }
            catch { return false; }

            bool allReady = true;
            foreach (var asset in UiFontAssets)
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
                    Warn("Не удалось безопасно скачать шрифт интерфейса: " + asset.Name);
                    return false;
                }
            }

            bool configured = Theme.ConfigureDownloadedFonts(FontsDir);
            if (configured && !allReady) Info("Шрифты Google Sans загружены и проверены.");
            return configured;
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

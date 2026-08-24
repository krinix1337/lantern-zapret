using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ZapretStudio
{
    public class AudioTrackInfo
    {
        public string FilePath;
        public string Title;
        public string Artist;
        public ImageSource Cover;
    }

    // Мощный и надёжный парсер аудио-метаданных и обложек для MP3, WAV, FLAC, M4A, OGG, WMA
    public static class AudioTagReader
    {
        public static AudioTrackInfo Read(string filePath, ImageSource defaultCover)
        {
            var fallback = defaultCover ?? MainWindow.PeterBackdrop();
            var info = new AudioTrackInfo
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath),
                Artist = Loc.T("player.defaultArtist"),
                Cover = fallback
            };

            // 1. Эвристика из имени файла: "Artist - Title"
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.Contains(" - "))
            {
                var parts = fileName.Split(new[] { " - " }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    info.Artist = parts[0].Trim();
                    info.Title = parts[1].Trim();
                }
            }

            // 2. Поиск персональной обложки трека в папке (<имя>.jpg, <имя>.png, cover.jpg, folder.png)
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                    string[] candidates = {
                        Path.Combine(dir, baseName + ".jpg"),
                        Path.Combine(dir, baseName + ".jpeg"),
                        Path.Combine(dir, baseName + ".png"),
                        Path.Combine(dir, "cover.jpg"),
                        Path.Combine(dir, "cover.png"),
                        Path.Combine(dir, "folder.jpg"),
                        Path.Combine(dir, "folder.png")
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            var img = LoadImageFromFile(c);
                            if (img != null) { info.Cover = img; break; }
                        }
                    }
                }
            }
            catch { }

            // 3. Извлечение встроенных метаданных и обложки из аудиофайла
            try
            {
                if (File.Exists(filePath))
                {
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (ext == ".mp3") ReadId3v2(fs, info);
                        else if (ext == ".flac" || ext == ".ogg") ReadFlacOrOgg(fs, info);
                        else if (ext == ".m4a" || ext == ".mp4" || ext == ".aac") ReadM4a(fs, info);
                        else if (ext == ".wav") ReadWavInfo(fs, info);
                    }
                }
            }
            catch { }

            if (info.Cover == null) info.Cover = fallback;
            return info;
        }

        static void ReadId3v2(Stream stream, AudioTrackInfo info)
        {
            if (stream.Length < 10) return;
            byte[] header = new byte[10];
            stream.Read(header, 0, 10);
            if (header[0] != 'I' || header[1] != 'D' || header[2] != '3') return;

            int ver = header[3];
            int tagSize = ((header[6] & 0x7F) << 21) | ((header[7] & 0x7F) << 14) |
                          ((header[8] & 0x7F) << 7) | (header[9] & 0x7F);
            if (tagSize <= 0 || tagSize > 12 * 1024 * 1024) return;

            byte[] tagData = new byte[tagSize];
            int readTotal = ReadFull(stream, tagData, 0, tagSize);

            // Поиск текстовых тегов TIT2, TPE1
            int pos = 0;
            while (pos + 10 < readTotal)
            {
                if (tagData[pos] == 0) break;
                string frameId = Encoding.ASCII.GetString(tagData, pos, 4);
                int frameSize = (ver == 4)
                    ? (((tagData[pos + 4] & 0x7F) << 21) | ((tagData[pos + 5] & 0x7F) << 14) |
                       ((tagData[pos + 6] & 0x7F) << 7) | (tagData[pos + 7] & 0x7F))
                    : ((tagData[pos + 4] << 24) | (tagData[pos + 5] << 16) | (tagData[pos + 6] << 8) | tagData[pos + 7]);

                if (frameSize <= 0 || pos + 10 + frameSize > readTotal) break;

                if (frameId == "TIT2" && frameSize > 1)
                {
                    byte[] data = new byte[frameSize];
                    Array.Copy(tagData, pos + 10, data, 0, frameSize);
                    string t = DecodeId3Text(data);
                    if (!string.IsNullOrEmpty(t)) info.Title = t;
                }
                else if (frameId == "TPE1" && frameSize > 1)
                {
                    byte[] data = new byte[frameSize];
                    Array.Copy(tagData, pos + 10, data, 0, frameSize);
                    string a = DecodeId3Text(data);
                    if (!string.IsNullOrEmpty(a)) info.Artist = a;
                }
                else if (frameId == "APIC" || frameId == "PIC")
                {
                    byte[] data = new byte[frameSize];
                    Array.Copy(tagData, pos + 10, data, 0, frameSize);
                    var img = ExtractImageFromBytes(data);
                    if (img != null) info.Cover = img;
                }

                pos += 10 + frameSize;
            }

            // Если APIC не распознан по фрейму, сканируем сигнатуру изображения во всём заголовке ID3
            if (info.Cover == null || info.Cover == MainWindow.PeterBackdrop())
            {
                var img = ExtractImageFromBytes(tagData);
                if (img != null) info.Cover = img;
            }
        }

        static void ReadFlacOrOgg(Stream stream, AudioTrackInfo info)
        {
            try
            {
                byte[] buf = new byte[Math.Min(stream.Length, 512 * 1024)];
                stream.Read(buf, 0, buf.Length);
                string text = Encoding.UTF8.GetString(buf);
                int idxTitle = text.IndexOf("TITLE=", StringComparison.OrdinalIgnoreCase);
                if (idxTitle >= 0)
                {
                    int end = text.IndexOfAny(new[] { '\0', '\n', '\r' }, idxTitle);
                    if (end > idxTitle) info.Title = text.Substring(idxTitle + 6, end - idxTitle - 6).Trim();
                }
                int idxArtist = text.IndexOf("ARTIST=", StringComparison.OrdinalIgnoreCase);
                if (idxArtist >= 0)
                {
                    int end = text.IndexOfAny(new[] { '\0', '\n', '\r' }, idxArtist);
                    if (end > idxArtist) info.Artist = text.Substring(idxArtist + 7, end - idxArtist - 7).Trim();
                }
                var img = ExtractImageFromBytes(buf);
                if (img != null) info.Cover = img;
            }
            catch { }
        }

        static void ReadM4a(Stream stream, AudioTrackInfo info)
        {
            try
            {
                byte[] buf = new byte[Math.Min(stream.Length, 512 * 1024)];
                stream.Read(buf, 0, buf.Length);
                string raw = Encoding.Default.GetString(buf);
                int namIdx = raw.IndexOf("\xa9nam");
                if (namIdx >= 0 && namIdx + 24 < buf.Length)
                {
                    int len = (buf[namIdx + 8] << 8) | buf[namIdx + 9];
                    if (len > 0 && len < 256 && namIdx + 16 + len <= buf.Length)
                        info.Title = Encoding.UTF8.GetString(buf, namIdx + 16, len).Trim('\0', ' ');
                }
                int artIdx = raw.IndexOf("\xa9ART");
                if (artIdx >= 0 && artIdx + 24 < buf.Length)
                {
                    int len = (buf[artIdx + 8] << 8) | buf[artIdx + 9];
                    if (len > 0 && len < 256 && artIdx + 16 + len <= buf.Length)
                        info.Artist = Encoding.UTF8.GetString(buf, artIdx + 16, len).Trim('\0', ' ');
                }
                var img = ExtractImageFromBytes(buf);
                if (img != null) info.Cover = img;
            }
            catch { }
        }

        static void ReadWavInfo(Stream stream, AudioTrackInfo info)
        {
            try
            {
                byte[] buf = new byte[Math.Min(stream.Length, 65536)];
                stream.Read(buf, 0, buf.Length);
                string raw = Encoding.ASCII.GetString(buf);
                int inam = raw.IndexOf("INAM");
                if (inam >= 0 && inam + 8 < buf.Length)
                {
                    int len = BitConverter.ToInt32(buf, inam + 4);
                    if (len > 0 && len < 256 && inam + 8 + len <= buf.Length)
                        info.Title = Encoding.UTF8.GetString(buf, inam + 8, len).Trim('\0', ' ');
                }
                int iart = raw.IndexOf("IART");
                if (iart >= 0 && iart + 8 < buf.Length)
                {
                    int len = BitConverter.ToInt32(buf, iart + 4);
                    if (len > 0 && len < 256 && iart + 8 + len <= buf.Length)
                        info.Artist = Encoding.UTF8.GetString(buf, iart + 8, len).Trim('\0', ' ');
                }
            }
            catch { }
        }

        static int ReadFull(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, offset + total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        // public: покрывается юнит-тестами SelfTest (кодировки ID3).
        public static string DecodeId3Text(byte[] data)
        {
            if (data == null || data.Length <= 1) return null;
            byte enc = data[0];
            try
            {
                if (enc == 0)
                {
                    // Частая ошибка тегеров: помечают тег как ANSI (0), а байты —
                    // сырой UTF-8. Строгий декодер UTF-8 кидает исключение на
                    // настоящей cp1251, поэтому путаница исключена.
                    try
                    {
                        var strict = new UTF8Encoding(false, true);
                        return strict.GetString(data, 1, data.Length - 1).Trim('\0', ' ');
                    }
                    catch (DecoderFallbackException) { }
                    catch (ArgumentException) { }

                    // Настоящая ANSI: обычно это cp1251 у русскоязычных файлов;
                    // чиним возможное двойное кодирование (UTF-8→1252).
                    return RepairDoubleEncodedUtf8(Encoding.GetEncoding(1251).GetString(data, 1, data.Length - 1)).Trim('\0', ' ');
                }
                if (enc == 1) return Encoding.Unicode.GetString(data, 1, data.Length - 1).Trim('\0', ' ');
                if (enc == 2) return Encoding.BigEndianUnicode.GetString(data, 1, data.Length - 1).Trim('\0', ' ');
                if (enc == 3) return Encoding.UTF8.GetString(data, 1, data.Length - 1).Trim('\0', ' ');
            }
            catch { }
            return Encoding.UTF8.GetString(data, 1, data.Length - 1).Trim('\0', ' ');
        }

        static string RepairDoubleEncodedUtf8(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Случай A: сырой UTF-8 был прочитан как cp1252 → маркеры "Ð"/"Ñ".
            if (text.IndexOf('Ð') >= 0 || text.IndexOf('Ñ') >= 0)
            {
                try
                {
                    string fixedText = Encoding.UTF8.GetString(Encoding.GetEncoding(1252).GetBytes(text));
                    foreach (char c in fixedText)
                        if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё' || char.IsSurrogate(c)) return fixedText;
                }
                catch { }
            }

            // Случай B: сырой UTF-8 прочитан как cp1251 → мусор вида «РџСЂРё…»
            // (характерные заглавные Р/С перед кириллическим символом).
            int pairs = 0;
            for (int i = 0; i + 1 < text.Length; i++)
                if ((text[i] == 'Р' || text[i] == 'С') && IsCyrillic(text[i + 1])) pairs++;
            if (pairs >= 2)
            {
                try
                {
                    string alt = Encoding.UTF8.GetString(Encoding.GetEncoding(1251).GetBytes(text));
                    if (alt.IndexOf('\uFFFD') < 0 && alt != text)
                    {
                        int cyr = 0;
                        foreach (char c in alt) if (IsCyrillic(c)) cyr++;
                        if (cyr >= 2) return alt;
                    }
                }
                catch { }
            }
            return text;
        }

        static bool IsCyrillic(char c)
        {
            return (c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё';
        }

        // Поиск бинарных сигнатур JPEG (FF D8 FF) и PNG (89 50 4E 47) внутри массива байт
        static ImageSource ExtractImageFromBytes(byte[] data)
        {
            if (data == null || data.Length < 16) return null;

            int start = -1;
            for (int i = 0; i < data.Length - 4; i++)
            {
                if (data[i] == 0xFF && data[i + 1] == 0xD8 && data[i + 2] == 0xFF)
                {
                    start = i;
                    break;
                }
                if (data[i] == 0x89 && data[i + 1] == 0x50 && data[i + 2] == 0x4E && data[i + 3] == 0x47)
                {
                    start = i;
                    break;
                }
            }

            if (start < 0) return null;

            try
            {
                using (var ms = new MemoryStream(data, start, data.Length - start))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return null; }
        }

        static ImageSource LoadImageFromFile(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }
    }

    // Контроллер воспроизведения музыки с мягким кроссфейдом
    public class PeterMusicController
    {
        readonly MediaPlayer _player = new MediaPlayer();
        readonly List<string> _playlist = new List<string>();
        readonly DispatcherTimer _progressTimer;
        readonly Random _random = new Random();
        readonly ImageSource _defaultPeterCover = MainWindow.PeterBackdrop();
        DispatcherTimer _fadeTimer;
        int _fadeStep;
        int _fadeTotalSteps = 8;
        double _fadeStartVol;
        double _fadeTargetVol;
        Action _onFadeEnd;

        int _currentIndex = -1;
        bool _isPlaying;
        bool _isPaused;
        bool _shuffleMode = true;
        double _volume = 0.35;
        AudioTrackInfo _currentTrack;

        // .ogg намеренно отсутствует: системный MediaPlayer нестабильно
        // декодирует OGG в Windows (см. заметки к v5.2).
        public static readonly string[] SupportedExtensions = {
            ".mp3", ".wav", ".m4a", ".aac", ".flac", ".wma", ".mp4"
        };

        public event Action StateChanged;
        public event Action<AudioTrackInfo> TrackChanged;
        public event Action<TimeSpan, TimeSpan> ProgressTick;
        public event Action<double> VolumeChanged;

        public bool IsActive { get { return _isPlaying || _isPaused; } }
        public bool IsPlaying { get { return _isPlaying; } }
        public bool IsPaused { get { return _isPaused; } }
        public bool ShuffleMode
        {
            get { return _shuffleMode; }
            set { _shuffleMode = value; if (StateChanged != null) StateChanged(); }
        }
        public double Volume
        {
            get { return _volume; }
            set
            {
                _volume = Math.Max(0.0, Math.Min(1.0, value));
                if (_fadeTimer == null) _player.Volume = _volume;
                // Ползунок дёргает Volume десятки раз в секунду: сохранение конфига
                // откладываем (debounce), чтобы не писать файл на каждый шаг.
                ScheduleVolumeSave();
                if (VolumeChanged != null) VolumeChanged(_volume);
            }
        }

        DispatcherTimer _volumeSaveTimer;
        void ScheduleVolumeSave()
        {
            try
            {
                Core.Set("peter_volume", _volume.ToString("0.00", CultureInfo.InvariantCulture));
                if (_volumeSaveTimer == null)
                {
                    _volumeSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                    _volumeSaveTimer.Tick += (s, e) =>
                    {
                        _volumeSaveTimer.Stop();
                        try { Core.SaveConfig(); } catch { }
                    };
                }
                _volumeSaveTimer.Stop(); // перезапуск: пишем только после паузы в изменениях
                _volumeSaveTimer.Start();
            }
            catch { }
        }
        public AudioTrackInfo CurrentTrack { get { return _currentTrack; } }
        public int TrackCount { get { return _playlist.Count; } }

        public PeterMusicController()
        {
            try
            {
                string savedVol = Core.Get("peter_volume", "0.35");
                double v;
                if (double.TryParse(savedVol, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    _volume = Math.Max(0.0, Math.Min(1.0, v));
            }
            catch { }

            _player.MediaOpened += (s, e) =>
            {
                try
                {
                    if (_isPlaying)
                    {
                        // Когда файл готов к воспроизведению, плавно нарастает громкость
                        AnimateVolume(0, _volume, 240, null);
                    }
                }
                catch { }
            };

            _player.MediaEnded += (s, e) => { try { PlayNext(); } catch { } };
            _player.MediaFailed += (s, e) =>
            {
                try
                {
                    if (_playlist.Count > 1) PlayNext();
                    else Stop();
                }
                catch { }
            };
            try { _player.Volume = _volume; } catch { }

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _progressTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_isPlaying && _player.NaturalDuration.HasTimeSpan)
                    {
                        if (ProgressTick != null)
                            ProgressTick(_player.Position, _player.NaturalDuration.TimeSpan);
                    }
                }
                catch { }
            };
        }

        public static string GetPeterSongsDir()
        {
            if (!string.IsNullOrEmpty(Core.Root))
            {
                string d = Path.Combine(Core.Root, "assets", "peter-songs");
                if (Directory.Exists(d)) return d;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                string d1 = Path.Combine(baseDir, "assets", "peter-songs");
                if (Directory.Exists(d1)) return d1;

                string cur = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (int i = 0; i < 4 && !string.IsNullOrEmpty(cur); i++)
                {
                    string candidate = Path.Combine(cur, "assets", "peter-songs");
                    if (Directory.Exists(candidate)) return candidate;
                    var parent = Directory.GetParent(cur);
                    if (parent == null) break;
                    cur = parent.FullName;
                }
            }

            return !string.IsNullOrEmpty(Core.Root)
                ? Path.Combine(Core.Root, "assets", "peter-songs")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "peter-songs");
        }

        public List<string> ScanTracks()
        {
            _playlist.Clear();
            try
            {
                string dir = GetPeterSongsDir();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        string ext = Path.GetExtension(f);
                        if (string.IsNullOrEmpty(ext)) continue;
                        foreach (var sup in SupportedExtensions)
                        {
                            if (ext.Equals(sup, StringComparison.OrdinalIgnoreCase))
                            {
                                _playlist.Add(Path.GetFullPath(f));
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
            return _playlist;
        }

        public bool ToggleMusic(ImageSource defaultCover)
        {
            if (IsActive)
            {
                Stop();
                return false;
            }
            else
            {
                if (_shuffleMode) PlayRandom(defaultCover);
                else
                {
                    ScanTracks();
                    if (_playlist.Count > 0) PlayIndex(0, defaultCover);
                }
                return true;
            }
        }

        public void PlayRandom(ImageSource defaultCover)
        {
            ScanTracks();
            if (_playlist.Count == 0)
            {
                if (StateChanged != null) StateChanged();
                return;
            }
            int next = _random.Next(_playlist.Count);
            if (_playlist.Count > 1 && next == _currentIndex) next = (next + 1) % _playlist.Count;
            PlayIndex(next, defaultCover);
        }

        void AnimateVolume(double from, double to, int durationMs, Action onComplete)
        {
            if (_fadeTimer != null)
            {
                _fadeTimer.Stop();
                _fadeTimer = null;
            }
            if (durationMs <= 0 || Math.Abs(from - to) < 0.001)
            {
                _player.Volume = to;
                if (onComplete != null) onComplete();
                return;
            }

            _fadeStartVol = from;
            _fadeTargetVol = to;
            _fadeTotalSteps = Math.Max(4, durationMs / 25);
            _fadeStep = 0;
            _onFadeEnd = onComplete;

            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            _fadeTimer.Tick += (s, e) =>
            {
                _fadeStep++;
                double progress = (double)_fadeStep / _fadeTotalSteps;
                if (progress >= 1.0)
                {
                    _fadeTimer.Stop();
                    _fadeTimer = null;
                    _player.Volume = _fadeTargetVol;
                    if (_onFadeEnd != null) _onFadeEnd();
                }
                else
                {
                    double curve = Math.Sin(progress * Math.PI / 2);
                    _player.Volume = _fadeStartVol + (_fadeTargetVol - _fadeStartVol) * curve;
                }
            };
            _fadeTimer.Start();
        }

        public void PlayIndex(int index, ImageSource defaultCover)
        {
            if (index < 0 || index >= _playlist.Count) return;
            _currentIndex = index;
            string trackPath = _playlist[index];

            if (_isPlaying && _player.Volume > 0.05)
            {
                // Мягкое плавное затухание текущей композиции перед включением следующей
                AnimateVolume(_player.Volume, 0, 140, () =>
                {
                    StartTrackPlayback(trackPath, _defaultPeterCover);
                });
            }
            else
            {
                StartTrackPlayback(trackPath, _defaultPeterCover);
            }
        }

        void StartTrackPlayback(string trackPath, ImageSource defaultCover)
        {
            try
            {
                string fullPath = Path.GetFullPath(trackPath);
                _currentTrack = AudioTagReader.Read(fullPath, defaultCover);
                _player.Volume = 0;
                _player.Open(new Uri(fullPath, UriKind.Absolute));
                _player.Play();
                _isPlaying = true;
                _isPaused = false;
                _progressTimer.Start();

                if (TrackChanged != null) TrackChanged(_currentTrack);
                if (StateChanged != null) StateChanged();
            }
            catch
            {
                PlayNext();
            }
        }

        public void PlayNext()
        {
            if (_playlist.Count == 0) ScanTracks();
            if (_playlist.Count == 0) { Stop(); return; }
            if (_shuffleMode && _playlist.Count > 1)
            {
                int next = _random.Next(_playlist.Count);
                if (next == _currentIndex) next = (next + 1) % _playlist.Count;
                PlayIndex(next, _defaultPeterCover);
            }
            else
            {
                int next = (_currentIndex + 1) % _playlist.Count;
                PlayIndex(next, _defaultPeterCover);
            }
        }

        public void PlayPrev()
        {
            if (_playlist.Count == 0) ScanTracks();
            if (_playlist.Count == 0) { Stop(); return; }
            int prev = (_currentIndex - 1 + _playlist.Count) % _playlist.Count;
            PlayIndex(prev, _defaultPeterCover);
        }

        public void TogglePlayPause()
        {
            if (_isPlaying)
            {
                AnimateVolume(_player.Volume, 0, 120, () =>
                {
                    _player.Pause();
                    _isPlaying = false;
                    _isPaused = true;
                    _progressTimer.Stop();
                    if (StateChanged != null) StateChanged();
                });
            }
            else if (_isPaused)
            {
                _player.Play();
                _isPlaying = true;
                _isPaused = false;
                _progressTimer.Start();
                AnimateVolume(0, _volume, 200, null);
                if (StateChanged != null) StateChanged();
            }
        }

        public void SeekFraction(double fraction)
        {
            if (_player.NaturalDuration.HasTimeSpan)
            {
                double total = _player.NaturalDuration.TimeSpan.TotalSeconds;
                double target = Math.Max(0, Math.Min(total, total * fraction));
                _player.Position = TimeSpan.FromSeconds(target);
                if (ProgressTick != null)
                    ProgressTick(_player.Position, _player.NaturalDuration.TimeSpan);
            }
        }

        public void Stop()
        {
            if (_fadeTimer != null) { _fadeTimer.Stop(); _fadeTimer = null; }
            _progressTimer.Stop();
            try { _player.Stop(); _player.Close(); } catch { }
            _isPlaying = false;
            _isPaused = false;
            _currentTrack = null;
            if (StateChanged != null) StateChanged();
        }
    }

    // Встроенный мини-плеер в боковой панели (Sidebar) с плавной сменой треков
    public class PeterMusicWidget : UserControl
    {
        readonly PeterMusicController _controller;
        Border _rootCard;
        StackPanel _fullLayout;
        Border _compactLayout;
        Image _coverImg;
        Image _compactCoverImg;
        TextBlock _titleTb;
        TextBlock _artistTb;
        TextBlock _timeElapsedTb;
        TextBlock _timeTotalTb;
        Button _btnShuffle;
        System.Windows.Shapes.Path _shuffleIcon;
        Button _btnPlayPause;
        System.Windows.Shapes.Path _playPauseIcon;
        System.Windows.Shapes.Path _compactPlayIcon;
        Button _btnVolume;
        System.Windows.Shapes.Path _volIcon;
        Grid _volSliderContainer;
        Border _volFill;
        Border _volKnob;
        TranslateTransform _volKnobTrans;
        Grid _seekContainer;
        Border _seekGroove;
        Border _seekFill;
        Border _seekKnob;
        TranslateTransform _seekKnobTrans;
        bool _isUserDraggingSeek;
        bool _isUserDraggingVol;
        double _prevUnmuteVolume = 0.35;

        public PeterMusicWidget(PeterMusicController controller)
        {
            _controller = controller;
            BuildUI();
            HookEvents();
            Visibility = Visibility.Collapsed;
            Opacity = 0;
        }

        void BuildUI()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            Margin = new Thickness(0, 0, 0, 8);

            _rootCard = new Border
            {
                Background = Theme.BrSurface,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R12,
                Padding = new Thickness(9, 8, 9, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            _rootCard.MouseWheel += (s, e) =>
            {
                double step = e.Delta > 0 ? 0.05 : -0.05;
                _controller.Volume = Math.Max(0.0, Math.Min(1.0, _controller.Volume + step));
            };

            _fullLayout = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            // 1. Верхний ряд: Обложка 34x34 + Текст + Кнопка закрытия
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Обложка
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Текст
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Кнопка закрытия

            var coverBorder = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = Theme.R8,
                ClipToBounds = true,
                Background = Theme.BrSurfaceAlt,
                VerticalAlignment = VerticalAlignment.Center
            };
            _coverImg = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(_coverImg, BitmapScalingMode.LowQuality);
            coverBorder.Child = _coverImg;
            Grid.SetColumn(coverBorder, 0);
            topGrid.Children.Add(coverBorder);

            var textStack = new StackPanel
            {
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _titleTb = new TextBlock
            {
                Text = "—",
                Foreground = Theme.BrText,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 108
            };
            _artistTb = new TextBlock
            {
                Text = Loc.T("player.defaultArtist"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.UiFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0),
                MaxWidth = 108
            };
            textStack.Children.Add(_titleTb);
            textStack.Children.Add(_artistTb);
            Grid.SetColumn(textStack, 1);
            topGrid.Children.Add(textStack);

            var btnClose = MiniBtn(Icons.Cross, 10, 20, 20, () => _controller.Stop(), Loc.T("player.close"));
            btnClose.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(btnClose, 2);
            topGrid.Children.Add(btnClose);

            _fullLayout.Children.Add(topGrid);

            // 2. Ряд кнопок управления: 🔀 ⏮️ ⏯️ ⏭️ + 🔊 Громкость
            var ctlRow = new Grid { Margin = new Thickness(0, 6, 0, 5) };
            ctlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctlRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var buttonsSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnShuffle = MiniBtn(Icons.Shuffle, 12, 20, 20, () =>
            {
                _controller.ShuffleMode = !_controller.ShuffleMode;
                UpdateShuffleVisual();
            }, Loc.T("player.shuffle"));
            _shuffleIcon = GetIconFromButton(_btnShuffle);
            UpdateShuffleVisual();
            buttonsSp.Children.Add(_btnShuffle);

            var btnPrev = MiniBtn(Icons.SkipPrev, 12, 22, 22, () => _controller.PlayPrev(), Loc.T("player.prev"));
            btnPrev.Margin = new Thickness(2, 0, 2, 0);
            buttonsSp.Children.Add(btnPrev);

            _btnPlayPause = new Button
            {
                Width = 26,
                Height = 26,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
            Ctl.StripChrome(_btnPlayPause);
            var playPauseBd = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = Theme.Rpill,
                Background = Theme.BrAccent,
                Child = _playPauseIcon = UI.Icon(Icons.Pause, 12, Theme.BrOnAccent, 1.8)
            };
            _playPauseIcon.HorizontalAlignment = HorizontalAlignment.Center;
            _playPauseIcon.VerticalAlignment = VerticalAlignment.Center;
            UI.AttachIconHoverAnimation(_btnPlayPause, _playPauseIcon, IconAnimType.ScaleBounce);
            _btnPlayPause.Content = playPauseBd;
            _btnPlayPause.Click += (s, e) => _controller.TogglePlayPause();
            Ctl.AutomationSetName(_btnPlayPause, Loc.T("player.play"));
            buttonsSp.Children.Add(_btnPlayPause);

            var btnNext = MiniBtn(Icons.SkipNext, 12, 22, 22, () => _controller.PlayNext(), Loc.T("player.next"));
            btnNext.Margin = new Thickness(2, 0, 0, 0);
            buttonsSp.Children.Add(btnNext);

            Grid.SetColumn(buttonsSp, 0);
            ctlRow.Children.Add(buttonsSp);

            // Блок громкости: Иконка 🔊 + Мини-ползунок
            var volWrap = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _btnVolume = MiniBtn(Icons.VolumeUp, 12, 20, 20, () =>
            {
                if (_controller.Volume > 0)
                {
                    _prevUnmuteVolume = _controller.Volume;
                    _controller.Volume = 0;
                }
                else
                {
                    _controller.Volume = _prevUnmuteVolume > 0 ? _prevUnmuteVolume : 0.35;
                }
            }, Loc.T("player.volume"));
            _volIcon = GetIconFromButton(_btnVolume);
            volWrap.Children.Add(_btnVolume);

            _volSliderContainer = new Grid
            {
                Width = 44,
                Height = 14,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };

            var volGroove = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrSurfaceHi,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false
            };
            _volSliderContainer.Children.Add(volGroove);

            _volFill = new Border
            {
                Width = 0,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrAccent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _volSliderContainer.Children.Add(_volFill);

            _volKnobTrans = new TranslateTransform();
            _volKnob = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = Theme.BrOnAccent,
                BorderBrush = Theme.BrAccent,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _volKnobTrans,
                IsHitTestVisible = false
            };
            _volSliderContainer.Children.Add(_volKnob);

            _volSliderContainer.MouseDown += OnVolMouseDown;
            _volSliderContainer.MouseMove += OnVolMouseMove;
            _volSliderContainer.MouseUp += (s, e) => { _isUserDraggingVol = false; _volSliderContainer.ReleaseMouseCapture(); };

            volWrap.Children.Add(_volSliderContainer);
            Grid.SetColumn(volWrap, 1);
            ctlRow.Children.Add(volWrap);

            _fullLayout.Children.Add(ctlRow);

            // 3. Тонкий SeekBar + тайминг
            _seekContainer = new Grid
            {
                Height = 14,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 1, 0, 2)
            };

            _seekGroove = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrSurfaceHi,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false
            };
            _seekContainer.Children.Add(_seekGroove);

            _seekFill = new Border
            {
                Width = 0,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Theme.BrAccent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _seekContainer.Children.Add(_seekFill);

            _seekKnobTrans = new TranslateTransform();
            _seekKnob = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Theme.BrOnAccent,
                BorderBrush = Theme.BrAccent,
                BorderThickness = new Thickness(1.5),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _seekKnobTrans,
                IsHitTestVisible = false
            };
            _seekContainer.Children.Add(_seekKnob);

            _seekContainer.MouseDown += OnSeekMouseDown;
            _seekContainer.MouseMove += OnSeekMouseMove;
            _seekContainer.MouseUp += (s, e) => { _isUserDraggingSeek = false; _seekContainer.ReleaseMouseCapture(); };
            _seekContainer.MouseEnter += (s, e) =>
            {
                _seekGroove.Height = 4;
                _seekFill.Height = 4;
                _seekKnob.Width = 10;
                _seekKnob.Height = 10;
                _seekKnob.CornerRadius = new CornerRadius(5);
            };
            _seekContainer.MouseLeave += (s, e) =>
            {
                if (!_isUserDraggingSeek)
                {
                    _seekGroove.Height = 3;
                    _seekFill.Height = 3;
                    _seekKnob.Width = 8;
                    _seekKnob.Height = 8;
                    _seekKnob.CornerRadius = new CornerRadius(4);
                }
            };

            _fullLayout.Children.Add(_seekContainer);

            var timeGrid = new Grid { Margin = new Thickness(0, 1, 0, 0) };
            timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _timeElapsedTb = new TextBlock
            {
                Text = "0:00",
                Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.MonoFont,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(_timeElapsedTb, 0);
            timeGrid.Children.Add(_timeElapsedTb);

            _timeTotalTb = new TextBlock
            {
                Text = "0:00",
                Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.MonoFont,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_timeTotalTb, 1);
            timeGrid.Children.Add(_timeTotalTb);

            _fullLayout.Children.Add(timeGrid);

            // Компактный режим для свёрнутого сайдбара (38x38)
            _compactLayout = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = Theme.R8,
                Background = Theme.BrSurfaceAlt,
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var compactGrid = new Grid();
            _compactCoverImg = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(_compactCoverImg, BitmapScalingMode.LowQuality);
            compactGrid.Children.Add(_compactCoverImg);

            var compactOverlay = new Border
            {
                Background = Theme.Alpha(Theme.BgBase, 120),
                Child = _compactPlayIcon = UI.Icon(Icons.Pause, 14, Theme.BrText, 1.8)
            };
            _compactPlayIcon.HorizontalAlignment = HorizontalAlignment.Center;
            _compactPlayIcon.VerticalAlignment = VerticalAlignment.Center;
            compactGrid.Children.Add(compactOverlay);

            _compactLayout.Child = compactGrid;
            _compactLayout.MouseDown += (s, e) => _controller.TogglePlayPause();

            var wrapper = new StackPanel();
            wrapper.Children.Add(_fullLayout);
            wrapper.Children.Add(_compactLayout);

            _rootCard.Child = wrapper;
            Content = _rootCard;
            UpdateVolumeVisual(_controller.Volume);
        }

        public void SetCollapsedMode(bool isCollapsed)
        {
            if (isCollapsed)
            {
                _rootCard.Padding = new Thickness(2);
                _rootCard.Background = Brushes.Transparent;
                _rootCard.BorderThickness = new Thickness(0);
                _fullLayout.Visibility = Visibility.Collapsed;
                _compactLayout.Visibility = Visibility.Visible;
            }
            else
            {
                _rootCard.Padding = new Thickness(9, 8, 9, 8);
                _rootCard.Background = Theme.BrSurface;
                _rootCard.BorderThickness = new Thickness(1);
                _fullLayout.Visibility = Visibility.Visible;
                _compactLayout.Visibility = Visibility.Collapsed;
            }
        }

        void UpdateVolumeVisual(double vol)
        {
            if (_volIcon != null)
            {
                if (vol <= 0.001) UI.UpdateIcon(_volIcon, Icons.VolumeOff, Theme.BrMuted);
                else if (vol < 0.5) UI.UpdateIcon(_volIcon, Icons.VolumeDown, Theme.BrMuted);
                else UI.UpdateIcon(_volIcon, Icons.VolumeUp, Theme.BrMuted);
            }
            if (_volSliderContainer != null && _volFill != null && _volKnobTrans != null)
            {
                double w = _volSliderContainer.ActualWidth > 0 ? _volSliderContainer.ActualWidth : 44;
                double knobSize = _volKnob.Width > 0 ? _volKnob.Width : 7;
                double usableW = Math.Max(1, w - knobSize);
                double left = vol * usableW;
                _volFill.Width = left + (knobSize / 2);
                _volKnobTrans.X = left;
            }
        }

        void OnVolMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserDraggingVol = true;
            _volSliderContainer.CaptureMouse();
            ApplyVol(e.GetPosition(_volSliderContainer).X);
        }

        void OnVolMouseMove(object sender, MouseEventArgs e)
        {
            if (_isUserDraggingVol && e.LeftButton == MouseButtonState.Pressed)
            {
                ApplyVol(e.GetPosition(_volSliderContainer).X);
            }
        }

        void ApplyVol(double mouseX)
        {
            double width = _volSliderContainer.ActualWidth > 0 ? _volSliderContainer.ActualWidth : 44;
            double knobSize = _volKnob.Width > 0 ? _volKnob.Width : 7;
            double usableW = Math.Max(1, width - knobSize);
            double fraction = Math.Max(0.0, Math.Min(1.0, (mouseX - (knobSize / 2)) / usableW));
            _controller.Volume = fraction;
        }

        void UpdateShuffleVisual()
        {
            if (_shuffleIcon != null)
            {
                _shuffleIcon.Fill = _controller.ShuffleMode ? Theme.BrAccent : Theme.BrMuted;
                _shuffleIcon.Opacity = _controller.ShuffleMode ? 1.0 : 0.45;
            }
        }

        System.Windows.Shapes.Path GetIconFromButton(Button b)
        {
            var bd = b.Content as Border;
            if (bd != null) return bd.Child as System.Windows.Shapes.Path;
            return null;
        }

        Button MiniBtn(string icon, double iconSize, double btnW, double btnH, Action onClick, string accName)
        {
            var b = new Button
            {
                Width = btnW,
                Height = btnH,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            Ctl.StripChrome(b);
            var bd = new Border
            {
                Width = btnW,
                Height = btnH,
                CornerRadius = Theme.R8,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var ic = UI.Icon(icon, iconSize, Theme.BrMuted, 1.8);
            ic.HorizontalAlignment = HorizontalAlignment.Center;
            ic.VerticalAlignment = VerticalAlignment.Center;
            UI.AttachIconHoverAnimation(b, ic, icon == Icons.Shuffle ? IconAnimType.Wiggle : IconAnimType.ScaleBounce);
            bd.Child = ic;
            b.Content = bd;
            b.MouseEnter += (s, e) => bd.Background = Theme.BrSurfaceAlt;
            b.MouseLeave += (s, e) => bd.Background = Brushes.Transparent;
            b.Click += (s, e) => onClick();
            Ctl.AutomationSetName(b, accName);
            return b;
        }

        void OnSeekMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserDraggingSeek = true;
            _seekContainer.CaptureMouse();
            ApplySeek(e.GetPosition(_seekContainer).X);
        }

        void OnSeekMouseMove(object sender, MouseEventArgs e)
        {
            if (_isUserDraggingSeek && e.LeftButton == MouseButtonState.Pressed)
            {
                ApplySeek(e.GetPosition(_seekContainer).X);
            }
        }

        void ApplySeek(double mouseX)
        {
            double width = _seekContainer.ActualWidth;
            if (width <= 0) return;
            double knobSize = _seekKnob.Width > 0 ? _seekKnob.Width : 8;
            double usableW = Math.Max(1, width - knobSize);
            double fraction = Math.Max(0.0, Math.Min(1.0, (mouseX - (knobSize / 2)) / usableW));
            _seekFill.Width = (fraction * usableW) + (knobSize / 2);
            _seekKnobTrans.X = fraction * usableW;
            _controller.SeekFraction(fraction);
        }

        Action<AudioTrackInfo> _onTrackChanged;
        Action<double> _onVolumeChanged;
        Action _onStateChanged;
        Action<TimeSpan, TimeSpan> _onProgressTick;

        // Виджет пересоздаётся при каждой смене темы/языка, а контроллер живёт
        // столько же, сколько окно: без Detach обработчики накапливаются.
        public void Detach()
        {
            if (_onTrackChanged != null) _controller.TrackChanged -= _onTrackChanged;
            if (_onVolumeChanged != null) _controller.VolumeChanged -= _onVolumeChanged;
            if (_onStateChanged != null) _controller.StateChanged -= _onStateChanged;
            if (_onProgressTick != null) _controller.ProgressTick -= _onProgressTick;
            _onTrackChanged = null; _onVolumeChanged = null;
            _onStateChanged = null; _onProgressTick = null;
        }

        void HookEvents()
        {
            _onTrackChanged = info =>
            {
                if (info == null) return;
                var cover = info.Cover ?? MainWindow.PeterBackdrop();

                if (Theme.AnimationsEnabled)
                {
                    // Мягкий и плавный визуальный кроссфейд названия, автора и обложки
                    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(110))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    fadeOut.Completed += (s, e) =>
                    {
                        _titleTb.Text = info.Title;
                        _artistTb.Text = info.Artist;
                        _coverImg.Source = cover;
                        _compactCoverImg.Source = cover;
                        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        _titleTb.BeginAnimation(OpacityProperty, fadeIn);
                        _artistTb.BeginAnimation(OpacityProperty, fadeIn);
                        _coverImg.BeginAnimation(OpacityProperty, fadeIn);
                        _compactCoverImg.BeginAnimation(OpacityProperty, fadeIn);
                    };
                    _titleTb.BeginAnimation(OpacityProperty, fadeOut);
                    _artistTb.BeginAnimation(OpacityProperty, fadeOut);
                    _coverImg.BeginAnimation(OpacityProperty, fadeOut);
                    _compactCoverImg.BeginAnimation(OpacityProperty, fadeOut);
                }
                else
                {
                    _titleTb.Text = info.Title;
                    _artistTb.Text = info.Artist;
                    _coverImg.Source = cover;
                    _compactCoverImg.Source = cover;
                }
            };
            _controller.TrackChanged += _onTrackChanged;

            _onVolumeChanged = vol =>
            {
                UpdateVolumeVisual(vol);
            };
            _controller.VolumeChanged += _onVolumeChanged;

            _onStateChanged = () =>
            {
                try
                {
                    if (_controller.IsActive)
                    {
                        ShowWidget();
                        string icData = _controller.IsPlaying ? Icons.Pause : Icons.Play;
                        UI.UpdateIcon(_playPauseIcon, icData, Theme.BrOnAccent);
                        UI.UpdateIcon(_compactPlayIcon, icData, Theme.BrOnAccent);
                        UpdateShuffleVisual();
                    }
                    else
                    {
                        HideWidget();
                    }
                }
                catch { }
            };
            _controller.StateChanged += _onStateChanged;

            _onProgressTick = (current, total) =>
            {
                if (!_isUserDraggingSeek)
                {
                    double totalSec = total.TotalSeconds;
                    double currSec = current.TotalSeconds;
                    if (totalSec > 0)
                    {
                        double trackW = _seekContainer.ActualWidth;
                        double ratio = Math.Max(0.0, Math.Min(1.0, currSec / totalSec));
                        double knobSize = _seekKnob.Width > 0 ? _seekKnob.Width : 8;
                        double usableW = Math.Max(1, trackW - knobSize);
                        double left = ratio * usableW;
                        _seekFill.Width = left + (knobSize / 2);
                        _seekKnobTrans.X = left;
                    }
                    _timeElapsedTb.Text = FmtTime(current);
                    _timeTotalTb.Text = FmtTime(total);
                }
            };
            _controller.ProgressTick += _onProgressTick;
        }

        static string FmtTime(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return string.Format("{0}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
        }

        public void ShowWidget()
        {
            if (Visibility == Visibility.Visible && Opacity == 1) return;
            Visibility = Visibility.Visible;
            if (Theme.AnimationsEnabled)
            {
                var trans = new TranslateTransform(0, 10);
                RenderTransform = trans;
                trans.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            }
            else
            {
                RenderTransform = null;
                Opacity = 1;
            }
        }

        public void HideWidget()
        {
            if (Visibility == Visibility.Collapsed) return;
            if (Theme.AnimationsEnabled)
            {
                var trans = new TranslateTransform(0, 0);
                RenderTransform = trans;
                trans.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(130)));
                var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
                anim.Completed += (s, e) => { Visibility = Visibility.Collapsed; };
                BeginAnimation(OpacityProperty, anim);
            }
            else
            {
                Opacity = 0;
                Visibility = Visibility.Collapsed;
            }
        }
    }
}

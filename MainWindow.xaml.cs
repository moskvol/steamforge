using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SteamForge
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _matrixTimer = new();
        private readonly Random _rnd = new();
        private readonly List<MatrixColumn> _columns = new();
        private const string Chars = "01010101ABCDEFｦｱｳｴｵｶｷｹｺｻｼｽｾｿﾀﾂﾃﾅﾆﾇﾈﾊﾋﾎﾏﾐﾑﾒﾓﾔﾕﾗﾘﾜ123456789";

        public MainWindow()
        {
            InitializeComponent();
            var vm = new MainVM();
            DataContext = vm;

            Loaded += (s, e) =>
            {
                InitMatrixRain();
                vm.StartHackerBootSequence();
            };
        }

        private void InitMatrixRain()
        {
            int colWidth = 18;
            int totalCols = (int)(SystemParameters.PrimaryScreenWidth / colWidth);
            _columns.Clear();
            MatrixCanvas.Children.Clear();

            for (int i = 0; i < totalCols; i++)
            {
                var col = new MatrixColumn
                {
                    X = i * colWidth,
                    Y = _rnd.Next(-600, 0),
                    Speed = _rnd.Next(6, 18),
                    TextElement = new TextBlock
                    {
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromArgb(160, 0, 255, 102)),
                        TextWrapping = TextWrapping.Wrap,
                        Width = 16
                    }
                };
                Canvas.SetLeft(col.TextElement, col.X);
                Canvas.SetTop(col.TextElement, col.Y);
                MatrixCanvas.Children.Add(col.TextElement);
                _columns.Add(col);
            }

            _matrixTimer.Interval = TimeSpan.FromMilliseconds(40);
            _matrixTimer.Tick += (s, e) =>
            {
                if (DataContext is MainVM vm && !vm.IsHackerMode) return;

                double height = ActualHeight > 0 ? ActualHeight : 800;
                foreach (var c in _columns)
                {
                    c.Y += c.Speed;
                    if (c.Y > height)
                    {
                        c.Y = _rnd.Next(-200, 0);
                        c.Speed = _rnd.Next(6, 18);
                    }
                    Canvas.SetTop(c.TextElement, c.Y);

                    if (_rnd.Next(0, 3) == 0)
                    {
                        char nextChar = Chars[_rnd.Next(Chars.Length)];
                        c.TextElement.Text = nextChar + "\n" + (c.TextElement.Text.Length > 20 ? c.TextElement.Text.Substring(0, 18) : c.TextElement.Text);
                    }
                }
            };
            _matrixTimer.Start();
        }

        private class MatrixColumn
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Speed { get; set; }
            public TextBlock TextElement { get; set; } = null!;
        }
    }

    public class EnabledToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? 1.0 : 0.45;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EnabledToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            (value is bool b && b) ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RegionalPriceItem
    {
        public string Region { get; set; } = "";
        public string Currency { get; set; } = "";
        public string Price { get; set; } = "-";
        public string Discount { get; set; } = "0%";
    }

    public class SteamSearchResultItem
    {
        public int AppId { get; set; }
        public string Name { get; set; } = "";
        public string Price { get; set; } = "";
        public string TinyImage { get; set; } = "";
        public string HeaderImage => $"https://cdn.akamai.steamstatic.com/steam/apps/{AppId}/header.jpg";
    }

    public class DlcItemModel : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        public int DlcId { get; set; }
        public string Title { get; set; } = "";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class GameCardModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private int _dlcCount;
        private BitmapImage? _poster;

        public int AppId { get; set; }
        public string Title { get; set; } = "";
        public string LuaFilePath { get; set; } = "";
        public string InstallDirName { get; set; } = "";

        public int DlcCount
        {
            get => _dlcCount;
            set
            {
                _dlcCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DlcText));
            }
        }

        public string DlcText => DlcCount > 0 ? $"{DlcCount} DLC" : "Базовый манифест";
        public string PosterUrl => $"https://cdn.akamai.steamstatic.com/steam/apps/{AppId}/header.jpg";

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBadgeText));
                OnPropertyChanged(nameof(ToggleActionText));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }

        public string StatusBadgeText => IsEnabled ? "● ВКЛЮЧЕН" : "○ ОТКЛЮЧЕН";
        public string ToggleActionText => IsEnabled ? "⏸ Отключить манифест (.disabled)" : "▶ Включить манифест (.lua)";

        public SolidColorBrush CardBorderBrush => IsEnabled
            ? new SolidColorBrush(Color.FromArgb(180, 0, 255, 102))
            : new SolidColorBrush(Color.FromArgb(120, 239, 68, 68));

        public BitmapImage? Poster
        {
            get => _poster;
            set { _poster = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DlcItemModel> DlcList { get; } = new();
        public ObservableCollection<RegionalPriceItem> RegionalPrices { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainVM : INotifyPropertyChanged
    {
        private static readonly HttpClient _http;
        private string _searchFilter = "";
        private string _statusText = "[СИСТЕМА] SteamForge 2.0 готов к работе";
        private string _steamPath = "";
        private string _storeSearchInput = "";
        private bool _isGameMatrixOpen;
        private bool _isSearchingStore;
        private int _currentViewIndex = 0;
        private GameCardModel? _selectedGame;
        private string _rawLuaContent = "";
        private int _matrixTab = 0;

        private bool _isOpenSteamToolInstalled;
        private string _openSteamToolStatus = "Проверка...";
        private bool _enableOnlineFix;
        private string _logLevel = "info";

        private bool _isHackerMode = true;
        private SolidColorBrush _windowBg = new(Color.FromRgb(2, 5, 2));
        private SolidColorBrush _sidebarBg = new(Color.FromArgb(234, 2, 8, 3));
        private SolidColorBrush _cardBg = new(Color.FromArgb(204, 2, 9, 4));
        private SolidColorBrush _accentColor = new(Color.FromRgb(0, 255, 102));
        private SolidColorBrush _textPrimary = new(Color.FromRgb(0, 255, 102));
        private SolidColorBrush _borderBrush = new(Color.FromRgb(0, 136, 45));
        private FontFamily _currentFont = new("Consolas");

        private bool _isBooting = true;
        private string _bootLog = "";
        private int _bootProgress = 0;

        static MainVM()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public ObservableCollection<GameCardModel> AllGames { get; } = new();
        public ObservableCollection<GameCardModel> DisplayedGames { get; } = new();
        public ObservableCollection<SteamSearchResultItem> StoreSearchResults { get; } = new();

        public MainVM()
        {
            ApplyTheme();
            DetectSteamPath();
            CheckOpenSteamToolStatus();
            ScanLuaManifests();
        }

        public bool IsHackerMode
        {
            get => _isHackerMode;
            set
            {
                _isHackerMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeToggleTitle));
                ApplyTheme();
            }
        }

        public string ThemeToggleTitle => IsHackerMode ? "🟢 Тема: Матрица (Хакер)" : "🔵 Тема: Легаси (Cyber)";

        public SolidColorBrush WindowBg { get => _windowBg; set { _windowBg = value; OnPropertyChanged(); } }
        public SolidColorBrush SidebarBg { get => _sidebarBg; set { _sidebarBg = value; OnPropertyChanged(); } }
        public SolidColorBrush CardBg { get => _cardBg; set { _cardBg = value; OnPropertyChanged(); } }
        public SolidColorBrush AccentColor { get => _accentColor; set { _accentColor = value; OnPropertyChanged(); } }
        public SolidColorBrush TextPrimary { get => _textPrimary; set { _textPrimary = value; OnPropertyChanged(); } }
        public SolidColorBrush BorderBrushColor { get => _borderBrush; set { _borderBrush = value; OnPropertyChanged(); } }
        public FontFamily CurrentFont { get => _currentFont; set { _currentFont = value; OnPropertyChanged(); } }

        public bool IsOpenSteamToolInstalled
        {
            get => _isOpenSteamToolInstalled;
            set { _isOpenSteamToolInstalled = value; OnPropertyChanged(); }
        }

        public string OpenSteamToolStatus
        {
            get => _openSteamToolStatus;
            set { _openSteamToolStatus = value; OnPropertyChanged(); }
        }

        public bool EnableOnlineFix
        {
            get => _enableOnlineFix;
            set { _enableOnlineFix = value; OnPropertyChanged(); }
        }

        public string LogLevel
        {
            get => _logLevel;
            set { _logLevel = value; OnPropertyChanged(); }
        }

        public void ApplyTheme()
        {
            if (IsHackerMode)
            {
                WindowBg = new SolidColorBrush(Color.FromRgb(2, 5, 2));
                SidebarBg = new SolidColorBrush(Color.FromArgb(234, 2, 8, 3));
                CardBg = new SolidColorBrush(Color.FromArgb(204, 2, 9, 4));
                AccentColor = new SolidColorBrush(Color.FromRgb(0, 255, 102));
                TextPrimary = new SolidColorBrush(Color.FromRgb(0, 255, 102));
                BorderBrushColor = new SolidColorBrush(Color.FromRgb(0, 136, 45));
                CurrentFont = new FontFamily("Consolas");
            }
            else
            {
                WindowBg = new SolidColorBrush(Color.FromRgb(7, 10, 19));
                SidebarBg = new SolidColorBrush(Color.FromArgb(240, 11, 16, 29));
                CardBg = new SolidColorBrush(Color.FromArgb(230, 14, 22, 40));
                AccentColor = new SolidColorBrush(Color.FromRgb(2, 132, 199));
                TextPrimary = new SolidColorBrush(Color.FromRgb(248, 250, 252));
                BorderBrushColor = new SolidColorBrush(Color.FromRgb(30, 42, 66));
                CurrentFont = new FontFamily("Segoe UI");
            }
        }

        public RelayCommand ToggleThemeCommand => new(() =>
        {
            IsHackerMode = !IsHackerMode;
            StatusText = IsHackerMode ? "[Тема] Включен режим Матрицы" : "[Тема] Включен Легаси Cyber режим";
        });

        public bool IsBooting
        {
            get => _isBooting;
            set { _isBooting = value; OnPropertyChanged(); }
        }

        public string BootLog
        {
            get => _bootLog;
            set { _bootLog = value; OnPropertyChanged(); }
        }

        public int BootProgress
        {
            get => _bootProgress;
            set { _bootProgress = value; OnPropertyChanged(); }
        }

        public async void StartHackerBootSequence()
        {
            IsBooting = true;
            BootLog = "";
            string[] logs =
            {
                ">> Инициализация ядра SteamForge 2.0...",
                ">> Проверка модуля OpenSteamTool...",
                ">> Инициализация хуков и API Valve...",
                ">> Сканирование манифестов stplug-in и config/lua...",
                ">> СИСТЕМА ГОТОВА К РАБОТЕ!"
            };

            for (int i = 0; i < logs.Length; i++)
            {
                BootLog += logs[i] + "\n";
                BootProgress = (int)(((i + 1) / (float)logs.Length) * 100);
                await Task.Delay(130);
            }

            await Task.Delay(200);
            IsBooting = false;
        }

        public int CurrentViewIndex
        {
            get => _currentViewIndex;
            set
            {
                _currentViewIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLibraryView));
                OnPropertyChanged(nameof(IsStoreSearchView));
                OnPropertyChanged(nameof(IsOpenSteamToolView));
                OnPropertyChanged(nameof(IsSettingsView));
            }
        }

        public bool IsLibraryView => CurrentViewIndex == 0;
        public bool IsStoreSearchView => CurrentViewIndex == 1;
        public bool IsOpenSteamToolView => CurrentViewIndex == 2;
        public bool IsSettingsView => CurrentViewIndex == 3;

        public int MatrixTab
        {
            get => _matrixTab;
            set
            {
                _matrixTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMatrixDlcTab));
                OnPropertyChanged(nameof(IsMatrixPricesTab));
                OnPropertyChanged(nameof(IsMatrixLuaTab));
            }
        }

        public bool IsMatrixDlcTab => MatrixTab == 0;
        public bool IsMatrixPricesTab => MatrixTab == 1;
        public bool IsMatrixLuaTab => MatrixTab == 2;

        public string RawLuaContent
        {
            get => _rawLuaContent;
            set { _rawLuaContent = value; OnPropertyChanged(); }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                _searchFilter = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        public string StoreSearchInput
        {
            get => _storeSearchInput;
            set { _storeSearchInput = value; OnPropertyChanged(); }
        }

        public bool IsSearchingStore
        {
            get => _isSearchingStore;
            set { _isSearchingStore = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string SteamPath
        {
            get => _steamPath;
            set
            {
                _steamPath = value;
                OnPropertyChanged();
                CheckOpenSteamToolStatus();
                ScanLuaManifests();
            }
        }

        public bool IsGameMatrixOpen
        {
            get => _isGameMatrixOpen;
            set { _isGameMatrixOpen = value; OnPropertyChanged(); }
        }

        public GameCardModel? SelectedGame
        {
            get => _selectedGame;
            set { _selectedGame = value; OnPropertyChanged(); }
        }

        private void DetectSteamPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string path)
                {
                    SteamPath = path.Replace('/', '\\');
                    StatusText = $"[Steam] Папка найдена: {SteamPath}";
                    return;
                }
            }
            catch { }

            SteamPath = @"C:\Program Files (x86)\Steam";
        }

        public void CheckOpenSteamToolStatus()
        {
            if (string.IsNullOrWhiteSpace(SteamPath) || !Directory.Exists(SteamPath))
            {
                IsOpenSteamToolInstalled = false;
                OpenSteamToolStatus = "Папка Steam не найдена";
                return;
            }

            string ostDll = Path.Combine(SteamPath, "OpenSteamTool.dll");
            string xinputDll = Path.Combine(SteamPath, "xinput1_4.dll");
            string dwmDll = Path.Combine(SteamPath, "dwmapi.dll");
            string versionDll = Path.Combine(SteamPath, "version.dll");

            if (File.Exists(ostDll) || File.Exists(xinputDll) || File.Exists(dwmDll) || File.Exists(versionDll))
            {
                IsOpenSteamToolInstalled = true;
                OpenSteamToolStatus = "УСТАНОВЛЕН И АКТИВЕН (Библиотеки подключены)";
            }
            else
            {
                IsOpenSteamToolInstalled = false;
                OpenSteamToolStatus = "НЕ УСТАНОВЛЕН (Файлы хука отсутствуют)";
            }
        }

        public string GetPluginDirectory()
        {
            string ostDir = Path.Combine(SteamPath, "config", "lua");
            string legacyDir = Path.Combine(SteamPath, "steamapps", "stplug-in");

            if (Directory.Exists(ostDir)) return ostDir;
            if (!Directory.Exists(legacyDir)) Directory.CreateDirectory(legacyDir);
            return legacyDir;
        }

        public string GetSteamAppsDirectory() => Path.Combine(SteamPath, "steamapps");

        public void ScanLuaManifests()
        {
            try
            {
                AllGames.Clear();
                DisplayedGames.Clear();

                var searchDirs = new List<string>
                {
                    Path.Combine(SteamPath, "steamapps", "stplug-in"),
                    Path.Combine(SteamPath, "config", "lua"),
                    Path.Combine(SteamPath, "OpenSteamTool", "lua")
                };

                var files = new List<string>();
                foreach (var d in searchDirs)
                {
                    if (Directory.Exists(d))
                    {
                        files.AddRange(Directory.GetFiles(d, "*.lua*"));
                    }
                }

                StatusText = $"[Сканер] Найдено манифестов: {files.Count}";

                foreach (var file in files)
                {
                    string filename = Path.GetFileName(file);
                    bool isEnabled = !filename.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                    string rawName = isEnabled ? filename.Replace(".lua", "") : filename.Replace(".lua.disabled", "");

                    if (int.TryParse(rawName, out int appId))
                    {
                        var card = new GameCardModel
                        {
                            AppId = appId,
                            Title = $"Игра {appId}",
                            LuaFilePath = file,
                            IsEnabled = isEnabled
                        };

                        AllGames.Add(card);
                        DisplayedGames.Add(card);

                        Task.Run(() => FetchMetadataAsync(card));
                    }
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка сканера] {ex.Message}";
            }
        }

        private async Task FetchMetadataAsync(GameCardModel game)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(game.PosterUrl);
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                Application.Current.Dispatcher.Invoke(() => game.Poster = bmp);
            }
            catch { }

            try
            {
                string url = $"https://store.steampowered.com/api/appdetails?appids={game.AppId}&l=russian";
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(game.AppId.ToString(), out var root) && root.GetProperty("success").GetBoolean())
                {
                    var data = root.GetProperty("data");
                    string name = data.GetProperty("name").GetString() ?? $"Игра {game.AppId}";

                    var dlcs = new List<int>();
                    if (data.TryGetProperty("dlc", out var arr))
                    {
                        foreach (var d in arr.EnumerateArray()) dlcs.Add(d.GetInt32());
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        game.Title = name;
                        game.InstallDirName = name.Replace(":", "").Replace("/", "").Trim();
                        game.DlcCount = dlcs.Count;
                        game.DlcList.Clear();
                        foreach (var d in dlcs)
                        {
                            game.DlcList.Add(new DlcItemModel { DlcId = d, Title = $"DLC #{d}", IsSelected = true });
                        }
                    });
                }
            }
            catch { }
        }

        public async Task LoadRegionalPricesAsync(GameCardModel game)
        {
            var regions = new[]
            {
                ("Россия (RU)", "ru", "RUB"),
                ("Казахстан (KZ)", "kz", "KZT"),
                ("Турция / CIS (TR)", "tr", "USD"),
                ("Украина (UA)", "ua", "UAH"),
                ("США (US)", "us", "USD")
            };

            Application.Current.Dispatcher.Invoke(() => game.RegionalPrices.Clear());

            foreach (var (title, cc, curr) in regions)
            {
                try
                {
                    string url = $"https://store.steampowered.com/api/appdetails?appids={game.AppId}&cc={cc}&filters=price_overview";
                    var json = await _http.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty(game.AppId.ToString(), out var root) && root.GetProperty("success").GetBoolean())
                    {
                        var data = root.GetProperty("data");
                        if (data.TryGetProperty("price_overview", out var po))
                        {
                            string finalFormatted = po.GetProperty("final_formatted").GetString() ?? "-";
                            int discount = po.GetProperty("discount_percent").GetInt32();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                game.RegionalPrices.Add(new RegionalPriceItem
                                {
                                    Region = title,
                                    Currency = curr,
                                    Price = finalFormatted,
                                    Discount = discount > 0 ? $"-{discount}%" : "0%"
                                });
                            });
                        }
                    }
                }
                catch { }
            }
        }

        private void ApplyFilter()
        {
            string q = SearchFilter.Trim().ToLowerInvariant();
            DisplayedGames.Clear();
            foreach (var g in AllGames)
            {
                if (string.IsNullOrEmpty(q) || g.Title.ToLowerInvariant().Contains(q) || g.AppId.ToString().Contains(q))
                {
                    DisplayedGames.Add(g);
                }
            }
        }

        public async Task SearchSteamStoreAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;

            IsSearchingStore = true;
            StatusText = $"[Поиск] Отправка запроса в Steam: \"{term}\"...";
            StoreSearchResults.Clear();

            try
            {
                string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(term)}&l=russian&cc=RU";
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var it in items.EnumerateArray())
                    {
                        int id = it.GetProperty("id").GetInt32();
                        string name = it.GetProperty("name").GetString() ?? "";
                        string tiny = it.TryGetProperty("tiny_image", out var ti) ? ti.GetString() ?? "" : "";

                        string priceStr = "Бесплатно";
                        if (it.TryGetProperty("price", out var priceObj) && priceObj.ValueKind == JsonValueKind.Object)
                        {
                            if (priceObj.TryGetProperty("final", out var finalVal))
                            {
                                int cents = finalVal.GetInt32();
                                priceStr = cents == 0 ? "Бесплатно" : $"{(cents / 100.0):N0} руб.";
                            }
                        }

                        StoreSearchResults.Add(new SteamSearchResultItem
                        {
                            AppId = id,
                            Name = name,
                            Price = priceStr,
                            TinyImage = tiny
                        });
                    }
                }

                StatusText = $"[Поиск] Найдено совпадений: {StoreSearchResults.Count}";
                CurrentViewIndex = 1;
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка поиска] {ex.Message}";
            }
            finally
            {
                IsSearchingStore = false;
            }
        }

        public RelayCommand SwitchToDlcTab => new(() => MatrixTab = 0);
        public RelayCommand SwitchToPricesTab => new(() =>
        {
            MatrixTab = 1;
            if (SelectedGame != null) Task.Run(() => LoadRegionalPricesAsync(SelectedGame));
        });
        public RelayCommand SwitchToLuaTab => new(() =>
        {
            MatrixTab = 2;
            if (SelectedGame != null && File.Exists(SelectedGame.LuaFilePath))
            {
                RawLuaContent = File.ReadAllText(SelectedGame.LuaFilePath);
            }
        });

        public RelayCommand GenerateAppManifestCommand => new(async () =>
        {
            if (SelectedGame == null) return;
            try
            {
                string steamapps = GetSteamAppsDirectory();
                string acfPath = Path.Combine(steamapps, $"appmanifest_{SelectedGame.AppId}.acf");
                string folderName = string.IsNullOrWhiteSpace(SelectedGame.InstallDirName) ? SelectedGame.Title : SelectedGame.InstallDirName;
                folderName = folderName.Replace(":", "").Trim();

                string acfContent = 
$@"""AppState""
{{
    ""appid""        ""{SelectedGame.AppId}""
    ""Universe""     ""1""
    ""name""         ""{SelectedGame.Title}""
    ""installdir""   ""{folderName}""
    ""StateFlags""   ""4""
    ""FullValidateBeforeNextRun"" ""0""
    ""BytesToDownload"" ""1000000000""
    ""BytesDownloaded"" ""1000000000""
    ""AutoUpdateBehavior"" ""0""
}}";

                await File.WriteAllTextAsync(acfPath, acfContent);
                StatusText = $"[ACF] Сгенерирован appmanifest_{SelectedGame.AppId}.acf";
                MessageBox.Show($"Файл манифеста appmanifest_{SelectedGame.AppId}.acf создан в папке steamapps!\nНажмите кнопку «Перезапустить Steam».", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка ACF] {ex.Message}";
            }
        });

        public RelayCommand GenerateSmokeApiConfigCommand => new(async () =>
        {
            if (SelectedGame == null) return;
            try
            {
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string configPath = Path.Combine(desk, $"smokeapi_{SelectedGame.AppId}.json");

                var dlcDict = new Dictionary<string, string>();
                foreach (var d in SelectedGame.DlcList)
                {
                    if (d.IsSelected) dlcDict[d.DlcId.ToString()] = d.Title;
                }

                var rootObj = new
                {
                    appid = SelectedGame.AppId,
                    logging = false,
                    dlcs = dlcDict
                };

                string jsonStr = JsonSerializer.Serialize(rootObj, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configPath, jsonStr);

                StatusText = $"[SmokeAPI] Конфиг сохранен на Рабочий стол: {Path.GetFileName(configPath)}";
                MessageBox.Show($"Файл конфигурации сохранен на Рабочий стол:\n{configPath}", "Конфиг готов", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка SmokeAPI] {ex.Message}";
            }
        });

        public RelayCommand InstallOpenSteamToolCommand => new(async () =>
        {
            try
            {
                string targetDir = SteamPath;
                if (!Directory.Exists(targetDir))
                {
                    MessageBox.Show("Папка Steam не найдена! Укажите путь в настройках.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string luaDir = Path.Combine(targetDir, "config", "lua");
                if (!Directory.Exists(luaDir)) Directory.CreateDirectory(luaDir);

                string tomlPath = Path.Combine(targetDir, "opensteamtool.toml");
                string tomlContent = 
$@"[log]
level = ""{LogLevel}""

[manifest]
remote_source = ""jsdelivr""

[features]
family_sharing_bypass = true
onlinefix = {(EnableOnlineFix ? "true" : "false")}
";
                await File.WriteAllTextAsync(tomlPath, tomlContent);

                CheckOpenSteamToolStatus();
                StatusText = "[OpenSteamTool] Директория config/lua и opensteamtool.toml настроены";
                MessageBox.Show("OpenSteamTool подготовлен!\nПапка скриптов: Steam\\config\\lua\\\nКонфиг: opensteamtool.toml", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка OpenSteamTool] {ex.Message}";
            }
        });

        public RelayCommand UninstallOpenSteamToolCommand => new(() =>
        {
            try
            {
                var res = MessageBox.Show("Удалить компоненты OpenSteamTool из директории Steam?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;

                string[] files = { "OpenSteamTool.dll", "xinput1_4.dll", "dwmapi.dll", "version.dll", "opensteamtool.toml" };
                foreach (var f in files)
                {
                    string p = Path.Combine(SteamPath, f);
                    if (File.Exists(p)) File.Delete(p);
                }

                CheckOpenSteamToolStatus();
                StatusText = "[OpenSteamTool] Компоненты удалены";
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка удаления] {ex.Message}";
            }
        });

        public RelayCommand SaveOstConfigCommand => new(async () =>
        {
            try
            {
                string tomlPath = Path.Combine(SteamPath, "opensteamtool.toml");
                string tomlContent = 
$@"[log]
level = ""{LogLevel}""

[manifest]
remote_source = ""jsdelivr""

[features]
family_sharing_bypass = true
onlinefix = {(EnableOnlineFix ? "true" : "false")}
";
                await File.WriteAllTextAsync(tomlPath, tomlContent);
                StatusText = "[OpenSteamTool] Конфиг opensteamtool.toml сохранен";
                MessageBox.Show("Файл opensteamtool.toml обновлен!", "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка TOML] {ex.Message}";
            }
        });

        public RelayCommand BrowseSteamPathCommand => new(() =>
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Выберите директорию установки Steam",
                InitialDirectory = SteamPath
            };
            if (dlg.ShowDialog() == true)
            {
                SteamPath = dlg.FolderName;
                StatusText = $"[Steam] Путь обновлен: {SteamPath}";
            }
        });

        public RelayCommand ShowLibraryCommand => new(() => CurrentViewIndex = 0);
        public RelayCommand ShowStoreSearchCommand => new(() => CurrentViewIndex = 1);
        public RelayCommand ShowOpenSteamToolCommand => new(() =>
        {
            CheckOpenSteamToolStatus();
            CurrentViewIndex = 2;
        });
        public RelayCommand ShowSettingsCommand => new(() => CurrentViewIndex = 3);

        public RelayCommand RunStoreSearchCommand => new(async () => await SearchSteamStoreAsync(StoreSearchInput));

        public RelayCommand<object> CreateManifestCommand => new(async param =>
        {
            int appId = 0;
            if (param is int directId) appId = directId;
            else if (int.TryParse(StoreSearchInput.Trim(), out int parsed)) appId = parsed;

            if (appId > 0)
            {
                string dir = GetPluginDirectory();
                string path = Path.Combine(dir, $"{appId}.lua");
                await File.WriteAllTextAsync(path, $"-- SteamForge 2.0 Manifest\naddappid({appId})\n");

                StatusText = $"[Успех] Манифест {appId}.lua записан в {Path.GetFileName(dir)}";
                StoreSearchInput = "";
                CurrentViewIndex = 0;
                ScanLuaManifests();
            }
        });

        public RelayCommand<GameCardModel> DeleteManifestCommand => new(card =>
        {
            var target = card ?? SelectedGame;
            if (target == null) return;

            var res = MessageBox.Show(
                $"Удалить файл манифеста \"{target.Title}\" ({target.AppId}.lua)?",
                "Удаление",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                string f1 = target.LuaFilePath;
                string dir = Path.GetDirectoryName(f1) ?? GetPluginDirectory();
                string f2 = Path.Combine(dir, $"{target.AppId}.lua.disabled");

                if (File.Exists(f1)) File.Delete(f1);
                if (File.Exists(f2)) File.Delete(f2);

                AllGames.Remove(target);
                DisplayedGames.Remove(target);
                StatusText = $"[Удалено] {target.AppId}.lua стерт";
                IsGameMatrixOpen = false;
            }
        });

        public RelayCommand RefreshCommand => new(ScanLuaManifests);

        public RelayCommand RestartSteamCommand => new(() =>
        {
            try
            {
                StatusText = "[Steam] Перезапуск процесса...";
                foreach (var p in Process.GetProcessesByName("steam"))
                {
                    p.Kill();
                    p.WaitForExit();
                }

                string exe = Path.Combine(SteamPath, "steam.exe");
                if (File.Exists(exe))
                {
                    var psi = new ProcessStartInfo { FileName = exe };
                    if (EnableOnlineFix) psi.Arguments = "-onlinefix";
                    Process.Start(psi);
                }
                StatusText = EnableOnlineFix ? "[Steam] Запущен с ключом -onlinefix" : "[Steam] Клиент перезапущен";
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка Steam] {ex.Message}";
            }
        });

        public RelayCommand CreateBackupCommand => new(() =>
        {
            try
            {
                string pluginDir = GetPluginDirectory();
                string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string zipPath = Path.Combine(desk, $"SteamForge_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                ZipFile.CreateFromDirectory(pluginDir, zipPath);
                StatusText = $"[Бэкап] Сохранен: {Path.GetFileName(zipPath)}";
            }
            catch (Exception ex)
            {
                StatusText = $"[Ошибка бэкапа] {ex.Message}";
            }
        });

        public RelayCommand<GameCardModel> OpenGameMatrixCommand => new(game =>
        {
            SelectedGame = game;
            MatrixTab = 0;
            IsGameMatrixOpen = true;
        });

        public RelayCommand CloseMatrixCommand => new(() => IsGameMatrixOpen = false);

        public RelayCommand ToggleManifestStateCommand => new(() =>
        {
            if (SelectedGame == null) return;
            string dir = Path.GetDirectoryName(SelectedGame.LuaFilePath) ?? GetPluginDirectory();
            string normal = Path.Combine(dir, $"{SelectedGame.AppId}.lua");
            string disabled = Path.Combine(dir, $"{SelectedGame.AppId}.lua.disabled");

            if (SelectedGame.IsEnabled)
            {
                if (File.Exists(normal)) File.Move(normal, disabled, true);
                SelectedGame.IsEnabled = false;
                SelectedGame.LuaFilePath = disabled;
                StatusText = $"[Статус] {SelectedGame.AppId}.lua отключен (.disabled)";
            }
            else
            {
                if (File.Exists(disabled)) File.Move(disabled, normal, true);
                SelectedGame.IsEnabled = true;
                SelectedGame.LuaFilePath = normal;
                StatusText = $"[Статус] {SelectedGame.AppId}.lua включен";
            }
        });

        public RelayCommand SaveMatrixCommand => new(async () =>
        {
            if (SelectedGame == null) return;
            string path = SelectedGame.LuaFilePath;

            if (IsMatrixLuaTab)
            {
                await File.WriteAllTextAsync(path, RawLuaContent);
                StatusText = $"[Редактор] Манифест {SelectedGame.AppId}.lua сохранен!";
            }
            else
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"-- SteamForge 2.0 / OpenSteamTool");
                sb.AppendLine($"-- Игра: {SelectedGame.Title} [{SelectedGame.AppId}]");
                sb.AppendLine($"addappid({SelectedGame.AppId})");

                int activeCount = 0;
                foreach (var dlc in SelectedGame.DlcList)
                {
                    if (dlc.IsSelected)
                    {
                        sb.AppendLine($"addappid({dlc.DlcId}) -- {dlc.Title}");
                        activeCount++;
                    }
                }

                await File.WriteAllTextAsync(path, sb.ToString());
                SelectedGame.DlcCount = activeCount;
                StatusText = $"[Манифест] Сохранен {SelectedGame.AppId}.lua ({activeCount} DLC)";
            }

            IsGameMatrixOpen = false;
        });

        public RelayCommand<string> OpenUrlCommand => new(url =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        });

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
        public event EventHandler? CanExecuteChanged;
    }

    public class RelayCommand<T> : System.Windows.Input.ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            if (parameter is T val) _execute(val);
        }
        public event EventHandler? CanExecuteChanged;
    }
}
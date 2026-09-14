using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LuaToolsApp
{
    public class SteamSearchResult
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private string _currentAppId = "239140";
        private string _currentAppTitle = "Dying Light";

        public MainWindow()
        {
            InitializeComponent();
            ApplyTimeGreeting();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Resources["WindowFadeIn"] is Storyboard fadeIn)
            {
                fadeIn.Begin(this);
            }

            Log("[Studio Engine] SteamForge Studio v2.5 активирован.");
            Log($"[System] ОС: {RuntimeInformation.OSDescription}");
            Log($"[Path] Steam: {GetSteamDirectory()}");
            Log($"[Path] stplug-in: {GetPluginDirectory()}");
        }

        private void ApplyTimeGreeting()
        {
            int hour = DateTime.Now.Hour;
            string greeting = hour switch
            {
                >= 5 and < 12 => "Доброе утро! ☀️",
                >= 12 and < 18 => "Добрый день! ⚡",
                >= 18 and < 23 => "Добрый вечер! 🌌",
                _ => "Доброй ночи! 🌙"
            };
            TxtGreeting.Text = $"{greeting} Рады видеть вас в SteamForge";
        }

        // --- Переключение между Экранами (Генератор / Настройки) ---
        private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewGenerator.Visibility = Visibility.Collapsed;
            ViewSettings.Visibility = Visibility.Visible;

            BtnNavSettings.Visibility = Visibility.Collapsed;
            BtnNavBack.Visibility = Visibility.Visible;
            Log("[Навигация] Открыт раздел настроек.");
        }

        private void BtnNavBack_Click(object sender, RoutedEventArgs e)
        {
            ViewSettings.Visibility = Visibility.Collapsed;
            ViewGenerator.Visibility = Visibility.Visible;

            BtnNavBack.Visibility = Visibility.Collapsed;
            BtnNavSettings.Visibility = Visibility.Visible;
            Log("[Навигация] Возврат к генератору манифестов.");
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LstLogs.Items.Add($"[{time}] {message}");
            if (LstLogs.Items.Count > 0)
            {
                LstLogs.ScrollIntoView(LstLogs.Items[LstLogs.Items.Count - 1]);
            }
        }

        private string GetSteamDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string nativeSteam = Path.Combine(home, ".local", "share", "Steam");
                if (Directory.Exists(nativeSteam)) return nativeSteam;

                string flatpakSteam = Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam");
                if (Directory.Exists(flatpakSteam)) return flatpakSteam;

                return nativeSteam;
            }
            else
            {
                return @"C:\Program Files (x86)\Steam";
            }
        }

        private string GetPluginDirectory()
        {
            string steamDir = GetSteamDirectory();
            string pluginDir = Path.Combine(steamDir, "steamapps", "stplug-in");

            if (!Directory.Exists(pluginDir))
            {
                try { Directory.CreateDirectory(pluginDir); }
                catch (Exception ex) { Log($"[Ошибка папки] {ex.Message}"); }
            }

            return pluginDir;
        }

        private async Task<List<SteamSearchResult>> SearchSteamStoreAsync(string query)
        {
            var results = new List<SteamSearchResult>();

            try
            {
                string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(query)}&l=english&cc=US";
                string json = await _httpClient.GetStringAsync(url);

                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("items", out var items))
                {
                    int count = 0;
                    foreach (var item in items.EnumerateArray())
                    {
                        if (count++ >= 5) break;
                        string id = item.GetProperty("id").GetInt64().ToString();
                        string name = item.GetProperty("name").GetString() ?? "Unknown";
                        results.Add(new SteamSearchResult { Id = id, Name = name });
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[Steam API Ошибка] {ex.Message}");
            }

            return results;
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteSearch();
        }

        private async void TxtGameName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await ExecuteSearch();
            }
        }

        private async Task ExecuteSearch()
        {
            string query = TxtGameName.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            BtnSearch.IsEnabled = false;
            TxtStatusBadge.Text = "● Поиск...";
            TxtStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            Log($"[Поиск] Поиск совпадений для «{query}»...");

            var results = await SearchSteamStoreAsync(query);

            if (results.Count == 0)
            {
                Log("[404] Совпадений не найдено.");
                MessageBox.Show($"По запросу «{query}» ничего не найдено!", "Поиск Steam", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtStatusBadge.Text = "● Ошибка";
                TxtStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else
            {
                LstSearchResults.ItemsSource = results;
                LstSearchResults.SelectedIndex = 0;
                TxtStatusBadge.Text = "● Результаты найдены";
                TxtStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }

            BtnSearch.IsEnabled = true;
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSearchResults.SelectedItem is SteamSearchResult selected)
            {
                _currentAppId = selected.Id;
                _currentAppTitle = selected.Name;

                TxtSelectedTitle.Text = selected.Name;
                TxtSelectedAppId.Text = $"AppID: {selected.Id}";

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri($"https://cdn.cloudflare.steamstatic.com/steam/apps/{selected.Id}/header.jpg");
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    ImgCover.Source = bmp;
                }
                catch { }

                Log($"[Выбор] Активна: {selected.Name} ({selected.Id})");
            }
        }

        private async void BtnApplyManifest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentAppId)) return;

            try
            {
                string pluginDir = GetPluginDirectory();
                string filePath = Path.Combine(pluginDir, $"{_currentAppId}.lua");

                string luaContent = $"-- SteamForge Studio Manifest\n" +
                                    $"-- Game: {_currentAppTitle}\n" +
                                    $"-- AppID: {_currentAppId}\n" +
                                    $"-- Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                    $"addappid({_currentAppId})\n";

                await File.WriteAllTextAsync(filePath, luaContent);
                Log($"[Запись] Сгенерирован stplug-in/{_currentAppId}.lua");

                PlaySuccessAnimation(_currentAppTitle);
                TxtStatusBadge.Text = "● Синхронизировано";
                TxtStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
            catch (Exception ex)
            {
                Log($"[Ошибка] {ex.Message}");
                MessageBox.Show($"Не удалось записать файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlaySuccessAnimation(string gameTitle)
        {
            ToastText.Text = $"⚡ {gameTitle} успешно добавлен!";
            if (Resources["ToastSuccessAnimation"] is Storyboard toastAnim)
            {
                toastAnim.Begin(this);
            }
        }

        // --- Управление Темами ---
        private void SetTheme(Color c1, Color c2)
        {
            if (Resources["DynamicAccentGrad"] is LinearGradientBrush grad && grad.GradientStops.Count >= 2)
            {
                grad.GradientStops[0].Color = c1;
                grad.GradientStops[1].Color = c2;
            }

            BgGlow1.Fill = new SolidColorBrush(c1);
            BgGlow2.Fill = new SolidColorBrush(c2);
            Log("[Тема] Стиль оформления переключен.");
        }

        private void ThemeViolet_Click(object sender, RoutedEventArgs e) => SetTheme(Color.FromRgb(99, 102, 241), Color.FromRgb(6, 182, 212));
        private void ThemeBlue_Click(object sender, RoutedEventArgs e) => SetTheme(Color.FromRgb(2, 132, 199), Color.FromRgb(56, 189, 248));
        private void ThemeGreen_Click(object sender, RoutedEventArgs e) => SetTheme(Color.FromRgb(5, 150, 105), Color.FromRgb(16, 185, 129));
        private void ThemeSunset_Click(object sender, RoutedEventArgs e) => SetTheme(Color.FromRgb(217, 119, 6), Color.FromRgb(239, 68, 68));

        // --- Ссылки ---
        private void OpenWebsite_Click(object sender, RoutedEventArgs e) => OpenUrl("https://moskvol.github.io");
        private void OpenGithub_Click(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/moskvol");

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Log($"[Браузер] Переход по адресу: {url}");
            }
            catch (Exception ex)
            {
                Log($"[Ошибка браузера] {ex.Message}");
            }
        }

        private void BtnRestartSteam_Click(object sender, RoutedEventArgs e)
        {
            Log("[Команда] Перезапуск клиента Steam...");
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    try { Process.Start("killall", "-9 steam")?.WaitForExit(); } catch { }
                    Process.Start(new ProcessStartInfo { FileName = "steam", UseShellExecute = true });
                }
                else
                {
                    Process.Start("taskkill", "/F /IM steam.exe")?.WaitForExit();
                    string exe = Path.Combine(GetSteamDirectory(), "steam.exe");
                    if (File.Exists(exe)) Process.Start(exe);
                }
                Log("[Успех] Сигнал перезапуска отправлен.");
            }
            catch (Exception ex) { Log($"[Ошибка] {ex.Message}"); }
        }
    }
}
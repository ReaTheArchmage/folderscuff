using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace SmartSearch
{
    public partial class MainWindow : Window
    {
        private const int HoverEdgeThreshold = 12;
        private string? searchFolderPath;
        private const string BrowserItemText = "-- Search on Browser";
        private bool _isDarkTheme = true;
        private bool _showInTaskbar = false;

        private readonly string configDirectory;
        private readonly string configFile;

        public MainWindow()
        {
            InitializeComponent();

            // Config paths
            configDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartSearch");
            configFile = Path.Combine(configDirectory, "config.txt");

            // Load previous settings
            LoadConfig();

            // Apply taskbar visibility
            this.ShowInTaskbar = _showInTaskbar;

            // First run: force folder selection
            if (string.IsNullOrEmpty(searchFolderPath) || !Directory.Exists(searchFolderPath))
                SelectSearchFolder(true);

            ApplyTheme();

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };

            SetupSearchBoxContextMenu();

            MainBorder.Opacity = 0.9;
            this.MouseEnter += (s, e) => AnimateOpacity(0.9);
            this.MouseLeave += (s, e) => AnimateOpacity(0.4);
        }

        #region Config

        private void LoadConfig()
        {
            if (!File.Exists(configFile)) return;

            var lines = File.ReadAllLines(configFile);
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "Folder":
                        if (Directory.Exists(value)) searchFolderPath = value;
                        break;
                    case "DarkTheme":
                        _isDarkTheme = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "ShowInTaskbar":
                        _showInTaskbar = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(configDirectory))
                    Directory.CreateDirectory(configDirectory);

                File.WriteAllLines(configFile, new[]
                {
                    $"Folder={searchFolderPath}",
                    $"DarkTheme={_isDarkTheme}",
                    $"ShowInTaskbar={_showInTaskbar}"
                });
            }
            catch
            {
                // fail silently
            }
        }

        #endregion

        #region Folder Selection

        private void SelectSearchFolder(bool isMandatory)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select SmartSearch Folder",
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    searchFolderPath = selectedPath;
                    SaveConfig();
                }
            }
            else if (isMandatory)
            {
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Theme

        private void ApplyTheme()
        {
            if (_isDarkTheme)
            {
                var darkMain = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFrom("#FF1E1E1E");
                var darkSub = (System.Windows.Media.Brush?)new System.Windows.Media.BrushConverter().ConvertFrom("#FF2E2E2E");

                MainBorder.Background = darkMain ?? System.Windows.Media.Brushes.Black;

                SearchBox.Background = darkSub ?? System.Windows.Media.Brushes.Gray;
                SearchBox.Foreground = System.Windows.Media.Brushes.White;
                SearchBox.CaretBrush = System.Windows.Media.Brushes.White;

                ResultsList.Background = darkSub ?? System.Windows.Media.Brushes.Gray;
                ResultsList.Foreground = System.Windows.Media.Brushes.White;

                TopmostToggle.Foreground = System.Windows.Media.Brushes.White;

                ResultsList.ItemContainerStyle = CreateListBoxItemStyle(System.Windows.Media.Brushes.White);
            }
            else
            {
                MainBorder.Background = System.Windows.Media.Brushes.White;

                SearchBox.Background = System.Windows.Media.Brushes.WhiteSmoke;
                SearchBox.Foreground = System.Windows.Media.Brushes.Black;
                SearchBox.CaretBrush = System.Windows.Media.Brushes.Black;

                ResultsList.Background = System.Windows.Media.Brushes.WhiteSmoke;
                ResultsList.Foreground = System.Windows.Media.Brushes.Black;

                TopmostToggle.Foreground = System.Windows.Media.Brushes.Black;

                ResultsList.ItemContainerStyle = CreateListBoxItemStyle(System.Windows.Media.Brushes.Black);
            }
        }

        private Style CreateListBoxItemStyle(System.Windows.Media.Brush foreground)
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, foreground));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            return style;
        }

        #endregion

        #region UI Animations

        private void AnimateOpacity(double targetOpacity)
        {
            var animation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };
            MainBorder.BeginAnimation(OpacityProperty, animation);
        }

        #endregion

        #region Context Menu

        private void SetupSearchBoxContextMenu()
        {
            var menu = new ContextMenu();

            // Open current folder
            var openFolder = new MenuItem { Header = "Open Custom Folder" };
            openFolder.Click += (_, __) =>
            {
                if (!string.IsNullOrEmpty(searchFolderPath) && Directory.Exists(searchFolderPath))
                {
                    try { Process.Start(new ProcessStartInfo(searchFolderPath) { UseShellExecute = true }); }
                    catch { System.Windows.MessageBox.Show("Unable to open folder.", "Error"); }
                }
                else
                {
                    var res = System.Windows.MessageBox.Show("Folder not set or missing. Select one?", "Folder Missing", MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.Yes) SelectSearchFolder(false);
                }
            };
            menu.Items.Add(openFolder);

            // Change folder
            var changeFolder = new MenuItem { Header = "Change Search Folder" };
            changeFolder.Click += (_, __) => SelectSearchFolder(false);
            menu.Items.Add(changeFolder);

            menu.Items.Add(new Separator());

            // Theme toggle
            var themeToggle = new MenuItem
            {
                Header = "Light Theme",
                IsCheckable = true,
                IsChecked = !_isDarkTheme
            };
            themeToggle.Checked += (_, __) => { _isDarkTheme = false; SaveConfig(); ApplyTheme(); };
            themeToggle.Unchecked += (_, __) => { _isDarkTheme = true; SaveConfig(); ApplyTheme(); };
            menu.Items.Add(themeToggle);

            // Taskbar toggle
            var taskbarToggle = new MenuItem
            {
                Header = "Show In Taskbar",
                IsCheckable = true,
                IsChecked = _showInTaskbar
            };
            taskbarToggle.Checked += (_, __) => { _showInTaskbar = true; this.ShowInTaskbar = true; SaveConfig(); };
            taskbarToggle.Unchecked += (_, __) => { _showInTaskbar = false; this.ShowInTaskbar = false; SaveConfig(); };
            menu.Items.Add(taskbarToggle);

            menu.Items.Add(new Separator());

            // Exit
            var exit = new MenuItem { Header = "Exit" };
            exit.Click += (_, __) => Application.Current.Shutdown();
            menu.Items.Add(exit);

            SearchBox.ContextMenu = menu;
        }

        #endregion

        #region Search Logic

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(searchFolderPath) || !Directory.Exists(searchFolderPath))
            {
                ResultsList.Visibility = Visibility.Collapsed;
                ResultsList.ItemsSource = null;
                return;
            }

            var exact = Directory
                .EnumerateFiles(searchFolderPath, "*", SearchOption.AllDirectories)
                .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), query, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                TryOpenFile(exact);
                ResetSearch();
                return;
            }

            string[] partials;
            try
            {
                partials = Directory
                    .EnumerateFiles(searchFolderPath, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(10)
                    .Select(Path.GetFileName)
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToArray();
            }
            catch
            {
                partials = Array.Empty<string>();
            }

            ResultsList.ItemsSource = partials.Length > 0 ? partials : new[] { BrowserItemText };
            ResultsList.SelectedIndex = 0;
            ResultsList.Visibility = Visibility.Visible;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var query = SearchBox.Text.Trim();
                if (string.IsNullOrEmpty(query)) return;

                bool browserVisible = ResultsList.Visibility == Visibility.Visible
                    && ResultsList.Items.Count == 1
                    && (ResultsList.SelectedItem as string == BrowserItemText);

                if (browserVisible)
                {
                    OpenBrowserSearch(query);
                    ResetSearch();
                }
            }
            else if (e.Key == Key.Escape)
            {
                Application.Current.Shutdown();
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsList.SelectedItem == null) return;

            var sel = ResultsList.SelectedItem as string;
            if (sel == BrowserItemText)
            {
                OpenBrowserSearch(SearchBox.Text.Trim());
            }
            else if (!string.IsNullOrEmpty(sel) && !string.IsNullOrEmpty(searchFolderPath) && Directory.Exists(searchFolderPath))
            {
                var full = Directory.EnumerateFiles(searchFolderPath, sel, SearchOption.AllDirectories).FirstOrDefault();
                if (full != null) TryOpenFile(full);
            }

            ResetSearch();
        }

        private void TryOpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { System.Windows.MessageBox.Show("Unable to open file.", "Error"); }
        }

        private void OpenBrowserSearch(string query)
        {
            var url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query);
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { System.Windows.MessageBox.Show("Unable to launch browser.", "Error"); }
        }

        private void ResetSearch()
        {
            ResultsList.Visibility = Visibility.Collapsed;
            SearchBox.Text = "";
        }

        #endregion

        #region Window Hover

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            bool edge = pos.X <= HoverEdgeThreshold || pos.X >= ActualWidth - HoverEdgeThreshold
                        || pos.Y <= HoverEdgeThreshold || pos.Y >= ActualHeight - HoverEdgeThreshold;
            Mouse.OverrideCursor = edge ? Cursors.SizeAll : null;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        #endregion

        private void TopmostToggle_Checked(object sender, RoutedEventArgs e) => this.Topmost = true;
        private void TopmostToggle_Unchecked(object sender, RoutedEventArgs e) => this.Topmost = false;
    }
}

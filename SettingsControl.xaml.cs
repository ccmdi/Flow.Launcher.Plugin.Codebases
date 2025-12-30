using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.Codebases
{
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;
        private readonly Main _plugin;
        private bool _isInitializing = true;

        public SettingsControl(Settings settings, PluginInitContext context, Main plugin)
        {
            _settings = settings;
            _context = context;
            _plugin = plugin;

            InitializeComponent();
            LoadSettings();

            _isInitializing = false;
        }

        private void LoadSettings()
        {
            // Load Editor
            foreach (ComboBoxItem item in EditorComboBox.Items)
            {
                if (item.Tag.ToString() == _settings.Editor.ToString())
                {
                    EditorComboBox.SelectedItem = item;
                    break;
                }
            }

            // Load es.exe path
            EsExePathTextBox.Text = _settings.EsExePath;

            // Load search paths (join with newlines)
            SearchPathsTextBox.Text = string.Join(Environment.NewLine, _settings.SearchPaths);

            // Load ignored directories (join with newlines)
            IgnoredDirectoriesTextBox.Text = string.Join(Environment.NewLine, _settings.IgnoredDirectories);

            // Load max results
            MaxResultsTextBox.Text = _settings.MaxResults.ToString();
        }

        private void SaveSettings()
        {
            if (_isInitializing)
                return;

            _context.API.SaveSettingJsonStorage<Settings>();
        }

        private void EditorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || EditorComboBox.SelectedItem == null)
                return;

            var selectedItem = (ComboBoxItem)EditorComboBox.SelectedItem;
            var editorString = selectedItem.Tag.ToString();

            if (Enum.TryParse<Editor>(editorString, out var editor))
            {
                _settings.Editor = editor;
                SaveSettings();
            }
        }

        private void EsExePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            _settings.EsExePath = EsExePathTextBox.Text.Trim();
            SaveSettings();
        }

        private void BrowseEsExeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Everything CLI (es.exe)",
                FileName = "es.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                EsExePathTextBox.Text = dialog.FileName;
            }
        }

        private void SearchPathsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            var paths = SearchPathsTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            _settings.SearchPaths = paths.Count > 0
                ? paths
                : new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };

            SaveSettings();
        }

        private void IgnoredDirectoriesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            var dirs = IgnoredDirectoriesTextBox.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            _settings.IgnoredDirectories = dirs;
            SaveSettings();
        }

        private void MaxResultsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            if (int.TryParse(MaxResultsTextBox.Text, out var maxResults) && maxResults > 0)
            {
                _settings.MaxResults = maxResults;
                SaveSettings();
            }
        }

        private void RebuildCacheButton_Click(object sender, RoutedEventArgs e)
        {
            RebuildCacheButton.IsEnabled = false;
            RebuildCacheButton.Content = "Rebuilding...";

            Task.Run(async () =>
            {
                try
                {
                    // Get all repo paths
                    var searchResults = _plugin.Search.Search();
                    var repoPaths = searchResults
                        .Where(r => r.Type == SearchResultType.GitRepository)
                        .Select(r => r.Path)
                        .ToList();

                    // Rebuild all
                    await _plugin.LanguageCache.RebuildAllAsync(repoPaths);

                    Dispatcher.Invoke(() =>
                    {
                        _context.API.ShowMsg("Language Cache",
                            $"Rebuilt language cache for {repoPaths.Count} repositories");
                        RebuildCacheButton.Content = "Rebuild Language Cache";
                        RebuildCacheButton.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _context.API.ShowMsg("Error", $"Failed to rebuild cache: {ex.Message}");
                        RebuildCacheButton.Content = "Rebuild Language Cache";
                        RebuildCacheButton.IsEnabled = true;
                    });
                }
            });
        }
    }
}

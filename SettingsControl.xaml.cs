using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.CodebaseFinder
{
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;
        private readonly PluginInitContext _context;
        private bool _isInitializing = true;

        public SettingsControl(Settings settings, PluginInitContext context)
        {
            _settings = settings;
            _context = context;

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
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Serilog;

namespace MochiV2.UI.Settings
{
    /// <summary>
    /// Normal WPF settings window (S-2, PRD §9.2 / DESIGN §3.S-2).
    /// 480×640 fixed, warm cream background, six sections in cards.
    /// Keyboard navigable: explicit TabIndex order, Enter = Save,
    /// Esc = Cancel. Save persists through <see cref="MochiSettings"/>;
    /// Cancel discards the working copy without writing.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private static readonly Serilog.ILogger Logger =
            Log.ForContext<SettingsWindow>();

        /// <summary>
        /// Settings committed after the last successful Save. Bound to the
        /// UI as the working copy; <see cref="_original"/> holds the
        /// pre-edit snapshot for Cancel.
        /// </summary>
        private readonly MochiSettings _working;

        /// <summary>Snapshot taken on open so Cancel can restore.</summary>
        private readonly MochiSettings _original;

        /// <summary>
        /// Constructs the settings window with default values. Caller can
        /// pass an existing <paramref name="settings"/> (e.g. loaded by
        /// SaveManager T-019) to edit the live configuration.
        /// </summary>
        /// <param name="settings">
        /// Settings to edit. A defensive copy is taken so the caller's
        /// instance is only mutated on Save. If null, defaults are used.
        /// </param>
        public SettingsWindow(MochiSettings? settings = null)
        {
            _original = settings?.Clone() ?? new MochiSettings();
            _working = _original.Clone();
            DataContext = _working;
            InitializeComponent();
            PopulateLanguages();
            HookSliderLabels();
            UpdateSliderLabels();
            Logger.Information("SettingsWindow opened (language={Lang})", _working.Language);
        }

        /// <summary>
        /// Result of the last interaction: true if the user pressed Save,
        /// false if Cancelled / closed otherwise. Useful for callers that
        /// want to know whether to re-read the settings file.
        /// </summary>
        public bool Saved { get; private set; }

        /// <summary>
        /// The committed settings after Save, or the pre-edit snapshot if
        /// cancelled. Callers should read this property rather than the
        /// constructor argument.
        /// </summary>
        public MochiSettings Result => _working;

        // ---------------------------------------------------------------
        // Setup helpers
        // ---------------------------------------------------------------

        private void PopulateLanguages()
        {
            LanguageComboBox.ItemsSource = MochiSettings.AvailableLanguages;
            // SelectedValue binding handles the code→entry mapping; ensure
            // a fallback so the dropdown always has something selected.
            if (LanguageComboBox.SelectedIndex < 0)
                LanguageComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Subscribe to slider ValueChanged so the right-aligned numeric
        /// labels stay in sync (two-way binding updates the model; these
        /// labels are plain TextBlocks for display).
        /// </summary>
        private void HookSliderLabels()
        {
            PersonalitySlider.ValueChanged += (_, _) => UpdateSliderLabels();
            VolumeSlider.ValueChanged += (_, _) => UpdateSliderLabels();
            ScaleSlider.ValueChanged += (_, _) => UpdateSliderLabels();
        }

        private void UpdateSliderLabels()
        {
            PersonalityValue.Text = $"{PersonalitySlider.Value:F2}";
            VolumeValue.Text = $"{VolumeSlider.Value * 100:F0}%";
            ScaleValue.Text = $"{ScaleSlider.Value:F2}x";
        }

        // ---------------------------------------------------------------
        // Actions
        // ---------------------------------------------------------------

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Push combobox selection back into the model in case the
            // SelectedValue binding hasn't flushed yet.
            if (LanguageComboBox.SelectedValue is string code)
                _working.Language = code;

            try
            {
                _working.SavePlaceholder(); // T-019 will replace this.
                _original.CopyFrom(_working);
                Saved = true;
                Logger.Information("Settings saved (Save placeholder; T-019 pending)");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings");
                MessageBox.Show(
                    this,
                    "Mochi couldn't save settings 😿\n" + ex.Message,
                    "Save failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Settings cancelled, discarding edits");
            _working.CopyFrom(_original);
            Saved = false;
            DialogResult = false;
            Close();
        }

        // ---------------------------------------------------------------
        // Keyboard shortcuts (DESIGN accessibility floor)
        // ---------------------------------------------------------------

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancel_Click(sender, e);
            }
            else if (e.Key == Key.Enter && !(e.OriginalSource is TextBox))
            {
                // Enter on sliders/toggles/buttons → Save. Skip when a
                // TextBox is focused so multiline editing isn't hijacked.
                e.Handled = true;
                Save_Click(sender, e);
            }
        }
    }
}
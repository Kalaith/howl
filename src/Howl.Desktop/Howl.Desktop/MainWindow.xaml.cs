using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Howl.Core.Models;
using Howl.Services;
using Howl.Services.Configuration;
using Microsoft.Win32;

namespace Howl.Desktop;

public partial class MainWindow : Window
{
    private HowlOrchestrator? _orchestrator;
    private RecordingSession? _currentSession;
    private DispatcherTimer? _recordingTimer;
    private DateTime _recordingStartTime;
    private GeminiConfiguration? _geminiConfig;
    private LMStudioConfiguration? _lmStudioConfig;
    private HttpClient? _httpClient;
    private GeminiService? _geminiService;
    private LMStudioService? _lmStudioService;

    public MainWindow()
    {
        InitializeComponent();
        InitializeServices();
    }

    private void InitializeServices()
    {
        try
        {
            // Initialize Gemini configuration
            _geminiConfig = new GeminiConfiguration();

            // Priority 1: Try to load from environment variable
            string? apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            if (!string.IsNullOrEmpty(apiKey))
            {
                _geminiConfig.ApiKey = apiKey;
                ApiKeyStatusText.Text = "✓ Gemini API Key loaded from environment variable";
                ApiKeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60"));
                ApiKeyStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                // Priority 2: Try to load from config file
                var config = ConfigurationService.LoadConfiguration();
                if (!string.IsNullOrEmpty(config.ApiKey))
                {
                    _geminiConfig.ApiKey = config.ApiKey;
                    ApiKeyStatusText.Text = $"✓ Gemini API Key loaded from config file";
                    ApiKeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60"));
                    ApiKeyStatusText.Visibility = Visibility.Visible;
                }
            }

            // Initialize LM Studio configuration (always available, no API key needed)
            _lmStudioConfig = new LMStudioConfiguration();

            // Initialize HTTP client
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };

            // Initialize other services (must be before AI services that depend on them)
            var recordingService = new ScreenRecordingService();
            var stepDetectionService = new StepDetectionService();
            var promptBuilderService = new PromptBuilderService();

            // Initialize both AI services
            _geminiService = new GeminiService(_geminiConfig, _httpClient);
            _lmStudioService = new LMStudioService(_lmStudioConfig, _httpClient, promptBuilderService);
            var htmlExportService = new HtmlExportService();
            var debugExportService = new DebugExportService();

            // Create orchestrator with LM Studio as default
            _orchestrator = new HowlOrchestrator(
                recordingService,
                stepDetectionService,
                promptBuilderService,
                _lmStudioService,
                htmlExportService,
                debugExportService
            );

            UpdateStatus("Ready to record (using LM Studio)");
            UpdateFooterForProvider();

            // Load available models from LM Studio
            _ = LoadModelsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize Howl: {ex.Message}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            StartButton.IsEnabled = false;
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_orchestrator == null)
            {
                MessageBox.Show("Services not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentSession = _orchestrator.StartRecording();
            _recordingStartTime = DateTime.Now;

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;

            UpdateStatus("Recording in progress...");
            AddProgress("Recording started. Perform your actions now.");
            AddProgress("Click anywhere on your screen to capture steps.");

            // Start timer to update recording duration
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;
            _recordingTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_orchestrator == null)
            {
                MessageBox.Show("Services not initialized", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Stop timer
            _recordingTimer?.Stop();
            _recordingTimer = null;

            StopButton.IsEnabled = false;
            UpdateStatus("Processing recording...");
            RecordingTimeTextBlock.Text = "";

            _currentSession = _orchestrator.StopRecording();

            AddProgress($"Recording stopped. Captured {_currentSession.Clicks.Count} clicks.");
            AddProgress($"Screenshots saved to: {Path.Combine(_currentSession.OutputDirectory!, "frames")}");
            AddProgress("");

            // Check if debug mode is enabled
            bool isDebugMode = DebugModeCheckBox.IsChecked == true;

            // Ask user where to save
            var saveDialog = new SaveFileDialog();

            if (isDebugMode)
            {
                saveDialog.Filter = "Text files (*.txt)|*.txt";
                saveDialog.DefaultExt = ".txt";
                saveDialog.FileName = $"howl-debug-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt";
            }
            else
            {
                saveDialog.Filter = "HTML files (*.html)|*.html|ZIP files (*.zip)|*.zip";
                saveDialog.DefaultExt = ".html";
                saveDialog.FileName = $"howl-guide-{DateTime.Now:yyyy-MM-dd-HHmmss}";
            }

            if (saveDialog.ShowDialog() == true)
            {
                if (isDebugMode)
                {
                    await ProcessAndExportDebugAsync(saveDialog.FileName);
                }
                else
                {
                    // Check if API key is configured for non-debug mode
                    if (_geminiConfig == null || string.IsNullOrWhiteSpace(_geminiConfig.ApiKey))
                    {
                        var result = MessageBox.Show(
                            "API key is required for AI mode.\n\n" +
                            "You can:\n" +
                            "1. Enter your API key in the field above, OR\n" +
                            "2. Use Debug Mode to skip AI processing\n\n" +
                            "Do you want to cancel and enter an API key?",
                            "API Key Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            StartButton.IsEnabled = true;
                            UpdateStatus("Ready to record");
                            ApiKeyBorder.Visibility = Visibility.Visible;
                            ApiKeyPasswordBox.Focus();
                            return;
                        }
                        else
                        {
                            StartButton.IsEnabled = true;
                            UpdateStatus("Ready to record");
                            return;
                        }
                    }

                    var isZip = Path.GetExtension(saveDialog.FileName).ToLower() == ".zip";
                    await ProcessAndExportAsync(saveDialog.FileName, isZip);
                }
            }
            else
            {
                UpdateStatus("Export cancelled");
                StartButton.IsEnabled = true;

                // Offer to open screenshots folder
                if (_currentSession != null && !string.IsNullOrEmpty(_currentSession.OutputDirectory))
                {
                    var screenshotsPath = Path.Combine(_currentSession.OutputDirectory, "frames");
                    if (Directory.Exists(screenshotsPath))
                    {
                        var result = MessageBox.Show(
                            $"Recording cancelled.\n\nWould you like to view the screenshots that were captured?\n\nLocation: {screenshotsPath}",
                            "View Screenshots?",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = screenshotsPath,
                                UseShellExecute = true,
                                Verb = "open"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StartButton.IsEnabled = true;
        }
    }

    private async Task ProcessAndExportAsync(string outputPath, bool isZip)
    {
        try
        {
            if (_orchestrator == null)
                return;

            string result;

            if (isZip)
            {
                result = await _orchestrator.ProcessAndExportToZipAsync(outputPath, AddProgress);
            }
            else
            {
                result = await _orchestrator.ProcessAndExportAsync(outputPath, AddProgress);
            }

            UpdateStatus("Export completed!");
            AddProgress("");
            AddProgress($"Guide saved to: {result}");

            var openResult = MessageBox.Show(
                $"Guide exported successfully!\n\nLocation: {result}\n\nWould you like to open the file?",
                "Export Successful",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (openResult == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to process recording: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Offer to view screenshots even if export failed
            if (_currentSession != null && !string.IsNullOrEmpty(_currentSession.OutputDirectory))
            {
                var screenshotsPath = Path.Combine(_currentSession.OutputDirectory, "frames");
                if (Directory.Exists(screenshotsPath))
                {
                    var result = MessageBox.Show(
                        $"Export failed, but screenshots were captured.\n\nWould you like to view them?\n\nLocation: {screenshotsPath}",
                        "View Screenshots?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = screenshotsPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
        }
        finally
        {
            StartButton.IsEnabled = true;
            UpdateStatus("Ready to record");
        }
    }

    private async Task ProcessAndExportDebugAsync(string outputPath)
    {
        try
        {
            if (_orchestrator == null)
                return;

            var result = await _orchestrator.ProcessAndExportDebugAsync(outputPath, AddProgress);

            UpdateStatus("Debug export completed!");
            AddProgress("");
            AddProgress($"Debug file saved to: {result}");

            var screenshotsDir = Path.Combine(Path.GetDirectoryName(result)!, "screenshots");
            if (Directory.Exists(screenshotsDir))
            {
                AddProgress($"Screenshots saved to: {screenshotsDir}");
            }

            var openResult = MessageBox.Show(
                $"Debug export completed!\n\nFile: {result}\n\nThis file contains:\n" +
                "- All detected steps\n" +
                "- The complete prompt that would be sent to Gemini\n" +
                "- Expected response format\n" +
                "- Screenshots (in separate folder)\n\n" +
                "Would you like to open the file?",
                "Debug Export Successful",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (openResult == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = result,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export debug information: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartButton.IsEnabled = true;
            UpdateStatus("Ready to record");
        }
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _recordingStartTime;
        RecordingTimeTextBlock.Text = $"Recording time: {elapsed:mm\\:ss}";
    }

    private void UpdateStatus(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusTextBlock.Text = status;
        });
    }

    private void AddProgress(string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(message))
            {
                ProgressTextBlock.Text += $"{DateTime.Now:HH:mm:ss} - {message}\n";
            }
            else
            {
                ProgressTextBlock.Text += "\n";
            }
        });
    }

    private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get API key from visible field (either PasswordBox or TextBox)
            var apiKey = ApiKeyPasswordBox.Visibility == Visibility.Visible
                ? ApiKeyPasswordBox.Password
                : ApiKeyTextBox.Text;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                MessageBox.Show("Please enter an API key.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate format (basic check - should start with expected prefix)
            if (!apiKey.StartsWith("AIza"))
            {
                var result = MessageBox.Show(
                    "This doesn't look like a valid Gemini API key (should start with 'AIza').\n\nDo you want to save it anyway?",
                    "Confirm API Key",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            // Save to configuration
            if (_geminiConfig != null)
            {
                _geminiConfig.ApiKey = apiKey;

                // Save to config file for persistence
                var config = new ConfigurationService.HowlConfig
                {
                    ApiKey = apiKey
                };
                ConfigurationService.SaveConfiguration(config);

                // Update UI
                ApiKeyBorder.Visibility = Visibility.Collapsed;
                ApiKeyStatusText.Text = $"✓ API Key saved to {ConfigurationService.GetConfigFilePath()}";
                ApiKeyStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60"));
                ApiKeyStatusText.Visibility = Visibility.Visible;

                UpdateStatus("Ready to record");

                // Clear the fields
                ApiKeyPasswordBox.Clear();
                ApiKeyTextBox.Clear();

                MessageBox.Show(
                    $"API key saved successfully!\n\nSaved to: {ConfigurationService.GetConfigFilePath()}\n\n" +
                    "Your API key will be automatically loaded on next startup.\n\n" +
                    "Note: Environment variable (GEMINI_API_KEY) takes priority over saved config.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save API key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ApiKeyPasswordBox.Visibility == Visibility.Visible)
        {
            // Switch to plain text view
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
            ApiKeyPasswordBox.Visibility = Visibility.Collapsed;
            ApiKeyTextBox.Visibility = Visibility.Visible;
            ToggleApiKeyButton.Content = "🙈";
            ToggleApiKeyButton.ToolTip = "Hide API Key";
        }
        else
        {
            // Switch to password view
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
            ApiKeyTextBox.Visibility = Visibility.Collapsed;
            ApiKeyPasswordBox.Visibility = Visibility.Visible;
            ToggleApiKeyButton.Content = "👁";
            ToggleApiKeyButton.ToolTip = "Show API Key";
        }
    }

    private void DebugModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DebugModeCheckBox.IsChecked == true)
        {
            FooterTextBlock.Text = "Debug mode: AI calls disabled - will export prompt preview only";
            FooterTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e67e22"));
        }
        else
        {
            UpdateFooterForProvider();
        }
    }

    private void AIProviderChanged(object sender, RoutedEventArgs e)
    {
        if (_orchestrator == null || _lmStudioService == null || _geminiService == null)
            return;

        try
        {
            // Update orchestrator with selected AI service
            if (LMStudioRadioButton.IsChecked == true)
            {
                _orchestrator.SetAIService(_lmStudioService);
                UpdateStatus("Ready to record (using LM Studio)");
                ApiKeyBorder.Visibility = Visibility.Collapsed;
                ModelSelectionPanel.Visibility = Visibility.Visible;
            }
            else if (GeminiRadioButton.IsChecked == true)
            {
                _orchestrator.SetAIService(_geminiService);
                UpdateStatus("Ready to record (using Gemini)");
                ModelSelectionPanel.Visibility = Visibility.Collapsed;

                // Show API key input if not configured
                if (_geminiConfig == null || string.IsNullOrWhiteSpace(_geminiConfig.ApiKey))
                {
                    ApiKeyBorder.Visibility = Visibility.Visible;
                }
            }

            UpdateFooterForProvider();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to switch AI provider: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateFooterForProvider()
    {
        if (DebugModeCheckBox.IsChecked == true)
            return;

        if (LMStudioRadioButton.IsChecked == true)
        {
            FooterTextBlock.Text = "Using local LM Studio - No API key required";
            FooterTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60"));
        }
        else
        {
            FooterTextBlock.Text = "Using Gemini - API key required";
            FooterTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#95a5a6"));
        }
    }

    private async Task LoadModelsAsync()
    {
        if (_lmStudioService == null || _lmStudioConfig == null)
            return;

        try
        {
            var models = await _lmStudioService.GetAvailableModelsAsync();

            if (models.Count > 0)
            {
                ModelComboBox.Items.Clear();

                foreach (var model in models)
                {
                    ModelComboBox.Items.Add(model);
                }

                // Select the currently configured model or the first one
                var currentModel = _lmStudioConfig.Model;
                if (models.Contains(currentModel))
                {
                    ModelComboBox.SelectedItem = currentModel;
                }
                else if (models.Count > 0)
                {
                    ModelComboBox.SelectedIndex = 0;
                }

                Console.WriteLine($"[MainWindow] Loaded {models.Count} models from LM Studio");
            }
            else
            {
                ModelComboBox.Items.Clear();
                ModelComboBox.Items.Add("No models found - Start LM Studio");
                ModelComboBox.SelectedIndex = 0;
                Console.WriteLine($"[MainWindow] No models available from LM Studio");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Error loading models: {ex.Message}");
            ModelComboBox.Items.Clear();
            ModelComboBox.Items.Add("Error loading models - Check LM Studio");
            ModelComboBox.SelectedIndex = 0;
        }
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadModelsAsync();
    }

    private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_lmStudioConfig == null || ModelComboBox.SelectedItem == null)
            return;

        var selectedModel = ModelComboBox.SelectedItem.ToString();

        if (!string.IsNullOrEmpty(selectedModel) &&
            !selectedModel.Contains("No models") &&
            !selectedModel.Contains("Error loading"))
        {
            _lmStudioConfig.Model = selectedModel;
            Console.WriteLine($"[MainWindow] Selected model: {selectedModel}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _recordingTimer?.Stop();
        base.OnClosed(e);
    }
}
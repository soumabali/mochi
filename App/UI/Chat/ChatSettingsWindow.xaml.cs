using System;
using System.Windows;
using MochiV2.Core.Models;
using MochiV2.Core.Services;
using Serilog;

namespace MochiV2.UI.Chat
{
    /// <summary>
    /// Settings window for Chat/LLM configuration. Post-MVP Phase I.
    /// </summary>
    public partial class ChatSettingsWindow : Window
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ChatSettingsWindow));
        private readonly ChatService _chatService;
        private readonly SaveData _saveData;

        public ChatSettingsWindow(ChatService chatService, SaveData saveData)
        {
            InitializeComponent();
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));

            // Load current settings
            ApiUrlBox.Text = _saveData.ChatApiUrl;
            ApiKeyBox.Text = _saveData.ChatApiKey;
            ModelBox.Text = _saveData.ChatModel;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // Save to SaveData
            _saveData.ChatApiUrl = ApiUrlBox.Text.Trim();
            _saveData.ChatApiKey = ApiKeyBox.Text.Trim();
            _saveData.ChatModel = ModelBox.Text.Trim();
            _saveData.ChatEnabled = true;

            // Update ChatService settings
            _chatService.Settings = new ChatSettings
            {
                ApiUrl = _saveData.ChatApiUrl,
                ApiKey = _saveData.ChatApiKey,
                Model = _saveData.ChatModel,
                Enabled = true,
            };

            Logger.Information("Chat settings saved: URL={Url}, Model={Model}", _saveData.ChatApiUrl, _saveData.ChatModel);
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
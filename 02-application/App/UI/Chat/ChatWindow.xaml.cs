using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MochiV2.Core.Services;
using Serilog;

namespace MochiV2.UI.Chat
{
    /// <summary>
    /// Chat window for talking with Mochi via LLM. Post-MVP Phase I.
    /// </summary>
    public partial class ChatWindow : Window
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ChatWindow));
        private readonly ChatService _chatService;
        private readonly SpeechBubbleService _speechBubble;

        public ChatWindow(ChatService chatService, SpeechBubbleService speechBubble)
        {
            InitializeComponent();
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _speechBubble = speechBubble;
            InputBox.Focus();
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                await SendMessage();
            }
        }

        private async System.Threading.Tasks.Task SendMessage()
        {
            var text = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            // Show user message
            AddMessageBubble(text, isUser: true);
            InputBox.Clear();
            InputBox.IsEnabled = false;

            // Add "typing..." indicator
            var typingIndicator = AddMessageBubble("...", isUser: false, isTyping: true);

            try
            {
                var response = await _chatService.SendAsync(text);
                MessagesPanel.Children.Remove(typingIndicator);
                AddMessageBubble(response, isUser: false);

                // Also show speech bubble near cat
                _speechBubble?.Show(response.Length > 50 ? response.Substring(0, 50) + "..." : response, 5.0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Chat send failed");
                MessagesPanel.Children.Remove(typingIndicator);
                AddMessageBubble($"Meow... something went wrong: {ex.Message}", isUser: false);
            }
            finally
            {
                InputBox.IsEnabled = true;
                InputBox.Focus();
            }
        }

        private Border AddMessageBubble(string text, bool isUser, bool isTyping = false)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(12),
                MaxWidth = 320,
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x89, 0xB4, 0xFA)  // blue
                    : Color.FromRgb(0x31, 0x32, 0x44)), // dark
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(isUser
                    ? Color.FromRgb(0x1E, 0x1E, 0x2E)
                    : Color.FromRgb(0xCD, 0xD6, 0xF4)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
            };

            border.Child = textBlock;
            MessagesPanel.Children.Add(border);

            // Auto-scroll
            MessagesScroll.ScrollToEnd();

            return border;
        }
    }
}
using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Editor.App
{
    public partial class AiChatPanel : UserControl
    {
        private const int MaxMessages = 100;

        public event EventHandler<string>? TextSearchRequested;
        public event EventHandler<string>? CommandRequested;

        public AiChatPanel()
        {
            InitializeComponent();
            BtnTextSearch.Click += OnTextSearchClick;
            TextSearchInput.KeyDown += OnTextSearchKeyDown;
            BtnExecute.Click += OnExecuteClick;
            CommandInput.KeyDown += OnCommandKeyDown;
        }

        private void OnTextSearchClick(object? sender, RoutedEventArgs e)
        {
            SubmitTextSearch();
        }

        private void OnTextSearchKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SubmitTextSearch();
        }

        private void SubmitTextSearch()
        {
            var query = TextSearchInput.Text?.Trim();
            if (string.IsNullOrEmpty(query))
                return;

            TextSearchRequested?.Invoke(this, query);
        }

        private void OnExecuteClick(object? sender, RoutedEventArgs e)
        {
            SubmitCommand();
        }

        private void OnCommandKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SubmitCommand();
        }

        private void SubmitCommand()
        {
            var command = CommandInput.Text?.Trim();
            if (string.IsNullOrEmpty(command))
                return;

            CommandRequested?.Invoke(this, command);
            CommandInput.Text = string.Empty;
        }

        public void ShowProgress(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressText.Text = $"\u25cf {message}...";
                ProgressBorder.IsVisible = true;
            });
        }

        public void HideProgress()
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressBorder.IsVisible = false;
            });
        }

        public void AddMessage(string text, bool isUser = false)
        {
            Dispatcher.UIThread.Post(() =>
            {
                while (ChatMessages.Children.Count >= MaxMessages)
                    ChatMessages.Children.RemoveAt(0);

                var border = new Border
                {
                    Background = new Avalonia.Media.SolidColorBrush(
                        Avalonia.Media.Color.Parse("#3A3F47")),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    Padding = new Avalonia.Thickness(8, 6),
                    Child = new TextBlock
                    {
                        Text = text,
                        Foreground = new Avalonia.Media.SolidColorBrush(
                            Avalonia.Media.Color.Parse("#CCCCCC")),
                        FontSize = 11,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };
                ChatMessages.Children.Add(border);
                ChatScroll.ScrollToEnd();
            });
        }
    }
}

using Avalonia.Controls;
using Avalonia.Threading;

namespace Editor.App
{
    public partial class AiChatPanel : UserControl
    {
        private const int MaxMessages = 100;

        public AiChatPanel()
        {
            InitializeComponent();
        }

        public void ShowProgress(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressText.Text = $"● {message}...";
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

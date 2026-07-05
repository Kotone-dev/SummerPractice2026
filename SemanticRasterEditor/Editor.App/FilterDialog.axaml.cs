using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Editor.App
{
    public partial class FilterDialog : Window
    {
        public int Brightness { get; private set; }
        public int Contrast { get; private set; }

        public FilterDialog()
        {
            InitializeComponent();
        }

        public FilterDialog(int currentBrightness, int currentContrast) : this()
        {
            BrightnessSlider.Value = currentBrightness;
            ContrastSlider.Value = currentContrast;
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            Brightness = (int)BrightnessSlider.Value;
            Contrast = (int)ContrastSlider.Value;
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}

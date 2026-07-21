using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SkiaSharp;

namespace Editor.App
{
    public partial class ColorPickerDialog : Window
    {
        public SKColor SelectedColor { get; private set; } = SKColors.Black;
        public bool DialogResult { get; private set; }

        public ColorPickerDialog(SKColor initial)
        {
            InitializeComponent();
            RedSlider.Value = initial.Red;
            GreenSlider.Value = initial.Green;
            BlueSlider.Value = initial.Blue;
            AlphaSlider.Value = initial.Alpha;

            RedSlider.ValueChanged += (_, _) => UpdatePreview();
            GreenSlider.ValueChanged += (_, _) => UpdatePreview();
            BlueSlider.ValueChanged += (_, _) => UpdatePreview();
            AlphaSlider.ValueChanged += (_, _) => UpdatePreview();

            UpdatePreview();

            BtnOk.Click += (_, _) =>
            {
                SelectedColor = new SKColor(
                    (byte)RedSlider.Value,
                    (byte)GreenSlider.Value,
                    (byte)BlueSlider.Value,
                    (byte)AlphaSlider.Value);
                DialogResult = true;
                Close();
            };
            BtnCancel.Click += (_, _) => Close();
        }

        private void UpdatePreview()
        {
            var r = (byte)RedSlider.Value;
            var g = (byte)GreenSlider.Value;
            var b = (byte)BlueSlider.Value;
            var a = (byte)AlphaSlider.Value;

            var hex = $"#{r:X2}{g:X2}{b:X2}{a:X2}";
            if (HexLabel is not null) HexLabel.Text = hex;
            if (PreviewBorder is not null)
                PreviewBorder.Background = new SolidColorBrush(Color.Parse(hex));
        }
    }
}

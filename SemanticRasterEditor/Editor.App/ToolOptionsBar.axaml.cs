using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using SkiaSharp;

namespace Editor.App
{
    public partial class ToolOptionsBar : UserControl
    {
        public event EventHandler<SKColor>? ColorChanged;
        public event EventHandler? UndoClicked;
        public event EventHandler? RedoClicked;

        public double OpacityValue => OpacitySlider?.Value ?? 100;
        public int BrushSize => int.TryParse(BrushSizeBox?.Text, out var v) ? Math.Max(1, v) : 20;
        public SKColor SelectedColor { get; private set; } = SKColors.Black;

        public SKColor BrushColor
        {
            get => SelectedColor;
            set
            {
                SelectedColor = value;
                var hex = $"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
                if (ColorIndicator is not null)
                    ColorIndicator.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));
            }
        }

        public string BlendMode
        {
            get
            {
                if (BlendModeCombo?.SelectedItem is ComboBoxItem item)
                    return item.Content?.ToString() ?? "Нормальный";
                return "Нормальный";
            }
        }

        public ToolOptionsBar()
        {
            InitializeComponent();

            if (OpacitySlider is not null)
                OpacitySlider.ValueChanged += (_, _) => UpdateOpacityText();

            if (ColorIndicator is not null)
                ColorIndicator.PointerPressed += OnColorIndicatorClick;

            if (BtnUndo is not null)
                BtnUndo.Click += (_, _) => UndoClicked?.Invoke(this, EventArgs.Empty);
            if (BtnRedo is not null)
                BtnRedo.Click += (_, _) => RedoClicked?.Invoke(this, EventArgs.Empty);

            var colors = new (string hex, SKColor color)[]
            {
                ("#000000", SKColors.Black),
                ("#FFFFFF", SKColors.White),
                ("#FF0000", SKColors.Red),
                ("#00FF00", SKColors.Lime),
                ("#0000FF", SKColors.Blue),
                ("#FFFF00", SKColors.Yellow),
                ("#FF8000", SKColors.Orange),
                ("#8000FF", SKColors.Purple),
            };

            foreach (var child in ColorPalette.Children)
            {
                if (child is Border border && border.Tag is string hex)
                {
                    var match = colors.FirstOrDefault(x => x.hex == hex);
                    var color = match.color;
                    if (color.Alpha == 0 && hex != "#000000") continue;
                    border.PointerPressed += (_, _) => SetColor(color, hex);
                }
            }

            UpdateOpacityText();
        }

        private async void OnColorIndicatorClick(object? sender, PointerPressedEventArgs e)
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
            if (parentWindow is null) return;

            var dialog = new ColorPickerDialog(SelectedColor);
            await dialog.ShowDialog(parentWindow);
            if (!dialog.DialogResult) return;

            var hex = $"#{dialog.SelectedColor.Red:X2}{dialog.SelectedColor.Green:X2}{dialog.SelectedColor.Blue:X2}";
            SetColor(dialog.SelectedColor, hex);
        }

        private void SetColor(SKColor color, string hex)
        {
            SelectedColor = color;
            if (ColorIndicator is not null)
                ColorIndicator.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex));
            ColorChanged?.Invoke(this, color);
        }

        private void UpdateOpacityText()
        {
            if (OpacityText is not null)
                OpacityText.Text = $"{(int)OpacityValue}%";
        }

        public void SetUndoRedoState(bool canUndo, bool canRedo)
        {
            if (BtnUndo is not null) BtnUndo.IsEnabled = canUndo;
            if (BtnRedo is not null) BtnRedo.IsEnabled = canRedo;
        }
    }
}

using System;
using Avalonia.Controls;

namespace Editor.App
{
    public partial class ToolOptionsBar : UserControl
    {
        public double OpacityValue => OpacitySlider?.Value ?? 100;
        public int BrushSize => int.TryParse(BrushSizeBox?.Text, out var v) ? Math.Max(1, v) : 200;

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
        }
    }
}

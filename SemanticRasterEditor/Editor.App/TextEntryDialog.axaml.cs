using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Editor.App
{
    public partial class TextEntryDialog : Window
    {
        public bool DialogResult { get; private set; }
        public string TextValue => TextInput?.Text ?? string.Empty;
        public string FontName => (FontCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Arial";
        public float FontSize => float.TryParse(FontSizeInput?.Text, out var v) ? Math.Max(6, v) : 24;

        public TextEntryDialog()
        {
            InitializeComponent();
            BtnOk.Click += (_, _) => { DialogResult = true; Close(); };
            BtnCancel.Click += (_, _) => { DialogResult = false; Close(); };
            TextInput.KeyDown += (_, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter) { DialogResult = true; Close(); }
                else if (e.Key == Avalonia.Input.Key.Escape) { DialogResult = false; Close(); }
            };
        }
    }
}

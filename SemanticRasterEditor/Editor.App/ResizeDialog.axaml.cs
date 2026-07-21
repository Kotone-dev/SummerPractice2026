using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Editor.App
{
    public partial class ResizeDialog : Window
    {
        public int NewWidth { get; private set; }
        public int NewHeight { get; private set; }
        public bool DialogResult { get; private set; }

        public ResizeDialog(int currentWidth, int currentHeight)
        {
            InitializeComponent();
            WidthBox.Text = currentWidth.ToString();
            HeightBox.Text = currentHeight.ToString();

            WidthBox.TextChanged += (_, _) => UpdateHeightIfLocked(currentWidth, currentHeight);
            HeightBox.TextChanged += (_, _) => UpdateWidthIfLocked(currentWidth, currentHeight);

            BtnOk.Click += (_, _) =>
            {
                if (int.TryParse(WidthBox.Text, out var w) && int.TryParse(HeightBox.Text, out var h)
                    && w > 0 && h > 0 && w <= 10000 && h <= 10000)
                {
                    NewWidth = w;
                    NewHeight = h;
                    DialogResult = true;
                    Close();
                }
            };

            BtnCancel.Click += (_, _) => Close();
        }

        private void UpdateHeightIfLocked(int origW, int origH)
        {
            if (KeepAspect?.IsChecked != true || origW == 0) return;
            if (int.TryParse(WidthBox.Text, out var w))
            {
                var h = (int)(w * ((double)origH / origW));
                if (HeightBox is not null)
                    HeightBox.Text = h.ToString();
            }
        }

        private void UpdateWidthIfLocked(int origW, int origH)
        {
            if (KeepAspect?.IsChecked != true || origH == 0) return;
            if (int.TryParse(HeightBox.Text, out var h))
            {
                var w = (int)(h * ((double)origW / origH));
                if (WidthBox is not null)
                    WidthBox.Text = w.ToString();
            }
        }
    }
}

using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Editor.App
{
    public partial class FilterPreviewDialog : Window
    {
        public bool DialogResult { get; private set; }

        public FilterPreviewDialog(SKBitmap original, Func<SKBitmap, SKBitmap> applyFilter, string title)
        {
            InitializeComponent();
            Title = title;
            BtnApply.Click += (_, _) => { DialogResult = true; Close(); };
            BtnCancel.Click += (_, _) => Close();

            ImageBefore.Source = BitmapToAvalonia(original);
            using var filtered = applyFilter(original);
            ImageAfter.Source = BitmapToAvalonia(filtered);
        }

        private static Bitmap BitmapToAvalonia(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }
    }
}

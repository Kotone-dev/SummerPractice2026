using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Editor.Services;
using SkiaSharp;

namespace Editor.App
{
    public partial class FilterDialog : Window
    {
        private readonly SKBitmap _original;
        private readonly ImageFilterService _filterService;

        public int Brightness { get; private set; }
        public int Contrast { get; private set; }
        public bool DialogResult { get; private set; }

        public FilterDialog(SKBitmap original, ImageFilterService filterService)
        {
            _original = original;
            _filterService = filterService;
            InitializeComponent();
            UpdatePreview();
            BrightnessSlider.ValueChanged += (_, _) => UpdatePreview();
            ContrastSlider.ValueChanged += (_, _) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            using var brightBitmap = _filterService.AdjustBrightness(_original, (int)BrightnessSlider.Value);
            using var result = _filterService.AdjustContrast(brightBitmap, (int)ContrastSlider.Value);
            PreviewImage.Source = BitmapToAvalonia(result);
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            Brightness = (int)BrightnessSlider.Value;
            Contrast = (int)ContrastSlider.Value;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private static Bitmap BitmapToAvalonia(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 80);
            using var stream = new MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }
    }
}

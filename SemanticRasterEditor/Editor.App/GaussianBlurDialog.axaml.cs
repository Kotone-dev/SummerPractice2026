using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Editor.Services;
using SkiaSharp;

namespace Editor.App
{
    public partial class GaussianBlurDialog : Window
    {
        private readonly SKBitmap _original;
        private readonly ImageFilterService _filterService;

        public int Radius { get; private set; }
        public bool DialogResult { get; private set; }

        public GaussianBlurDialog(SKBitmap original, ImageFilterService filterService)
        {
            _original = original;
            _filterService = filterService;
            InitializeComponent();

            RadiusSlider.ValueChanged += (_, e) =>
            {
                if (RadiusText is not null)
                    RadiusText.Text = ((int)e.NewValue).ToString();
                UpdatePreview();
            };

            BtnApply.Click += (_, _) =>
            {
                Radius = (int)RadiusSlider.Value;
                DialogResult = true;
                Close();
            };
            BtnCancel.Click += (_, _) => Close();

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            using var result = _filterService.ApplyGaussianBlur(_original, (int)RadiusSlider.Value);
            PreviewImage.Source = BitmapToAvalonia(result);
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

using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using SkiaSharp;

namespace Editor.App
{
    public partial class ChannelsPanel : UserControl
    {
        private SKBitmap? _sourceImage;
        private readonly bool[] _channelVisibility = { true, true, true, true, true };

        public bool[] ChannelVisibility => (bool[])_channelVisibility.Clone();
        public event Action? ChannelVisibilityChanged;

        public ChannelsPanel()
        {
            InitializeComponent();
        }

        public void SetImage(SKBitmap? image)
        {
            _sourceImage = image;
            Refresh();
        }

        public void Refresh()
        {
            if (ChannelList is null)
                return;

            ChannelList.ItemsSource = null;

            if (_sourceImage is null)
                return;

            int previewW = 60;
            int previewH = (int)((long)previewW * _sourceImage.Height / _sourceImage.Width);
            if (previewH < 1) previewH = 1;

            var items = new ListBoxItem[5];
            string[] names = ["RGB", "Красный", "Зеленый", "Синий", "Альфа"];
            var channelData = new byte[previewW * previewH];

            for (int i = 0; i < 5; i++)
            {
                ExtractChannel(_sourceImage, i, channelData, previewW, previewH);

                using var bmp = new SKBitmap(previewW, previewH, SKColorType.Gray8, SKAlphaType.Unpremul);
                unsafe
                {
                    var dst = (byte*)bmp.GetPixels().ToPointer();
                    if (dst is not null)
                        Marshal.Copy(channelData, 0, (IntPtr)dst, previewW * previewH);
                }

                var preview = CanvasControl.SkiaToAvaloniaBitmap(bmp);

                var panel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8
                };

                var visibilityBtn = new Button
                {
                    Padding = new Avalonia.Thickness(2),
                    Background = Avalonia.Media.Brushes.Transparent,
                    Width = 20,
                    Height = 20
                };
                var eyeIcon = new TextBlock
                {
                    Text = _channelVisibility[i] ? "👁" : "—",
                    FontSize = 10,
                    Foreground = new Avalonia.Media.SolidColorBrush(
                        Avalonia.Media.Color.Parse("#B5B5B5")),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                visibilityBtn.Content = eyeIcon;
                var channelIndex = i;
                visibilityBtn.Click += (_, _) =>
                {
                    _channelVisibility[channelIndex] = !_channelVisibility[channelIndex];
                    Refresh();
                    ChannelVisibilityChanged?.Invoke();
                };
                panel.Children.Add(visibilityBtn);

                var img = new Image
                {
                    Width = 40,
                    Height = 30,
                    Source = preview,
                    Stretch = Avalonia.Media.Stretch.UniformToFill
                };
                panel.Children.Add(img);

                var tb = new TextBlock
                {
                    Text = names[i],
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F2F2F2")),
                    FontSize = 12
                };
                panel.Children.Add(tb);

                items[i] = new ListBoxItem { Content = panel };
            }

            ChannelList.ItemsSource = items;
        }

        private static void ExtractChannel(SKBitmap source, int channelIndex, byte[] dst, int w, int h)
        {
            var srcW = source.Width;
            var srcH = source.Height;

            unsafe
            {
                var srcPixels = (byte*)source.GetPixels().ToPointer();
                if (srcPixels is null) return;
                var srcStride = source.RowBytes;

                for (int y = 0; y < h; y++)
                {
                    int srcY = y * srcH / h;
                    var srcRow = srcPixels + srcY * srcStride;
                    int dstOff = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        int srcX = x * srcW / w;
                        var px = srcRow + srcX * 4;
                        byte value = channelIndex switch
                        {
                            0 => (byte)((px[2] + px[1] + px[0]) / 3),
                            1 => px[2],
                            2 => px[1],
                            3 => px[0],
                            4 => px[3],
                            _ => 0
                        };
                        dst[dstOff + x] = value;
                    }
                }
            }
        }

    }
}

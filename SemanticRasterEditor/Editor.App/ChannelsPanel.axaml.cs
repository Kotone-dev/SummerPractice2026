using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.App
{
    public partial class ChannelsPanel : UserControl
    {
        private SKBitmap? _sourceImage;

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

            var items = new ListBoxItem[5];
            string[] names = ["RGB", "Красный", "Зеленый", "Синий", "Альфа"];

            for (int i = 0; i < 5; i++)
            {
                var channelBitmap = ExtractChannel(_sourceImage, i);
                var preview = CreatePreview(channelBitmap);
                channelBitmap.Dispose();

                var panel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8
                };

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
                    Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#CCCCCC"))
                };
                panel.Children.Add(tb);

                items[i] = new ListBoxItem { Content = panel };
            }

            ChannelList.ItemsSource = items;
        }

        private static SKBitmap ExtractChannel(SKBitmap source, int channelIndex)
        {
            int w = source.Width;
            int h = source.Height;
            var result = new SKBitmap(w, h, SKColorType.Gray8, SKAlphaType.Unpremul);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    byte value = channelIndex switch
                    {
                        0 => (byte)((pixel.Red + pixel.Green + pixel.Blue) / 3),
                        1 => pixel.Red,
                        2 => pixel.Green,
                        3 => pixel.Blue,
                        4 => pixel.Alpha,
                        _ => 0
                    };
                    result.SetPixel(x, y, new SKColor(value, value, value));
                }
            }

            return result;
        }

        private static Bitmap CreatePreview(SKBitmap channel)
        {
            using var stream = new MemoryStream();
            using var image = SKImage.FromBitmap(channel);
            using var data = image.Encode(SKEncodedImageFormat.Png, 80);
            data.SaveTo(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }
    }
}

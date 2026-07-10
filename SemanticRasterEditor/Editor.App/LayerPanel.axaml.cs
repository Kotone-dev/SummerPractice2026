using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Editor.Services;
using SkiaSharp;

namespace Editor.App
{
    public partial class LayerPanel : UserControl
    {
        private LayerService? _layerService;
        private bool _isRefreshing;

        public ChannelsPanel Channels => ChannelsPanel;

        public event Action? LayerChanged;

        public LayerPanel()
        {
            InitializeComponent();
            BtnAdd.Click += OnAddClick;
            BtnRemove.Click += OnRemoveClick;
            BtnUp.Click += OnUpClick;
            BtnDown.Click += OnDownClick;
            LayerList.SelectionChanged += OnSelectionChanged;
        }

        public void Bind(LayerService service)
        {
            _layerService = service;
            Refresh();
        }

        public void Refresh()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                RefreshInner();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RefreshInner()
        {
            if (_layerService is null)
                return;

            var prevIndex = LayerList.SelectedIndex;
            LayerList.ItemsSource = null;

            var items = new ListBoxItem[_layerService.Count];
            for (int i = 0; i < _layerService.Count; i++)
            {
                var layer = _layerService.Layers[i];
                var panel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 6
                };

                // Visibility toggle
                var visibilityBtn = new Button
                {
                    Padding = new Avalonia.Thickness(2),
                    Background = Avalonia.Media.Brushes.Transparent,
                    Width = 20,
                    Height = 20
                };
                var eyeIcon = new TextBlock
                {
                    Text = layer.IsVisible ? "👁" : "—",
                    FontSize = 10,
                    Foreground = new Avalonia.Media.SolidColorBrush(
                        Avalonia.Media.Color.Parse("#AAAAAA")),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                visibilityBtn.Content = eyeIcon;
                var layerRef = layer;
                visibilityBtn.Click += (_, _) =>
                {
                    layerRef.IsVisible = !layerRef.IsVisible;
                    Refresh();
                    LayerChanged?.Invoke();
                };
                panel.Children.Add(visibilityBtn);

                // Thumbnail
                var thumbBorder = new Border
                {
                    Width = 36,
                    Height = 36,
                    BorderBrush = new Avalonia.Media.SolidColorBrush(
                        Avalonia.Media.Color.Parse("#3F3F3F")),
                    BorderThickness = new Avalonia.Thickness(1),
                    ClipToBounds = true
                };

                if (layer.Bitmap is not null)
                {
                    var thumb = CreateThumbnail(layer.Bitmap, 36, 36);
                    var img = new Image
                    {
                        Source = thumb,
                        Stretch = Avalonia.Media.Stretch.UniformToFill
                    };
                    thumbBorder.Child = img;
                }
                panel.Children.Add(thumbBorder);

                // Layer name
                var name = layer.Name;
                if (_layerService.ActiveIndex == i)
                    name += " *";

                var tb = new TextBlock
                {
                    Text = name,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    MaxWidth = 120
                };
                panel.Children.Add(tb);

                items[i] = new ListBoxItem { Content = panel };
            }

            LayerList.ItemsSource = items;

            if (prevIndex >= 0 && prevIndex < _layerService.Count)
                LayerList.SelectedIndex = prevIndex;
            else if (_layerService.Count > 0)
                LayerList.SelectedIndex = _layerService.ActiveIndex;
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing)
                return;

            if (_layerService is null || LayerList.SelectedIndex < 0)
                return;

            _layerService.ActiveIndex = LayerList.SelectedIndex;
            Refresh();
        }

        private void OnAddClick(object? sender, RoutedEventArgs e)
        {
            // TODO: реализовать добавление нового пустого слоя
            LayerChanged?.Invoke();
        }

        private void OnRemoveClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null || _layerService.Count <= 1)
                return;

            _layerService.Remove(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }

        private void OnUpClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null)
                return;

            _layerService.MoveUp(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }

        private void OnDownClick(object? sender, RoutedEventArgs e)
        {
            if (_layerService is null)
                return;

            _layerService.MoveDown(_layerService.ActiveIndex);
            Refresh();
            LayerChanged?.Invoke();
        }

        private static Bitmap CreateThumbnail(SKBitmap source, int maxWidth, int maxHeight)
        {
            float scale = Math.Min((float)maxWidth / source.Width,
                                   (float)maxHeight / source.Height);
            int thumbW = (int)(source.Width * scale);
            int thumbH = (int)(source.Height * scale);

            using var resized = new SKBitmap(thumbW, thumbH, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(resized);
            canvas.Clear(SKColors.Transparent);

            using var paint = new SKPaint { IsAntialias = true };
            using var skImage = SKImage.FromBitmap(source);
            canvas.DrawImage(skImage,
                new SKRect(0, 0, thumbW, thumbH),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
                paint);

            using var stream = new MemoryStream();
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 60);
            data.SaveTo(stream);
            stream.Position = 0;
            return new Bitmap(stream);
        }
    }
}

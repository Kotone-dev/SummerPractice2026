using Editor.Models;
using SkiaSharp;

namespace Editor.Services
{
    public class LayerService : IDisposable
    {
        private readonly List<Layer> _layers = new();
        private int _activeIndex;
        private bool _disposed;

        public IReadOnlyList<Layer> Layers => _layers;

        public int ActiveIndex
        {
            get => _activeIndex;
            set
            {
                if (value < 0 || value >= _layers.Count)
                    return;
                _activeIndex = value;
            }
        }

        public Layer? ActiveLayer =>
            _activeIndex >= 0 && _activeIndex < _layers.Count
                ? _layers[_activeIndex]
                : null;

        public int Count => _layers.Count;

        public void Add(SKBitmap bitmap, string? name = null)
        {
            var layer = new Layer(bitmap, name ?? $"Слой {_layers.Count + 1}");
            _layers.Add(layer);
            _activeIndex = _layers.Count - 1;
        }

        public void Remove(int index)
        {
            if (index < 0 || index >= _layers.Count)
                return;

            _layers[index].Dispose();
            _layers.RemoveAt(index);

            if (_activeIndex >= _layers.Count)
                _activeIndex = _layers.Count - 1;
        }

        public void MoveUp(int index)
        {
            if (index <= 0 || index >= _layers.Count)
                return;

            var temp = _layers[index];
            _layers[index] = _layers[index - 1];
            _layers[index - 1] = temp;

            if (_activeIndex == index)
                _activeIndex--;
            else if (_activeIndex == index - 1)
                _activeIndex++;
        }

        public void MoveDown(int index)
        {
            if (index < 0 || index >= _layers.Count - 1)
                return;

            var temp = _layers[index];
            _layers[index] = _layers[index + 1];
            _layers[index + 1] = temp;

            if (_activeIndex == index)
                _activeIndex++;
            else if (_activeIndex == index + 1)
                _activeIndex--;
        }

        public SKBitmap Composite()
        {
            if (_layers.Count == 0)
                return new SKBitmap(1, 1);

            int width = 0;
            int height = 0;

            foreach (var layer in _layers)
            {
                if (layer.Bitmap is null)
                    continue;

                width = Math.Max(width, layer.Bitmap.Width);
                height = Math.Max(height, layer.Bitmap.Height);
            }

            if (width == 0 || height == 0)
                return new SKBitmap(1, 1);

            var result = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];

                if (!layer.IsVisible || layer.Bitmap is null)
                    continue;

                var paint = new SKPaint();
                if (layer.Opacity < 1f)
                {
                    var alpha = (byte)(255 * layer.Opacity);
                    paint.ColorFilter = SKColorFilter.CreateBlendMode(
                        new SKColor(0, 0, 0, alpha),
                        SKBlendMode.DstIn);
                }

                if (layer.HasMask && layer.Mask is not null)
                {
                    var masked = ApplyMask(layer.Bitmap, layer.Mask);
                    canvas.DrawBitmap(masked, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
                    masked.Dispose();
                }
                else
                {
                    canvas.DrawBitmap(layer.Bitmap, new SKPoint(0, 0), new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
                }

                paint.Dispose();
            }

            return result;
        }

        private static SKBitmap ApplyMask(SKBitmap source, SKBitmap mask)
        {
            var result = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    var maskAlpha = x < mask.Width && y < mask.Height
                        ? mask.GetPixel(x, y).Alpha
                        : (byte)255;

                    var alpha = (byte)(pixel.Alpha * maskAlpha / 255);
                    result.SetPixel(x, y, new SKColor(pixel.Red, pixel.Green, pixel.Blue, alpha));
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (var layer in _layers)
                layer.Dispose();

            _layers.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

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
            _disposed = false;
            var layer = new Layer(bitmap, name ?? $"Слой {_layers.Count + 1}");
            _layers.Add(layer);
            _activeIndex = _layers.Count - 1;
        }

        public void AddEmpty(int width, int height, string? name = null)
        {
            _disposed = false;
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
            }
            var layer = new Layer(bitmap, name ?? $"Слой {_layers.Count + 1}");
            _layers.Add(layer);
            _activeIndex = _layers.Count - 1;
        }

        public SKBitmap DuplicateLayer(int index)
        {
            if (index < 0 || index >= _layers.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var source = _layers[index];
            if (source.Bitmap is null)
                throw new InvalidOperationException("Layer has no bitmap");

            var copy = new SKBitmap(source.Bitmap.Width, source.Bitmap.Height,
                source.Bitmap.ColorType, source.Bitmap.AlphaType);
            using (var canvas = new SKCanvas(copy))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(source.Bitmap, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }

            var newLayer = new Layer(copy, $"{source.Name} (копия)");
            _layers.Insert(index + 1, newLayer);
            _activeIndex = index + 1;
            return copy;
        }

        public SKBitmap Flatten()
        {
            using var composite = Composite();
            var result = new SKBitmap(composite.Width, composite.Height,
                composite.ColorType, composite.AlphaType);
            using (var canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(composite, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }

            foreach (var layer in _layers)
                layer.Dispose();
            _layers.Clear();

            var flatLayer = new Layer(result, "Объединённый");
            _layers.Add(flatLayer);
            _activeIndex = 0;
            return result;
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

            using var paint = new SKPaint();

            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];

                if (!layer.IsVisible || layer.Bitmap is null)
                    continue;

                paint.Reset();
                paint.ColorFilter = null;

                if (layer.Opacity < 1f)
                {
                    var alpha = (byte)(255 * layer.Opacity);
                    paint.ColorFilter = SKColorFilter.CreateBlendMode(
                        new SKColor(0, 0, 0, alpha),
                        SKBlendMode.DstIn);
                }

                if (layer.HasMask && layer.Mask is not null)
                {
                    using var masked = ApplyMask(layer.Bitmap, layer.Mask);
                    canvas.DrawBitmap(masked, new SKPoint(0, 0),
                        new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
                }
                else
                {
                    canvas.DrawBitmap(layer.Bitmap, new SKPoint(0, 0),
                        new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);
                }
            }

            return result;
        }

        private static SKBitmap ApplyMask(SKBitmap source, SKBitmap mask)
        {
            var result = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);

            canvas.DrawBitmap(source, new SKPoint(0, 0),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

            using var maskPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn
            };

            if (mask.Width != source.Width || mask.Height != source.Height)
            {
                using var resizedMask = new SKBitmap(source.Width, source.Height,
                    SKColorType.Alpha8, SKAlphaType.Premul);
                using (var maskCanvas = new SKCanvas(resizedMask))
                {
                    maskCanvas.Clear(SKColors.Transparent);
                    maskCanvas.DrawBitmap(mask, new SKRect(0, 0, source.Width, source.Height),
                        new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                }
                canvas.DrawBitmap(resizedMask, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
                    maskPaint);
            }
            else
            {
                canvas.DrawBitmap(mask, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
                    maskPaint);
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach (var layer in _layers)
                layer.Dispose();

            _layers.Clear();
            _activeIndex = 0;
            _disposed = true;
        }
    }
}

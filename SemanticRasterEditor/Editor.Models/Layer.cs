using SkiaSharp;

namespace Editor.Models
{
    public class Layer : IDisposable
    {
        private SKBitmap? _bitmap;
        private SKBitmap? _mask;
        private bool _disposed;

        public string Name { get; set; }

        public SKBitmap? Bitmap => _bitmap;

        public SKBitmap? Mask => _mask;

        public bool IsVisible { get; set; } = true;

        public float Opacity { get; set; } = 1f;

        public bool HasMask => _mask is not null;

        public Layer(SKBitmap bitmap, string? name = null)
        {
            _bitmap = bitmap;
            Name = name ?? "Слой";
        }

        public void SetBitmap(SKBitmap bitmap)
        {
            _bitmap?.Dispose();
            _bitmap = bitmap;
        }

        public void SetMask(SKBitmap? mask)
        {
            _mask?.Dispose();
            _mask = mask;
        }

        public void ClearMask()
        {
            _mask?.Dispose();
            _mask = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _bitmap?.Dispose();
            _bitmap = null;

            _mask?.Dispose();
            _mask = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

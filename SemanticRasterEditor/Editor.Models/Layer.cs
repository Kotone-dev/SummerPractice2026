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

        private float _opacity = 1f;

        public float Opacity
        {
            get => _opacity;
            set => _opacity = Math.Clamp(value, 0f, 1f);
        }

        public bool HasMask => _mask is not null;

        public Layer(SKBitmap bitmap, string? name = null)
        {
            _bitmap = bitmap;
            Name = name ?? "Слой";
        }

        public void SetBitmap(SKBitmap bitmap)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Layer));

            _bitmap?.Dispose();
            _bitmap = bitmap;
        }

        public void SetMask(SKBitmap? mask)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Layer));

            _mask?.Dispose();
            _mask = mask;
        }

        public void ClearMask()
        {
            if (_disposed)
                return;

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
        }
    }
}

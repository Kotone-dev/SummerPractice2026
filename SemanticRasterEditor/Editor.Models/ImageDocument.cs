using SkiaSharp;

namespace Editor.Models
{
    public class ImageDocument
    {
        private SKBitmap? _bitmap;

        public SKBitmap? Bitmap => _bitmap;

        public string? FilePath { get; set; }

        public bool IsModified { get; private set; }

        public int Width => _bitmap?.Width ?? 0;

        public int Height => _bitmap?.Height ?? 0;

        public void SetBitmap(SKBitmap bitmap)
        {
            _bitmap?.Dispose();
            _bitmap = bitmap;
            IsModified = true;
        }

        public void MarkSaved(string path)
        {
            FilePath = path;
            IsModified = false;
        }

        public void Clear()
        {
            _bitmap?.Dispose();
            _bitmap = null;
            FilePath = null;
            IsModified = false;
        }
    }
}

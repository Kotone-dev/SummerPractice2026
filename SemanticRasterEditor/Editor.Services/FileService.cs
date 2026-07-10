using SkiaSharp;

namespace Editor.Services
{
    public class FileService
    {
        public static readonly string[] SupportedExtensions =
        [
            ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif"
        ];

        public SKBitmap? OpenImage(string path)
        {
            if (!File.Exists(path))
                return null;

            return SKBitmap.Decode(path);
        }

        public void SaveImage(SKBitmap bitmap, string path, int quality = 100)
        {
            if (bitmap is null)
                throw new ArgumentNullException(nameof(bitmap));
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Путь не может быть пустым", nameof(path));

            var format = GetFormat(path);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, quality);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
        }

        private static SKEncodedImageFormat GetFormat(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".webp" => SKEncodedImageFormat.Webp,
                ".gif" => SKEncodedImageFormat.Gif,
                ".bmp" => SKEncodedImageFormat.Png,
                _ => SKEncodedImageFormat.Png
            };
        }
    }
}

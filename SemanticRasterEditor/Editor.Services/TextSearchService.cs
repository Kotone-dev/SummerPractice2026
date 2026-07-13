using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public class TextSearchService : IDisposable
    {
        private readonly FastSamService _fastSam;
        private readonly RuClipService _ruClip;
        private bool _disposed;

        public TextSearchService(FastSamService fastSam, RuClipService ruClip)
        {
            _fastSam = fastSam ?? throw new ArgumentNullException(nameof(fastSam));
            _ruClip = ruClip ?? throw new ArgumentNullException(nameof(ruClip));
        }

        public static TextSearchService LoadFromDirectory(string modelsDir)
        {
            var fastSam = FastSamService.LoadFromDirectory(modelsDir);
            var ruClip = RuClipService.LoadFromDirectory(modelsDir);
            return new TextSearchService(fastSam, ruClip);
        }

        public SKBitmap? Search(SKBitmap image, string query)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Запрос не может быть пустым", nameof(query));

            var segments = _fastSam.SegmentAll(image);
            if (segments.Count == 0)
                return null;

            var textEmb = _ruClip.EncodeText(query);

            int bestIndex = -1;
            float bestSimilarity = float.MinValue;

            for (int i = 0; i < segments.Count; i++)
            {
                using var cropped = ApplyMaskToImage(image, segments[i]);
                if (cropped is null)
                    continue;

                var segEmb = _ruClip.EncodeImage(cropped);
                float similarity = RuClipService.CosineSimilarity(textEmb, segEmb);

                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
                return null;

            var result = segments[bestIndex];
            for (int i = 0; i < segments.Count; i++)
            {
                if (i != bestIndex)
                    segments[i].Dispose();
            }

            return result;
        }

        private static SKBitmap? ApplyMaskToImage(SKBitmap image, SKBitmap mask)
        {
            if (mask.Width != image.Width || mask.Height != image.Height)
                return null;

            int minX = image.Width, minY = image.Height;
            int maxX = 0, maxY = 0;
            bool found = false;

            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (mask.GetPixel(x, y).Alpha > 127)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                        found = true;
                    }
                }
            }

            if (!found)
                return null;

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;
            cropW = Math.Min(cropW, RuClipPreprocessor.ImageSize);
            cropH = Math.Min(cropH, RuClipPreprocessor.ImageSize);

            float scaleX = (float)cropW / image.Width;
            float scaleY = (float)cropH / image.Height;
            float scale = Math.Min(scaleX, scaleY);

            int resizedW = (int)(image.Width * scale);
            int resizedH = (int)(image.Height * scale);

            resizedW = Math.Max(resizedW, 1);
            resizedH = Math.Max(resizedH, 1);

            var resized = new SKBitmap(resizedW, resizedH, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

            using (var canvas = new SKCanvas(resized))
            {
                canvas.Clear(SKColors.Black);

                var src = new SKRect(0, 0, image.Width, image.Height);
                var dst = new SKRect(0, 0, resizedW, resizedH);
                canvas.DrawBitmap(image, src, dst);

                using var maskPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.DstIn
                };
                var maskSrc = new SKRect(0, 0, mask.Width, mask.Height);
                var maskDst = new SKRect(0, 0, resizedW, resizedH);
                canvas.DrawBitmap(mask, maskSrc, maskDst, maskPaint);
            }

            return resized;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _fastSam.Dispose();
            _ruClip.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

using SkiaSharp;

namespace Editor.Models
{
    public static class LayerMask
    {
        public static SKBitmap CreateEmpty(int width, int height)
        {
            var mask = new SKBitmap(width, height, SKColorType.Alpha8, SKAlphaType.Unpremul);

            using var canvas = new SKCanvas(mask);
            canvas.Clear(SKColors.Black);

            return mask;
        }

        public static SKBitmap CreateFilled(int width, int height, byte value)
        {
            var mask = new SKBitmap(width, height, SKColorType.Alpha8, SKAlphaType.Unpremul);

            using var canvas = new SKCanvas(mask);
            canvas.Clear(new SKColor(0, 0, 0, value));

            return mask;
        }

        public static SKBitmap Invert(SKBitmap mask)
        {
            var inverted = new SKBitmap(mask.Width, mask.Height, SKColorType.Alpha8, SKAlphaType.Unpremul);

            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    var alpha = mask.GetPixel(x, y).Alpha;
                    var invertedAlpha = (byte)(255 - alpha);
                    inverted.SetPixel(x, y, new SKColor(0, 0, 0, invertedAlpha));
                }
            }

            return inverted;
        }
    }
}

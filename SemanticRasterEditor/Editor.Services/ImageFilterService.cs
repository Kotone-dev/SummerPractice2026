using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public enum MorphologyType
    {
        Erode,
        Dilate,
        Open,
        Close
    }

    public class ImageFilterService
    {
        public SKBitmap AdjustBrightness(SKBitmap source, int value)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.ConvertScaleAbs(src, dst, 1.0, value);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap AdjustContrast(SKBitmap source, int value)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();

            double alpha = 1.0 + value / 100.0 * 2.0;
            alpha = Math.Clamp(alpha, 0.0, 3.0);

            Cv2.ConvertScaleAbs(src, dst, alpha, 0);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap ApplyMorphology(SKBitmap source, MorphologyType type)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));

            switch (type)
            {
                case MorphologyType.Erode:
                    Cv2.Erode(src, dst, kernel);
                    break;
                case MorphologyType.Dilate:
                    Cv2.Dilate(src, dst, kernel);
                    break;
                case MorphologyType.Open:
                    Cv2.MorphologyEx(src, dst, MorphTypes.Open, kernel);
                    break;
                case MorphologyType.Close:
                    Cv2.MorphologyEx(src, dst, MorphTypes.Close, kernel);
                    break;
            }

            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap ApplyGaussianBlur(SKBitmap source, int radius)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            int ksize = Math.Max(1, radius * 2 + 1);
            Cv2.GaussianBlur(src, dst, new OpenCvSharp.Size(ksize, ksize), 0);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap ApplySharpen(SKBitmap source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            using var kernel = new Mat(3, 3, MatType.CV_32F);
            unsafe
            {
                var ptr = (float*)kernel.Data;
                ptr[0] = 0; ptr[1] = -1; ptr[2] = 0;
                ptr[3] = -1; ptr[4] = 5; ptr[5] = -1;
                ptr[6] = 0; ptr[7] = -1; ptr[8] = 0;
            }
            Cv2.Filter2D(src, dst, src.Depth(), kernel);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap Rotate90CW(SKBitmap source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.Rotate(src, dst, RotateFlags.Rotate90Clockwise);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap Rotate90CCW(SKBitmap source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.Rotate(src, dst, RotateFlags.Rotate90Counterclockwise);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap FlipHorizontal(SKBitmap source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.Flip(src, dst, FlipMode.Y);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap FlipVertical(SKBitmap source)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.Flip(src, dst, FlipMode.X);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap Resize(SKBitmap source, int width, int height)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            using var src = SkiaBitmapConverter.ToMatWithAlpha(source);
            using var dst = new Mat();
            Cv2.Resize(src, dst, new OpenCvSharp.Size(width, height), interpolation: InterpolationFlags.Lanczos4);
            return SkiaBitmapConverter.ToBitmap(dst);
        }
    }
}

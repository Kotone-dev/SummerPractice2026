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
            using var src = SkiaBitmapConverter.ToMat(source);
            var dst = new Mat();
            Cv2.ConvertScaleAbs(src, dst, 1.0, value);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap AdjustContrast(SKBitmap source, int value)
        {
            using var src = SkiaBitmapConverter.ToMat(source);
            var dst = new Mat();

            double alpha = 1.0 + value / 100.0 * 2.0;
            alpha = Math.Clamp(alpha, 0.0, 3.0);

            Cv2.ConvertScaleAbs(src, dst, alpha, 0);
            return SkiaBitmapConverter.ToBitmap(dst);
        }

        public SKBitmap ApplyMorphology(SKBitmap source, MorphologyType type)
        {
            using var src = SkiaBitmapConverter.ToMat(source);
            var dst = new Mat();
            var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));

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

            kernel.Dispose();

            return SkiaBitmapConverter.ToBitmap(dst);
        }
    }
}

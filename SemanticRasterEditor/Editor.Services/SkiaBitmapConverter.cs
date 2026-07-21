using System.Runtime.InteropServices;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public static class SkiaBitmapConverter
    {
        public static Mat ToMat(SKBitmap bitmap)
        {
            if (bitmap is null)
                throw new ArgumentNullException(nameof(bitmap));

            var info = bitmap.Info;
            var pixels = bitmap.GetPixels(out _);
            var size = info.BytesSize;

            var buffer = new byte[size];
            Marshal.Copy(pixels, buffer, 0, size);

            using var mat = Mat.FromPixelData(info.Height, info.Width, MatType.CV_8UC4, buffer);

            var bgr = new Mat();
            Cv2.CvtColor(mat, bgr, ColorConversionCodes.BGRA2BGR);

            return bgr;
        }

        public static Mat ToMatWithAlpha(SKBitmap bitmap)
        {
            if (bitmap is null)
                throw new ArgumentNullException(nameof(bitmap));

            var info = bitmap.Info;
            var pixels = bitmap.GetPixels(out _);
            var size = info.BytesSize;

            var buffer = new byte[size];
            Marshal.Copy(pixels, buffer, 0, size);

            var mat = Mat.FromPixelData(info.Height, info.Width, MatType.CV_8UC4, buffer);
            return mat.Clone();
        }

        public static SKBitmap ToBitmap(Mat mat)
        {
            if (mat is null)
                throw new ArgumentNullException(nameof(mat));

            int channels = mat.Channels();
            Mat toUse;
            bool disposeToUse = false;

            if (channels == 4)
            {
                toUse = mat;
            }
            else if (channels == 3)
            {
                toUse = new Mat();
                Cv2.CvtColor(mat, toUse, ColorConversionCodes.BGR2BGRA);
                disposeToUse = true;
            }
            else if (channels == 1)
            {
                toUse = new Mat();
                Cv2.CvtColor(mat, toUse, ColorConversionCodes.GRAY2BGRA);
                disposeToUse = true;
            }
            else
            {
                throw new ArgumentException($"Unsupported number of channels: {channels}", nameof(mat));
            }

            try
            {
                var size = (int)(toUse.Step() * toUse.Rows);
                var bytes = new byte[size];
                Marshal.Copy(toUse.Data, bytes, 0, size);

                var bitmap = new SKBitmap(toUse.Width, toUse.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                var dst = bitmap.GetPixels();
                Marshal.Copy(bytes, 0, dst, size);

                return bitmap;
            }
            finally
            {
                if (disposeToUse)
                    toUse.Dispose();
            }
        }
    }
}

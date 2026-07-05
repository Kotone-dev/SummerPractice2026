using System.Runtime.InteropServices;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public static class SkiaBitmapConverter
    {
        public static Mat ToMat(SKBitmap bitmap)
        {
            var info = bitmap.Info;
            var pixels = bitmap.GetPixels(out IntPtr length);
            var mat = Mat.FromPixelData(info.Height, info.Width, MatType.CV_8UC4, pixels);

            var bgra = new Mat();
            Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGRA2BGR);
            mat.Dispose();

            return bgra;
        }

        public static SKBitmap ToBitmap(Mat mat)
        {
            var converted = new Mat();
            Cv2.CvtColor(mat, converted, ColorConversionCodes.BGR2BGRA);

            var size = (int)(converted.Step() * converted.Rows);
            var bytes = new byte[size];
            Marshal.Copy(converted.Data, bytes, 0, size);

            var bitmap = new SKBitmap(converted.Width, converted.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

            var dst = bitmap.GetPixels();
            Marshal.Copy(bytes, 0, dst, size);

            converted.Dispose();

            return bitmap;
        }
    }
}

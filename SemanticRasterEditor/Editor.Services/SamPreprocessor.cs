using System.Runtime.InteropServices;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public static class SamPreprocessor
    {
        public const int ImageSize = 1024;

        private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
        private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

        public static Mat ResizeAndPad(SKBitmap source)
        {
            using var src = SkiaBitmapConverter.ToMat(source);
            return ResizeAndPad(src);
        }

        public static Mat ResizeAndPad(Mat source)
        {
            int w = source.Width;
            int h = source.Height;

            float scale = ImageSize / (float)Math.Max(w, h);
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            using var resized = new Mat();
            Cv2.Resize(source, resized, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Linear);

            var padded = new Mat(ImageSize, ImageSize, source.Type(), new Scalar(0, 0, 0));
            resized.CopyTo(padded[new Rect(0, 0, newW, newH)]);

            return padded;
        }

        public static float[] ToTensor(Mat padded)
        {
            using var rgb = new Mat();
            Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

            int h = rgb.Rows;
            int w = rgb.Cols;
            int channelSize = h * w;
            var allBytes = new byte[channelSize * 3];
            Marshal.Copy(rgb.Data, allBytes, 0, allBytes.Length);

            var data = new float[3 * channelSize];
            for (int i = 0; i < channelSize; i++)
            {
                float r = allBytes[i * 3];
                float g = allBytes[i * 3 + 1];
                float b = allBytes[i * 3 + 2];
                data[i * 3] = (r / 255f - Mean[0]) / Std[0];
                data[i * 3 + 1] = (g / 255f - Mean[1]) / Std[1];
                data[i * 3 + 2] = (b / 255f - Mean[2]) / Std[2];
            }

            return data;
        }

        public static float[] ScalePoint(float x, float y, int origW, int origH)
        {
            float scale = ImageSize / (float)Math.Max(origW, origH);
            float newX = x * scale;
            float newY = y * scale;
            return [newX, newY];
        }
    }
}

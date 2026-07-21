using System.Runtime.InteropServices;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public static class FastSamPreprocessor
    {
        public const int ImageSize = 1024;

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
            Cv2.Resize(source, resized, new OpenCvSharp.Size(newW, newH),
                0, 0, InterpolationFlags.Linear);

            var padded = new Mat(ImageSize, ImageSize, source.Type(), new Scalar(114, 114, 114));
            resized.CopyTo(padded[new Rect(0, 0, newW, newH)]);

            return padded;
        }

        public static float[] ToFloatTensor(Mat padded)
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
                data[i] = allBytes[i * 3] / 255f;
                data[channelSize + i] = allBytes[i * 3 + 1] / 255f;
                data[channelSize * 2 + i] = allBytes[i * 3 + 2] / 255f;
            }

            return data;
        }

        public static float ComputeScale(int origW, int origH)
        {
            return ImageSize / (float)Math.Max(origW, origH);
        }
    }
}

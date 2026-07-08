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

            var resized = new Mat();
            Cv2.Resize(source, resized, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Linear);

            var padded = new Mat(ImageSize, ImageSize, source.Type(), new Scalar(0, 0, 0));
            resized.CopyTo(padded[new Rect(0, 0, newW, newH)]);
            resized.Dispose();

            return padded;
        }

        public static float[] ToTensor(Mat padded)
        {
            var rgb = new Mat();
            Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

            int h = rgb.Rows;
            int w = rgb.Cols;
            var data = new float[3 * h * w];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var pixel = rgb.At<Vec3b>(y, x);
                    int dstIdx = (y * w + x) * 3;

                    data[dstIdx] = (pixel[0] / 255f - Mean[0]) / Std[0];
                    data[dstIdx + 1] = (pixel[1] / 255f - Mean[1]) / Std[1];
                    data[dstIdx + 2] = (pixel[2] / 255f - Mean[2]) / Std[2];
                }
            }

            rgb.Dispose();
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

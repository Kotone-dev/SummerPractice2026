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

            var resized = new Mat();
            Cv2.Resize(source, resized, new OpenCvSharp.Size(newW, newH),
                0, 0, InterpolationFlags.Linear);

            var padded = new Mat(ImageSize, ImageSize, source.Type(), new Scalar(114, 114, 114));
            resized.CopyTo(padded[new Rect(0, 0, newW, newH)]);
            resized.Dispose();

            return padded;
        }

        public static float[] ToFloatTensor(Mat padded)
        {
            var rgb = new Mat();
            Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

            int h = rgb.Rows;
            int w = rgb.Cols;
            var data = new float[3 * h * w];

            for (int c = 0; c < 3; c++)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var pixel = rgb.At<Vec3b>(y, x);
                        data[c * h * w + y * w + x] = pixel[c] / 255f;
                    }
                }
            }

            rgb.Dispose();
            return data;
        }

        public static float ComputeScale(int origW, int origH)
        {
            return ImageSize / (float)Math.Max(origW, origH);
        }
    }
}

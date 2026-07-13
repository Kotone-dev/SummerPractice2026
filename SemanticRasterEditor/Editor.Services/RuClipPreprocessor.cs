using OpenCvSharp;
using SkiaSharp;
using Tokenizers.DotNet;

namespace Editor.Services
{
    public static class RuClipPreprocessor
    {
        public const int ImageSize = 336;
        public const int MaxTokenLength = 77;

        public const long PadToken = 0;
        public const long UnkToken = 1;
        public const long BosToken = 2;
        public const long EosToken = 3;

        private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
        private static readonly float[] Std = [0.26862954f, 0.26130258f, 0.27577711f];

        public static float[] ImageToTensor(SKBitmap source)
        {
            using var bgr = SkiaBitmapConverter.ToMat(source);

            var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(ImageSize, ImageSize),
                0, 0, InterpolationFlags.Linear);

            var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            var floatMat = new Mat();
            rgb.ConvertTo(floatMat, MatType.CV_32FC3);

            var data = new float[3 * ImageSize * ImageSize];

            for (int c = 0; c < 3; c++)
            {
                for (int y = 0; y < ImageSize; y++)
                {
                    for (int x = 0; x < ImageSize; x++)
                    {
                        var pixel = floatMat.At<Vec3f>(y, x);
                        float normalized = (pixel[c] / 255f - Mean[c]) / Std[c];
                        data[c * ImageSize * ImageSize + y * ImageSize + x] = normalized;
                    }
                }
            }

            resized.Dispose();
            rgb.Dispose();
            floatMat.Dispose();
            return data;
        }

        public static long[] Tokenize(string text, Tokenizer tokenizer)
        {
            var lower = text.ToLowerInvariant();
            var ids = tokenizer.Encode(lower);

            var result = new long[MaxTokenLength];
            result[0] = BosToken;

            int tokenCount = Math.Min(ids.Length, MaxTokenLength - 2);
            for (int i = 0; i < tokenCount; i++)
            {
                result[i + 1] = ids[i];
            }

            result[tokenCount + 1] = EosToken;

            for (int i = tokenCount + 2; i < MaxTokenLength; i++)
            {
                result[i] = PadToken;
            }

            return result;
        }
    }
}

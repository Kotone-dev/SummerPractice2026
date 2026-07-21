using System.Runtime.InteropServices;
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

            using var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(ImageSize, ImageSize),
                0, 0, InterpolationFlags.Linear);

            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            using var floatMat = new Mat();
            rgb.ConvertTo(floatMat, MatType.CV_32FC3);

            int channelSize = ImageSize * ImageSize;
            var allBytes = new float[channelSize * 3];
            Marshal.Copy(floatMat.Data, allBytes, 0, allBytes.Length);

            var data = new float[3 * channelSize];
            for (int i = 0; i < channelSize; i++)
            {
                float r = allBytes[i * 3];
                float g = allBytes[i * 3 + 1];
                float b = allBytes[i * 3 + 2];
                data[i] = (r / 255f - Mean[0]) / Std[0];
                data[channelSize + i] = (g / 255f - Mean[1]) / Std[1];
                data[channelSize * 2 + i] = (b / 255f - Mean[2]) / Std[2];
            }

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

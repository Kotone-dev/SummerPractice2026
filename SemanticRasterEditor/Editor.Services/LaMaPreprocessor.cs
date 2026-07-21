using System.Runtime.InteropServices;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public static class LaMaPreprocessor
    {
        public const int InputSize = 512;

        public static float[] ImageToTensor(SKBitmap source)
        {
            using var bgr = SkiaBitmapConverter.ToMat(source);

            using var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(InputSize, InputSize),
                0, 0, InterpolationFlags.Linear);

            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            using var floatMat = new Mat();
            rgb.ConvertTo(floatMat, MatType.CV_32FC3);

            int channelSize = InputSize * InputSize;
            var data = new float[3 * channelSize];
            var allPixels = new float[channelSize * 3];
            Marshal.Copy(floatMat.Data, allPixels, 0, allPixels.Length);

            for (int i = 0; i < channelSize; i++)
            {
                data[i] = allPixels[i * 3];
                data[channelSize + i] = allPixels[i * 3 + 1];
                data[channelSize * 2 + i] = allPixels[i * 3 + 2];
            }

            return data;
        }

        public static float[] MaskToTensor(SKBitmap maskSource)
        {
            using var maskMat = SkiaBitmapConverter.ToMat(maskSource);

            using var gray = new Mat();
            Cv2.CvtColor(maskMat, gray, ColorConversionCodes.BGR2GRAY);

            using var resized = new Mat();
            Cv2.Resize(gray, resized, new OpenCvSharp.Size(InputSize, InputSize),
                0, 0, InterpolationFlags.Linear);

            using var binary = new Mat();
            Cv2.Threshold(resized, binary, 0, 1.0, ThresholdTypes.Binary);

            using var floatMat = new Mat();
            binary.ConvertTo(floatMat, MatType.CV_32FC1);

            var data = new float[InputSize * InputSize];
            Marshal.Copy(floatMat.Data, data, 0, data.Length);

            return data;
        }

        public static SKBitmap Postprocess(float[] outputData, int origWidth, int origHeight)
        {
            int channelSize = InputSize * InputSize;

            var rData = new float[channelSize];
            var gData = new float[channelSize];
            var bData = new float[channelSize];
            Array.Copy(outputData, 0, rData, 0, channelSize);
            Array.Copy(outputData, channelSize, gData, 0, channelSize);
            Array.Copy(outputData, channelSize * 2, bData, 0, channelSize);

            using var rMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            using var gMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            using var bMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            Marshal.Copy(rData, 0, rMat.Data, channelSize);
            Marshal.Copy(gData, 0, gMat.Data, channelSize);
            Marshal.Copy(bData, 0, bMat.Data, channelSize);

            using var rClamped = new Mat();
            using var gClamped = new Mat();
            using var bClamped = new Mat();
            Cv2.Min(rMat, new Scalar(255), rClamped);
            Cv2.Min(gMat, new Scalar(255), gClamped);
            Cv2.Min(bMat, new Scalar(255), bClamped);
            Cv2.Max(rClamped, new Scalar(0), rClamped);
            Cv2.Max(gClamped, new Scalar(0), gClamped);
            Cv2.Max(bClamped, new Scalar(0), bClamped);

            using var bgr = new Mat();
            Cv2.Merge(new[] { bClamped, gClamped, rClamped }, bgr);

            using var uint8Mat = new Mat();
            bgr.ConvertTo(uint8Mat, MatType.CV_8UC3);

            using var result = new Mat();
            Cv2.Resize(uint8Mat, result, new OpenCvSharp.Size(origWidth, origHeight),
                0, 0, InterpolationFlags.Linear);

            return SkiaBitmapConverter.ToBitmap(result);
        }
    }
}

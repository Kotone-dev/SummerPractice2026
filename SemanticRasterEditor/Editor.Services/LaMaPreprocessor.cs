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

            var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(InputSize, InputSize),
                0, 0, InterpolationFlags.Linear);

            var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            var floatMat = new Mat();
            rgb.ConvertTo(floatMat, MatType.CV_32FC3);

            var data = new float[3 * InputSize * InputSize];
            for (int c = 0; c < 3; c++)
            {
                for (int y = 0; y < InputSize; y++)
                {
                    for (int x = 0; x < InputSize; x++)
                    {
                        var pixel = floatMat.At<Vec3f>(y, x);
                        data[c * InputSize * InputSize + y * InputSize + x] = pixel[c];
                    }
                }
            }

            resized.Dispose();
            rgb.Dispose();
            floatMat.Dispose();
            return data;
        }

        public static float[] MaskToTensor(SKBitmap maskSource)
        {
            using var maskMat = SkiaBitmapConverter.ToMat(maskSource);
            var gray = new Mat();
            Cv2.CvtColor(maskMat, gray, ColorConversionCodes.BGR2GRAY);

            var resized = new Mat();
            Cv2.Resize(gray, resized, new OpenCvSharp.Size(InputSize, InputSize),
                0, 0, InterpolationFlags.Linear);

            var binary = new Mat();
            Cv2.Threshold(resized, binary, 0, 1.0, ThresholdTypes.Binary);

            var floatMat = new Mat();
            binary.ConvertTo(floatMat, MatType.CV_32FC1);

            var data = new float[InputSize * InputSize];
            Marshal.Copy(floatMat.Data, data, 0, data.Length);

            gray.Dispose();
            resized.Dispose();
            binary.Dispose();
            floatMat.Dispose();
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

            var rMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            var gMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            var bMat = new Mat(InputSize, InputSize, MatType.CV_32FC1);
            Marshal.Copy(rData, 0, rMat.Data, channelSize);
            Marshal.Copy(gData, 0, gMat.Data, channelSize);
            Marshal.Copy(bData, 0, bMat.Data, channelSize);

            var rClamped = new Mat();
            var gClamped = new Mat();
            var bClamped = new Mat();
            Cv2.Min(rMat, new Scalar(255), rClamped);
            Cv2.Min(gMat, new Scalar(255), gClamped);
            Cv2.Min(bMat, new Scalar(255), bClamped);
            Cv2.Max(rClamped, new Scalar(0), rClamped);
            Cv2.Max(gClamped, new Scalar(0), gClamped);
            Cv2.Max(bClamped, new Scalar(0), bClamped);

            var bgr = new Mat();
            Cv2.Merge(new[] { bClamped, gClamped, rClamped }, bgr);

            var uint8Mat = new Mat();
            bgr.ConvertTo(uint8Mat, MatType.CV_8UC3);

            var result = new Mat();
            Cv2.Resize(uint8Mat, result, new OpenCvSharp.Size(origWidth, origHeight),
                0, 0, InterpolationFlags.Linear);

            var bitmap = SkiaBitmapConverter.ToBitmap(result);

            rMat.Dispose();
            gMat.Dispose();
            bMat.Dispose();
            rClamped.Dispose();
            gClamped.Dispose();
            bClamped.Dispose();
            bgr.Dispose();
            uint8Mat.Dispose();
            result.Dispose();
            return bitmap;
        }
    }
}

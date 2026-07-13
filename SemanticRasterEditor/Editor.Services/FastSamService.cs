using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public class FastSamService : IDisposable
    {
        private readonly InferenceSession _session;
        private bool _disposed;

        public const int MaxSegments = 20;
        public const float DefaultConfThreshold = 0.4f;
        public const float DefaultIouThreshold = 0.9f;

        public FastSamService(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));

            _session = new InferenceSession(modelPath);
        }

        public static FastSamService LoadFromDirectory(string modelsDir)
        {
            var modelPath = Path.Combine(modelsDir, "fast_sam_x.onnx");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Модель FastSAM не найдена", modelPath);

            return new FastSamService(modelPath);
        }

        public List<SKBitmap> SegmentAll(SKBitmap image)
        {
            return SegmentAll(image, DefaultConfThreshold, DefaultIouThreshold);
        }

        public List<SKBitmap> SegmentAll(SKBitmap image, float confThreshold, float iouThreshold)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));

            int origW = image.Width;
            int origH = image.Height;

            using var padded = FastSamPreprocessor.ResizeAndPad(image);
            var tensorData = FastSamPreprocessor.ToFloatTensor(padded);

            var (boxes, maskCoeffs, protos) = RunInference(tensorData);

            var detections = ParseDetections(boxes, maskCoeffs, origW, origH);
            var filtered = ApplyNms(detections, confThreshold, iouThreshold);

            var masks = GenerateMasks(filtered, protos, origW, origH);

            return masks.Take(MaxSegments).ToList();
        }

        private (float[] boxes, float[] maskCoeffs, float[] protos) RunInference(float[] tensorData)
        {
            var input = new DenseTensor<float>(
                tensorData,
                new ReadOnlySpan<int>(new[] { 1, 3, FastSamPreprocessor.ImageSize, FastSamPreprocessor.ImageSize }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", input)
            };

            using var results = _session.Run(inputs);

            var outputs = results.ToList();
            var output0 = outputs[0].AsTensor<float>().ToArray();
            var output4 = outputs.Count > 4 ? outputs[4].AsTensor<float>().ToArray() : Array.Empty<float>();
            var output5 = outputs.Count > 5 ? outputs[5].AsTensor<float>().ToArray() : Array.Empty<float>();

            return (output0, output4, output5);
        }

        private static List<Detection> ParseDetections(float[] output0, float[] maskCoeffs, int origW, int origH)
        {
            var detections = new List<Detection>();

            int numAnchors = output0.Length / 37;
            float scale = FastSamPreprocessor.ComputeScale(origW, origH);

            for (int i = 0; i < numAnchors; i++)
            {
                float cx = output0[i * 37 + 0];
                float cy = output0[i * 37 + 1];
                float w = output0[i * 37 + 2];
                float h = output0[i * 37 + 3];
                float objScore = output0[i * 37 + 4];

                float x1 = (cx - w / 2f) / scale;
                float y1 = (cy - h / 2f) / scale;
                float x2 = (cx + w / 2f) / scale;
                float y2 = (cy + h / 2f) / scale;

                x1 = Math.Max(0, Math.Min(x1, origW));
                y1 = Math.Max(0, Math.Min(y1, origH));
                x2 = Math.Max(0, Math.Min(x2, origW));
                y2 = Math.Max(0, Math.Min(y2, origH));

                if (x2 <= x1 || y2 <= y1)
                    continue;

                var coeffs = new float[32];
                if (maskCoeffs != null)
                {
                    for (int c = 0; c < 32; c++)
                        coeffs[c] = maskCoeffs[c * numAnchors + i];
                }
                else
                {
                    for (int c = 0; c < 32; c++)
                        coeffs[c] = output0[i * 37 + 5 + c];
                }

                detections.Add(new Detection
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Score = objScore,
                    MaskCoeffs = coeffs,
                    AnchorIndex = i
                });
            }

            return detections;
        }

        private static List<Detection> ApplyNms(List<Detection> detections, float confThreshold, float iouThreshold)
        {
            var filtered = detections
                .Where(d => d.Score >= confThreshold)
                .OrderByDescending(d => d.Score)
                .ToList();

            var result = new List<Detection>();
            var suppressed = new bool[filtered.Count];

            for (int i = 0; i < filtered.Count; i++)
            {
                if (suppressed[i])
                    continue;

                result.Add(filtered[i]);

                for (int j = i + 1; j < filtered.Count; j++)
                {
                    if (suppressed[j])
                        continue;

                    float iou = ComputeIoU(filtered[i], filtered[j]);
                    if (iou >= iouThreshold)
                        suppressed[j] = true;
                }
            }

            return result;
        }

        private static float ComputeIoU(Detection a, Detection b)
        {
            float interX1 = Math.Max(a.X1, b.X1);
            float interY1 = Math.Max(a.Y1, b.Y1);
            float interX2 = Math.Min(a.X2, b.X2);
            float interY2 = Math.Min(a.Y2, b.Y2);

            float interArea = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
            float union = areaA + areaB - interArea;

            return union > 0 ? interArea / union : 0;
        }

        private static List<SKBitmap> GenerateMasks(List<Detection> detections, float[] protos, int origW, int origH)
        {
            var masks = new List<SKBitmap>();

            if (protos is null)
                return masks;

            int protoChannels = 32;
            int protoH = 256;
            int protoW = 256;

            if (protos.Length > 0)
            {
                int totalSize = protos.Length / protoChannels;
                int sqrt = (int)Math.Sqrt(totalSize);
                if (sqrt * sqrt == totalSize)
                {
                    protoH = sqrt;
                    protoW = sqrt;
                }
            }

            foreach (var det in detections)
            {
                var mask = GenerateSingleMask(det, protos, protoChannels, protoH, protoW, origW, origH);
                masks.Add(mask);
            }

            masks.Sort((a, b) =>
            {
                int areaA = CountMaskPixels(a);
                int areaB = CountMaskPixels(b);
                return areaB.CompareTo(areaA);
            });

            return masks;
        }

        private static SKBitmap GenerateSingleMask(
            Detection det, float[] protos, int protoChannels, int protoH, int protoW, int origW, int origH)
        {
            var mask = new SKBitmap(origW, origH, SKColorType.Alpha8, SKAlphaType.Unpremul);

            float scale = FastSamPreprocessor.ComputeScale(origW, origH);

            int scaledW = (int)(origW * scale);
            int scaledH = (int)(origH * scale);

            int bboxX1 = (int)(det.X1 * scale);
            int bboxY1 = (int)(det.Y1 * scale);
            int bboxX2 = (int)(det.X2 * scale);
            int bboxY2 = (int)(det.Y2 * scale);

            bboxX1 = Math.Max(0, Math.Min(bboxX1, FastSamPreprocessor.ImageSize));
            bboxY1 = Math.Max(0, Math.Min(bboxY1, FastSamPreprocessor.ImageSize));
            bboxX2 = Math.Max(0, Math.Min(bboxX2, FastSamPreprocessor.ImageSize));
            bboxY2 = Math.Max(0, Math.Min(bboxY2, FastSamPreprocessor.ImageSize));

            float sx = (float)protoW / FastSamPreprocessor.ImageSize;
            float sy = (float)protoH / FastSamPreprocessor.ImageSize;

            int protoBboxX1 = (int)(bboxX1 * sx);
            int protoBboxY1 = (int)(bboxY1 * sy);
            int protoBboxX2 = (int)(bboxX2 * sx);
            int protoBboxY2 = (int)(bboxY2 * sy);

            protoBboxX1 = Math.Max(0, Math.Min(protoBboxX1, protoW));
            protoBboxY1 = Math.Max(0, Math.Min(protoBboxY1, protoH));
            protoBboxX2 = Math.Max(0, Math.Min(protoBboxX2, protoW));
            protoBboxY2 = Math.Max(0, Math.Min(protoBboxY2, protoH));

            for (int py = protoBboxY1; py < protoBboxY2; py++)
            {
                for (int px = protoBboxX1; px < protoBboxX2; px++)
                {
                    float val = 0f;
                    for (int c = 0; c < protoChannels; c++)
                    {
                        val += det.MaskCoeffs[c] * protos[c * protoH * protoW + py * protoW + px];
                    }
                    val = Sigmoid(val);

                    int origX = (int)(px / sx);
                    int origY = (int)(py / sy);

                    origX = Math.Max(0, Math.Min(origX, origW - 1));
                    origY = Math.Max(0, Math.Min(origY, origH - 1));

                    byte alpha = (byte)(val * 255);
                    mask.SetPixel(origX, origY, new SKColor(0, 0, 0, alpha));
                }
            }

            return mask;
        }

        private static float Sigmoid(float x)
        {
            return 1f / (1f + MathF.Exp(-x));
        }

        private static int CountMaskPixels(SKBitmap mask)
        {
            int count = 0;
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (mask.GetPixel(x, y).Alpha > 127)
                        count++;
                }
            }
            return count;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _session.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private class Detection
        {
            public float X1 { get; set; }
            public float Y1 { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }
            public float Score { get; set; }
            public float[] MaskCoeffs { get; set; } = [];
            public int AnchorIndex { get; set; }
        }
    }
}

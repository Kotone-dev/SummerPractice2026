using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using SkiaSharp;

namespace Editor.Services
{
    public class SamService : IDisposable
    {
        private readonly InferenceSession _encoder;
        private readonly InferenceSession _decoder;
        private bool _disposed;

        public SamService(string encoderPath, string decoderPath)
        {
            _encoder = new InferenceSession(encoderPath);
            _decoder = new InferenceSession(decoderPath);
        }

        public static SamService LoadFromDirectory(string modelsDir)
        {
            var encoderPath = Path.Combine(modelsDir, "mobile_sam_encoder.onnx");
            var decoderPath = Path.Combine(modelsDir, "mobile_sam_decoder.onnx");

            if (!File.Exists(encoderPath))
                throw new FileNotFoundException("Encoder model not found", encoderPath);
            if (!File.Exists(decoderPath))
                throw new FileNotFoundException("Decoder model not found", decoderPath);

            return new SamService(encoderPath, decoderPath);
        }

        public SKBitmap Predict(SKBitmap image, float pointX, float pointY)
        {
            int origW = image.Width;
            int origH = image.Height;

            using var padded = SamPreprocessor.ResizeAndPad(image);
            var tensorData = SamPreprocessor.ToTensor(padded);
            var scaledPoint = SamPreprocessor.ScalePoint(pointX, pointY, origW, origH);

            var embedding = RunEncoder(tensorData);

            var mask = RunDecoder(embedding, scaledPoint[0], scaledPoint[1], origW, origH);

            return mask;
        }

        private float[] RunEncoder(float[] tensorData)
        {
            var input = new DenseTensor<float>(
                tensorData,
                new ReadOnlySpan<int>(new[] { SamPreprocessor.ImageSize, SamPreprocessor.ImageSize, 3 }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_image", input)
            };

            using var results = _encoder.Run(inputs);
            var embedding = results.First().AsTensor<float>().ToArray();
            return embedding;
        }

        private SKBitmap RunDecoder(float[] embedding, float pointX, float pointY, int origW, int origH)
        {
            var embeddingTensor = new DenseTensor<float>(
                embedding,
                new ReadOnlySpan<int>(new[] { 1, 256, 64, 64 }));

            var pointCoords = new DenseTensor<float>(
                new[] { pointX, pointY },
                new ReadOnlySpan<int>(new[] { 1, 1, 2 }));

            var pointLabels = new DenseTensor<float>(
                new[] { 1f },
                new ReadOnlySpan<int>(new[] { 1, 1 }));

            var maskInput = new DenseTensor<float>(
                new float[1 * 1 * 256 * 256],
                new ReadOnlySpan<int>(new[] { 1, 1, 256, 256 }));

            var hasMaskInput = new DenseTensor<float>(
                new[] { 0f },
                new ReadOnlySpan<int>(new[] { 1 }));

            var origImSize = new DenseTensor<float>(
                new[] { (float)origH, (float)origW },
                new ReadOnlySpan<int>(new[] { 2 }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image_embeddings", embeddingTensor),
                NamedOnnxValue.CreateFromTensor("point_coords", pointCoords),
                NamedOnnxValue.CreateFromTensor("point_labels", pointLabels),
                NamedOnnxValue.CreateFromTensor("mask_input", maskInput),
                NamedOnnxValue.CreateFromTensor("has_mask_input", hasMaskInput),
                NamedOnnxValue.CreateFromTensor("orig_im_size", origImSize)
            };

            using var results = _decoder.Run(inputs);

            var masks = results.First().AsTensor<float>().ToArray();

            return CreateMaskBitmap(masks, origW, origH);
        }

        private static SKBitmap CreateMaskBitmap(float[] maskData, int width, int height)
        {
            var mask = new SKBitmap(width, height, SKColorType.Alpha8, SKAlphaType.Unpremul);

            float maxVal = maskData.Max();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    float val = maskData[idx];
                    byte alpha = val > 0f ? (byte)(val / maxVal * 255) : (byte)0;
                    mask.SetPixel(x, y, new SKColor(0, 0, 0, alpha));
                }
            }

            return mask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _encoder?.Dispose();
            _decoder?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

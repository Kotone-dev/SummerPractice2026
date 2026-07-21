using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
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
            if (string.IsNullOrEmpty(encoderPath))
                throw new ArgumentException("Путь к encoder не может быть пустым", nameof(encoderPath));
            if (string.IsNullOrEmpty(decoderPath))
                throw new ArgumentException("Путь к decoder не может быть пустым", nameof(decoderPath));

            _encoder = new InferenceSession(encoderPath);

            try
            {
                _decoder = new InferenceSession(decoderPath);
            }
            catch (Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
            {
                _encoder?.Dispose();
                throw;
            }
        }

        public static SamService LoadFromDirectory(string modelsDir)
        {
            var encoderPath = Path.Combine(modelsDir, "mobile_sam_encoder.onnx");
            var decoderPath = Path.Combine(modelsDir, "mobile_sam_decoder.onnx");

            if (!File.Exists(encoderPath))
                throw new FileNotFoundException("Модель encoder не найдена", encoderPath);
            if (!File.Exists(decoderPath))
                throw new FileNotFoundException("Модель decoder не найдена", decoderPath);

            return new SamService(encoderPath, decoderPath);
        }

        public SKBitmap Predict(SKBitmap image, float pointX, float pointY)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));

            int origW = image.Width;
            int origH = image.Height;

            if (origW <= 0 || origH <= 0)
                throw new ArgumentException("Изображение должно иметь положительные размеры", nameof(image));

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
            if (results.Count == 0)
                throw new InvalidOperationException("Encoder не вернул результатов");
            return results.First().AsTensor<float>().ToArray();
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

            if (results.Count == 0)
                throw new InvalidOperationException("Decoder не вернул результатов");
            var masks = results.First().AsTensor<float>().ToArray();

            return CreateMaskBitmap(masks, origW, origH);
        }

        private static unsafe SKBitmap CreateMaskBitmap(float[] maskData, int width, int height)
        {
            var mask = new SKBitmap(width, height, SKColorType.Alpha8, SKAlphaType.Unpremul);

            if (maskData.Length == 0)
                return mask;

            float maxVal = maskData.Max();
            if (maxVal <= 0f)
                return mask;

            float scale = 255f / maxVal;
            var pixels = (byte*)mask.GetPixels().ToPointer();
            int pixelCount = width * height;
            int count = Math.Min(pixelCount, maskData.Length);

            for (int i = 0; i < count; i++)
                pixels[i] = (byte)(maskData[i] * scale);

            return mask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _encoder.Dispose();
            _decoder.Dispose();
            _disposed = true;
        }
    }
}

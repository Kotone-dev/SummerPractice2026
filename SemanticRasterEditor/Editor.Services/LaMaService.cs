using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Editor.Services
{
    public class LaMaService : IDisposable
    {
        private readonly InferenceSession _session;
        private bool _disposed;

        public LaMaService(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));

            _session = new InferenceSession(modelPath);
        }

        public static LaMaService LoadFromDirectory(string modelsDir)
        {
            var modelPath = Path.Combine(modelsDir, "lama_fp32.onnx");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Модель LaMa не найдена", modelPath);

            return new LaMaService(modelPath);
        }

        public SKBitmap Inpaint(SKBitmap image, SKBitmap mask)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));
            if (mask is null)
                throw new ArgumentNullException(nameof(mask));
            if (image.Width <= 0 || image.Height <= 0)
                throw new ArgumentException("Изображение должно иметь положительные размеры", nameof(image));
            if (mask.Width <= 0 || mask.Height <= 0)
                throw new ArgumentException("Маска должна иметь положительные размеры", nameof(mask));

            int origW = image.Width;
            int origH = image.Height;

            var imageData = LaMaPreprocessor.ImageToTensor(image);
            var maskData = LaMaPreprocessor.MaskToTensor(mask);

            var outputData = RunInference(imageData, maskData);

            return LaMaPreprocessor.Postprocess(outputData, origW, origH);
        }

        private float[] RunInference(float[] imageData, float[] maskData)
        {
            int expectedImageSize = 3 * LaMaPreprocessor.InputSize * LaMaPreprocessor.InputSize;
            int expectedMaskSize = LaMaPreprocessor.InputSize * LaMaPreprocessor.InputSize;

            if (imageData.Length != expectedImageSize)
                throw new ArgumentException(
                    $"Неверный размер данных изображения: {imageData.Length}. Ожидалось {expectedImageSize}",
                    nameof(imageData));

            if (maskData.Length != expectedMaskSize)
                throw new ArgumentException(
                    $"Неверный размер данных маски: {maskData.Length}. Ожидалось {expectedMaskSize}",
                    nameof(maskData));

            var imageTensor = new DenseTensor<float>(
                imageData,
                new ReadOnlySpan<int>(new[] { 1, 3, LaMaPreprocessor.InputSize, LaMaPreprocessor.InputSize }));

            var maskTensor = new DenseTensor<float>(
                maskData,
                new ReadOnlySpan<int>(new[] { 1, 1, LaMaPreprocessor.InputSize, LaMaPreprocessor.InputSize }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("image", imageTensor),
                NamedOnnxValue.CreateFromTensor("mask", maskTensor)
            };

            using var results = _session.Run(inputs);

            if (results.Count == 0)
                throw new InvalidOperationException("LaMa модель не вернула результатов");
            var resultTensor = results.First().AsTensor<float>();
            return resultTensor.ToArray();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _session.Dispose();
            _disposed = true;
        }
    }
}

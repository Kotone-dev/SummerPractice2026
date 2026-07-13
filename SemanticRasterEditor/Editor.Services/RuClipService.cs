using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using Tokenizers.DotNet;

namespace Editor.Services
{
    public class RuClipService : IDisposable
    {
        private readonly InferenceSession _textSession;
        private readonly InferenceSession _visualSession;
        private readonly Tokenizer _tokenizer;
        private bool _disposed;

        public RuClipService(string textModelPath, string visualModelPath, string tokenizerPath)
        {
            if (string.IsNullOrEmpty(textModelPath))
                throw new ArgumentException("Путь к текстовой модели не может быть пустым", nameof(textModelPath));
            if (string.IsNullOrEmpty(visualModelPath))
                throw new ArgumentException("Путь к визуальной модели не может быть пустым", nameof(visualModelPath));
            if (string.IsNullOrEmpty(tokenizerPath))
                throw new ArgumentException("Путь к токенизатору не может быть пустым", nameof(tokenizerPath));

            _textSession = new InferenceSession(textModelPath);
            _visualSession = new InferenceSession(visualModelPath);
            _tokenizer = new Tokenizer(vocabPath: tokenizerPath);
        }

        public static RuClipService LoadFromDirectory(string modelsDir)
        {
            var textPath = Path.Combine(modelsDir, "ruclip_textual_int8.onnx");
            var visualPath = Path.Combine(modelsDir, "ruclip_visual_int8.onnx");
            var tokenizerPath = Path.Combine(modelsDir, "tokenizer.json");

            if (!File.Exists(textPath))
                throw new FileNotFoundException("Текстовая модель ruCLIP не найдена", textPath);
            if (!File.Exists(visualPath))
                throw new FileNotFoundException("Визуальная модель ruCLIP не найдена", visualPath);
            if (!File.Exists(tokenizerPath))
                throw new FileNotFoundException("Токенизатор ruCLIP не найден", tokenizerPath);

            return new RuClipService(textPath, visualPath, tokenizerPath);
        }

        public float[] EncodeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Текст не может быть пустым", nameof(text));

            var tokenIds = RuClipPreprocessor.Tokenize(text, _tokenizer);

            var tensor = new DenseTensor<long>(
                tokenIds,
                new ReadOnlySpan<int>(new[] { 1, RuClipPreprocessor.MaxTokenLength }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", tensor)
            };

            using var results = _textSession.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();

            return L2Normalize(output);
        }

        public float[] EncodeImage(SKBitmap image)
        {
            if (image is null)
                throw new ArgumentNullException(nameof(image));

            var tensorData = RuClipPreprocessor.ImageToTensor(image);

            var tensor = new DenseTensor<float>(
                tensorData,
                new ReadOnlySpan<int>(new[] { 1, 3, RuClipPreprocessor.ImageSize, RuClipPreprocessor.ImageSize }));

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", tensor)
            };

            using var results = _visualSession.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();

            return L2Normalize(output);
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Векторы должны быть одинаковой длины");

            float dot = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            float denominator = MathF.Sqrt(normA) * MathF.Sqrt(normB);
            if (denominator < 1e-12f)
                return 0f;

            return dot / denominator;
        }

        private static float[] L2Normalize(float[] vector)
        {
            float norm = 0f;
            for (int i = 0; i < vector.Length; i++)
                norm += vector[i] * vector[i];

            norm = MathF.Sqrt(norm);
            if (norm < 1e-12f)
                return vector;

            var result = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
                result[i] = vector[i] / norm;

            return result;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _textSession.Dispose();
            _visualSession.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

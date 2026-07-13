using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    public class RuClipPreprocessorTests
    {
        [Fact]
        public void ImageToTensor_ReturnsCorrectLength()
        {
            using var bitmap = new SKBitmap(64, 64);
            var tensor = RuClipPreprocessor.ImageToTensor(bitmap);
            Assert.Equal(3 * RuClipPreprocessor.ImageSize * RuClipPreprocessor.ImageSize, tensor.Length);
        }

        [Fact]
        public void ImageToTensor_ValuesInRange()
        {
            using var bitmap = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(128, 128, 128));

            var tensor = RuClipPreprocessor.ImageToTensor(bitmap);

            Assert.All(tensor, v => Assert.InRange(v, -3f, 3f));
        }

        [Fact]
        public void Tokenize_ReturnsCorrectLength()
        {
            var tokenizerPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "SemanticRasterEditor", "Assets", "Models", "tokenizer.json");
            tokenizerPath = Path.GetFullPath(tokenizerPath);

            if (!File.Exists(tokenizerPath))
                return;

            using var tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokenizerPath);
            var result = RuClipPreprocessor.Tokenize("тест", tokenizer);

            Assert.Equal(RuClipPreprocessor.MaxTokenLength, result.Length);
        }

        [Fact]
        public void Tokenize_StartsWithBos()
        {
            var tokenizerPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "SemanticRasterEditor", "Assets", "Models", "tokenizer.json");
            tokenizerPath = Path.GetFullPath(tokenizerPath);

            if (!File.Exists(tokenizerPath))
                return;

            using var tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokenizerPath);
            var result = RuClipPreprocessor.Tokenize("hello", tokenizer);

            Assert.Equal(RuClipPreprocessor.BosToken, result[0]);
        }

        [Fact]
        public void Tokenize_EndsWithEos()
        {
            var tokenizerPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "SemanticRasterEditor", "Assets", "Models", "tokenizer.json");
            tokenizerPath = Path.GetFullPath(tokenizerPath);

            if (!File.Exists(tokenizerPath))
                return;

            using var tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokenizerPath);
            var result = RuClipPreprocessor.Tokenize("hi", tokenizer);

            bool hasEos = false;
            for (int i = 1; i < result.Length; i++)
            {
                if (result[i] == RuClipPreprocessor.EosToken)
                {
                    hasEos = true;
                    break;
                }
                if (result[i] == RuClipPreprocessor.PadToken)
                    break;
            }
            Assert.True(hasEos, "EOS token should be present after BOS");
        }

        [Fact]
        public void Tokenize_PadsWithZeros()
        {
            var tokenizerPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "SemanticRasterEditor", "Assets", "Models", "tokenizer.json");
            tokenizerPath = Path.GetFullPath(tokenizerPath);

            if (!File.Exists(tokenizerPath))
                return;

            using var tokenizer = new Tokenizers.DotNet.Tokenizer(vocabPath: tokenizerPath);
            var result = RuClipPreprocessor.Tokenize("a", tokenizer);

            Assert.Equal(RuClipPreprocessor.PadToken, result[76]);
        }
    }
}

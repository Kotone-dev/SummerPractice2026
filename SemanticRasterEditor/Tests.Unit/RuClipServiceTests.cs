using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    [Trait("Category", "Integration")]
    public class RuClipServiceTests
    {
        private static string GetModelsDir()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SemanticRasterEditor", "Assets", "Models");
            return Path.GetFullPath(dir);
        }

        private static bool ModelsExist()
        {
            var dir = GetModelsDir();
            return File.Exists(Path.Combine(dir, "ruclip_textual_int8.onnx"))
                && File.Exists(Path.Combine(dir, "ruclip_visual_int8.onnx"))
                && File.Exists(Path.Combine(dir, "tokenizer.json"));
        }

        [Fact]
        public void LoadFromDirectory_LoadsModel()
        {
            if (!ModelsExist())
                return;

            using var service = RuClipService.LoadFromDirectory(GetModelsDir());
            Assert.NotNull(service);
        }

        [Fact]
        public void EncodeText_ReturnsVector()
        {
            if (!ModelsExist())
                return;

            using var service = RuClipService.LoadFromDirectory(GetModelsDir());
            var embedding = service.EncodeText("собака");

            Assert.NotNull(embedding);
            Assert.Equal(768, embedding.Length);
        }

        [Fact]
        public void EncodeImage_ReturnsVector()
        {
            if (!ModelsExist())
                return;

            using var service = RuClipService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SKBitmap(64, 64);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);

            var embedding = service.EncodeImage(bitmap);

            Assert.NotNull(embedding);
            Assert.Equal(768, embedding.Length);
        }

        [Fact]
        public void CosineSimilarity_SameVector_ReturnsOne()
        {
            var vec = new float[] { 1f, 0f, 0f };
            float similarity = RuClipService.CosineSimilarity(vec, vec);
            Assert.InRange(similarity, 0.99f, 1.01f);
        }

        [Fact]
        public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
        {
            var a = new float[] { 1f, 0f, 0f };
            var b = new float[] { 0f, 1f, 0f };
            float similarity = RuClipService.CosineSimilarity(a, b);
            Assert.InRange(similarity, -0.01f, 0.01f);
        }
    }
}

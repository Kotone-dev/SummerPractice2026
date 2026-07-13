using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    [Trait("Category", "Integration")]
    public class TextSearchServiceTests
    {
        private static string GetModelsDir()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SemanticRasterEditor", "Assets", "Models");
            return Path.GetFullPath(dir);
        }

        private static bool ModelsExist()
        {
            var dir = GetModelsDir();
            return File.Exists(Path.Combine(dir, "fast_sam_x.onnx"))
                && File.Exists(Path.Combine(dir, "ruclip_textual_int8.onnx"))
                && File.Exists(Path.Combine(dir, "ruclip_visual_int8.onnx"))
                && File.Exists(Path.Combine(dir, "tokenizer.json"));
        }

        [Fact]
        public void LoadFromDirectory_LoadsModel()
        {
            if (!ModelsExist())
                return;

            using var service = TextSearchService.LoadFromDirectory(GetModelsDir());
            Assert.NotNull(service);
        }

        [Fact]
        public void Search_ReturnsResultOrNull()
        {
            if (!ModelsExist())
                return;

            using var service = TextSearchService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SKBitmap(64, 64);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);

            var result = service.Search(bitmap, "тест");

            result?.Dispose();
        }
    }
}

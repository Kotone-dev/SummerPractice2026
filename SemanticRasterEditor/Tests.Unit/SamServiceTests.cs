using Editor.Services;

namespace Tests.Unit
{
    [Trait("Category", "Integration")]
    public class SamServiceTests
    {
        private static string GetModelsDir()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SemanticRasterEditor", "Assets", "Models");
            return Path.GetFullPath(dir);
        }

        private static bool ModelsExist()
        {
            var dir = GetModelsDir();
            return File.Exists(Path.Combine(dir, "mobile_sam_encoder.onnx"))
                && File.Exists(Path.Combine(dir, "mobile_sam_decoder.onnx"));
        }

        [Fact]
        public void LoadFromDirectory_LoadsModels()
        {
            if (!ModelsExist())
                return;

            using var service = SamService.LoadFromDirectory(GetModelsDir());
            Assert.NotNull(service);
        }

        [Fact]
        public void Predict_ReturnsMask()
        {
            if (!ModelsExist())
                return;

            using var service = SamService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SkiaSharp.SKBitmap(100, 100);

            using var mask = service.Predict(bitmap, 50, 50);

            Assert.NotNull(mask);
        }

        [Fact]
        public void Predict_MaskDimensionsMatchImage()
        {
            if (!ModelsExist())
                return;

            using var service = SamService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SkiaSharp.SKBitmap(200, 150);

            using var mask = service.Predict(bitmap, 100, 75);

            Assert.Equal(200, mask.Width);
            Assert.Equal(150, mask.Height);
        }
    }
}

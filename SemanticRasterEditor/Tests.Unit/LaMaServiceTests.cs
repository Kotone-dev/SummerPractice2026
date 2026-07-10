using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    [Trait("Category", "Integration")]
    public class LaMaServiceTests
    {
        private static string GetModelsDir()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SemanticRasterEditor", "Editor.App", "Assets", "Models");
            return Path.GetFullPath(dir);
        }

        private static bool ModelsExist()
        {
            return File.Exists(Path.Combine(GetModelsDir(), "lama_fp32.onnx"));
        }

        [Fact]
        public void LoadFromDirectory_LoadsModel()
        {
            if (!ModelsExist())
                return;

            using var service = LaMaService.LoadFromDirectory(GetModelsDir());
            Assert.NotNull(service);
        }

        [Fact]
        public void Inpaint_ReturnsBitmap()
        {
            if (!ModelsExist())
                return;

            using var service = LaMaService.LoadFromDirectory(GetModelsDir());
            using var image = new SKBitmap(64, 64);
            using var mask = new SKBitmap(64, 64);

            using var canvas = new SKCanvas(mask);
            canvas.Clear(new SKColor(255, 255, 255));

            using var result = service.Inpaint(image, mask);

            Assert.NotNull(result);
        }

        [Fact]
        public void Inpaint_OutputDimensionsMatchInput()
        {
            if (!ModelsExist())
                return;

            using var service = LaMaService.LoadFromDirectory(GetModelsDir());
            using var image = new SKBitmap(200, 150);
            using var mask = new SKBitmap(200, 150);

            using var canvas = new SKCanvas(mask);
            canvas.Clear(new SKColor(255, 255, 255));

            using var result = service.Inpaint(image, mask);

            Assert.Equal(200, result.Width);
            Assert.Equal(150, result.Height);
        }
    }
}

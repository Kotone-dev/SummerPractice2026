using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    [Trait("Category", "Integration")]
    public class FastSamServiceTests
    {
        private static string GetModelsDir()
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SemanticRasterEditor", "Assets", "Models");
            return Path.GetFullPath(dir);
        }

        private static bool ModelsExist()
        {
            return File.Exists(Path.Combine(GetModelsDir(), "fast_sam_x.onnx"));
        }

        [Fact]
        public void LoadFromDirectory_LoadsModel()
        {
            if (!ModelsExist())
                return;

            using var service = FastSamService.LoadFromDirectory(GetModelsDir());
            Assert.NotNull(service);
        }

        [Fact]
        public void SegmentAll_ReturnsList()
        {
            if (!ModelsExist())
                return;

            using var service = FastSamService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SKBitmap(64, 64);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);

            var masks = service.SegmentAll(bitmap);

            Assert.NotNull(masks);
        }

        [Fact]
        public void SegmentAll_MasksHaveCorrectDimensions()
        {
            if (!ModelsExist())
                return;

            using var service = FastSamService.LoadFromDirectory(GetModelsDir());
            using var bitmap = new SKBitmap(200, 150);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Blue);

            var masks = service.SegmentAll(bitmap);

            foreach (var mask in masks)
            {
                Assert.Equal(200, mask.Width);
                Assert.Equal(150, mask.Height);
                mask.Dispose();
            }
        }
    }
}

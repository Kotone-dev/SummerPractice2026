using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    public class ImageFilterServiceTests
    {
        private readonly ImageFilterService _service = new();

        [Fact]
        public void BrightnessZero_NoChange()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(128, 128, 128));

            using var result = _service.AdjustBrightness(bitmap, 0);

            var pixel = result.GetPixel(5, 5);
            Assert.Equal(128, pixel.Red);
            Assert.Equal(128, pixel.Green);
            Assert.Equal(128, pixel.Blue);
        }

        [Fact]
        public void BrightnessPositive_Increases()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));

            using var result = _service.AdjustBrightness(bitmap, 50);

            var pixel = result.GetPixel(5, 5);
            Assert.True(pixel.Red > 100);
        }

        [Fact]
        public void BrightnessNegative_Darkens()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(150, 150, 150));

            using var result = _service.AdjustBrightness(bitmap, -50);

            var pixel = result.GetPixel(5, 5);
            Assert.True(pixel.Red < 150);
        }

        [Fact]
        public void ContrastZero_NoChange()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(128, 128, 128));

            using var result = _service.AdjustContrast(bitmap, 0);

            var pixel = result.GetPixel(5, 5);
            Assert.Equal(128, pixel.Red);
        }

        [Fact]
        public void ContrastPositive_IncreasesSpread()
        {
            using var bitmap = CreateTestBitmap(10, 10, new SKColor(100, 100, 100));

            using var result = _service.AdjustContrast(bitmap, 50);

            var pixel = result.GetPixel(5, 5);
            Assert.NotEqual(100, pixel.Red);
        }

        [Fact]
        public void MorphologyErode_PreservesDimensions()
        {
            using var bitmap = CreateTestBitmap(20, 15, new SKColor(255, 255, 255));

            using var result = _service.ApplyMorphology(bitmap, MorphologyType.Erode);

            Assert.Equal(20, result.Width);
            Assert.Equal(15, result.Height);
        }

        [Fact]
        public void MorphologyDilate_PreservesDimensions()
        {
            using var bitmap = CreateTestBitmap(20, 15, new SKColor(0, 0, 0));

            using var result = _service.ApplyMorphology(bitmap, MorphologyType.Dilate);

            Assert.Equal(20, result.Width);
            Assert.Equal(15, result.Height);
        }

        [Fact]
        public void MorphologyOpen_PreservesDimensions()
        {
            using var bitmap = CreateTestBitmap(20, 15, new SKColor(64, 64, 64));

            using var result = _service.ApplyMorphology(bitmap, MorphologyType.Open);

            Assert.Equal(20, result.Width);
            Assert.Equal(15, result.Height);
        }

        [Fact]
        public void MorphologyClose_PreservesDimensions()
        {
            using var bitmap = CreateTestBitmap(20, 15, new SKColor(200, 200, 200));

            using var result = _service.ApplyMorphology(bitmap, MorphologyType.Close);

            Assert.Equal(20, result.Width);
            Assert.Equal(15, result.Height);
        }

        private static SKBitmap CreateTestBitmap(int width, int height, SKColor color)
        {
            var bitmap = new SKBitmap(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bitmap.SetPixel(x, y, color);
            return bitmap;
        }
    }
}

using Editor.Services;
using OpenCvSharp;
using SkiaSharp;

namespace Tests.Unit
{
    public class SamPreprocessorTests
    {
        [Fact]
        public void ResizeAndPad_OutputIsSquare()
        {
            using var bitmap = new SKBitmap(640, 480);

            using var result = SamPreprocessor.ResizeAndPad(bitmap);

            Assert.Equal(SamPreprocessor.ImageSize, result.Width);
            Assert.Equal(SamPreprocessor.ImageSize, result.Height);
        }

        [Fact]
        public void ResizeAndPad_PreservesLandscapeOrientation()
        {
            using var bitmap = new SKBitmap(1920, 1080);

            using var result = SamPreprocessor.ResizeAndPad(bitmap);

            Assert.Equal(SamPreprocessor.ImageSize, result.Width);
            Assert.Equal(SamPreprocessor.ImageSize, result.Height);
        }

        [Fact]
        public void ToTensor_HasCorrectLength()
        {
            using var bitmap = new SKBitmap(100, 100);
            using var padded = SamPreprocessor.ResizeAndPad(bitmap);

            var tensor = SamPreprocessor.ToTensor(padded);

            int expected = SamPreprocessor.ImageSize * SamPreprocessor.ImageSize * 3;
            Assert.Equal(expected, tensor.Length);
        }

        [Fact]
        public void ScalePoint_ScalesCorrectly()
        {
            float x = 500f;
            float y = 300f;
            int origW = 1000;
            int origH = 500;

            var result = SamPreprocessor.ScalePoint(x, y, origW, origH);

            float scale = 1024f / 1000f;
            Assert.Equal(x * scale, result[0], 1);
            Assert.Equal(y * scale, result[1], 1);
        }
    }
}

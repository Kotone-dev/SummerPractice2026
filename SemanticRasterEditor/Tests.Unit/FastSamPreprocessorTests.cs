using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    public class FastSamPreprocessorTests
    {
        [Fact]
        public void ResizeAndPad_ReturnsSquareImage()
        {
            using var bitmap = new SKBitmap(200, 100);
            using var padded = FastSamPreprocessor.ResizeAndPad(bitmap);

            Assert.Equal(FastSamPreprocessor.ImageSize, padded.Width);
            Assert.Equal(FastSamPreprocessor.ImageSize, padded.Height);
        }

        [Fact]
        public void ResizeAndPad_SquareInput_RemainsSquare()
        {
            using var bitmap = new SKBitmap(512, 512);
            using var padded = FastSamPreprocessor.ResizeAndPad(bitmap);

            Assert.Equal(FastSamPreprocessor.ImageSize, padded.Width);
            Assert.Equal(FastSamPreprocessor.ImageSize, padded.Height);
        }

        [Fact]
        public void ToFloatTensor_ReturnsCorrectLength()
        {
            using var bitmap = new SKBitmap(64, 64);
            using var padded = FastSamPreprocessor.ResizeAndPad(bitmap);
            var tensor = FastSamPreprocessor.ToFloatTensor(padded);

            Assert.Equal(3 * FastSamPreprocessor.ImageSize * FastSamPreprocessor.ImageSize, tensor.Length);
        }

        [Fact]
        public void ToFloatTensor_ValuesBetween0And1()
        {
            using var bitmap = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            using var padded = FastSamPreprocessor.ResizeAndPad(bitmap);
            var tensor = FastSamPreprocessor.ToFloatTensor(padded);

            Assert.All(tensor, v => Assert.InRange(v, 0f, 1f));
        }

        [Fact]
        public void ComputeScale_ReturnsCorrectValue()
        {
            float scale = FastSamPreprocessor.ComputeScale(2048, 1024);
            Assert.InRange(scale, 0.49f, 0.51f);
        }
    }
}

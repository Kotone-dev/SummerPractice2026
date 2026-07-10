using Editor.Services;
using SkiaSharp;

namespace Tests.Unit
{
    public class LaMaPreprocessorTests
    {
        [Fact]
        public void ImageToTensor_HasCorrectLength()
        {
            using var bitmap = new SKBitmap(100, 100);

            var tensor = LaMaPreprocessor.ImageToTensor(bitmap);

            int expected = 3 * LaMaPreprocessor.InputSize * LaMaPreprocessor.InputSize;
            Assert.Equal(expected, tensor.Length);
        }

        [Fact]
        public void ImageToTensor_ValuesInRange()
        {
            using var bitmap = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(128, 64, 32));

            var tensor = LaMaPreprocessor.ImageToTensor(bitmap);

            Assert.All(tensor, v => Assert.InRange(v, 0f, 255f));
        }

        [Fact]
        public void MaskToTensor_HasCorrectLength()
        {
            using var bitmap = new SKBitmap(100, 100);

            var tensor = LaMaPreprocessor.MaskToTensor(bitmap);

            int expected = LaMaPreprocessor.InputSize * LaMaPreprocessor.InputSize;
            Assert.Equal(expected, tensor.Length);
        }

        [Fact]
        public void MaskToTensor_ValuesAreBinary()
        {
            using var bitmap = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(new SKColor(255, 255, 255));

            var tensor = LaMaPreprocessor.MaskToTensor(bitmap);

            Assert.All(tensor, v => Assert.True(v == 0f || v == 1f));
        }

        [Fact]
        public void Postprocess_OutputDimensionsMatchOriginal()
        {
            int origW = 800;
            int origH = 600;
            int channelSize = LaMaPreprocessor.InputSize * LaMaPreprocessor.InputSize;
            var data = new float[3 * channelSize];

            using var result = LaMaPreprocessor.Postprocess(data, origW, origH);

            Assert.Equal(origW, result.Width);
            Assert.Equal(origH, result.Height);
        }
    }
}

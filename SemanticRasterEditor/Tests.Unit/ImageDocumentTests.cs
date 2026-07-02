using Editor.Models;
using SkiaSharp;

namespace Tests.Unit
{
    public class ImageDocumentTests
    {
        [Fact]
        public void NewDocHasNoBitmap()
        {
            var doc = new ImageDocument();

            Assert.Null(doc.Bitmap);
            Assert.Equal(0, doc.Width);
            Assert.Equal(0, doc.Height);
        }

        [Fact]
        public void SetBitmapTest()
        {
            var doc = new ImageDocument();
            using var bitmap = new SKBitmap(100, 200);

            doc.SetBitmap(bitmap);

            Assert.NotNull(doc.Bitmap);
            Assert.Equal(100, doc.Width);
            Assert.Equal(200, doc.Height);
        }

        [Fact]
        public void SetBitmapMarksModified()
        {
            var doc = new ImageDocument();
            using var bitmap = new SKBitmap(10, 10);

            doc.SetBitmap(bitmap);

            Assert.True(doc.IsModified);
        }

        [Fact]
        public void MarkSavedTest()
        {
            var doc = new ImageDocument();
            using var bitmap = new SKBitmap(10, 10);
            doc.SetBitmap(bitmap);

            doc.MarkSaved("/tmp/test.png");

            Assert.False(doc.IsModified);
            Assert.Equal("/tmp/test.png", doc.FilePath);
        }

        [Fact]
        public void ClearTest()
        {
            var doc = new ImageDocument();
            using var bitmap = new SKBitmap(10, 10);
            doc.SetBitmap(bitmap);
            doc.MarkSaved("/tmp/test.png");

            doc.Clear();

            Assert.Null(doc.Bitmap);
            Assert.Null(doc.FilePath);
            Assert.False(doc.IsModified);
            Assert.Equal(0, doc.Width);
            Assert.Equal(0, doc.Height);
        }

        [Fact]
        public void SetBitmapDisposesPrevious()
        {
            var doc = new ImageDocument();
            var bitmap1 = new SKBitmap(10, 10);
            doc.SetBitmap(bitmap1);

            var bitmap2 = new SKBitmap(20, 20);
            doc.SetBitmap(bitmap2);

            Assert.Equal(20, doc.Width);
            Assert.Equal(20, doc.Height);

            bitmap1.Dispose();
            bitmap2.Dispose();
        }
    }
}

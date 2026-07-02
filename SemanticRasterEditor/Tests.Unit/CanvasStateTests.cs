using Editor.Core;
using SkiaSharp;

namespace Tests.Unit
{
    public class CanvasStateTests
    {
        [Fact]
        public void ZoomInTest()
        {
            var state = new CanvasState();
            var before = state.Zoom;

            state.ZoomIn();

            Assert.True(state.Zoom > before);
        }

        [Fact]
        public void ZoomOutTest()
        {
            var state = new CanvasState();
            var before = state.Zoom;

            state.ZoomOut();

            Assert.True(state.Zoom < before);
        }

        [Fact]
        public void ZoomMax()
        {
            var state = new CanvasState();

            for (int i = 0; i < 100; i++)
                state.ZoomIn();

            Assert.True(state.Zoom <= CanvasState.MaxZoom);
        }

        [Fact]
        public void ZoomMin()
        {
            var state = new CanvasState();

            for (int i = 0; i < 100; i++)
                state.ZoomOut();

            Assert.True(state.Zoom >= CanvasState.MinZoom);
        }

        [Fact]
        public void ResetTest()
        {
            var state = new CanvasState();
            state.ZoomIn();
            state.ZoomIn();

            state.ResetZoom();

            Assert.Equal(1f, state.Zoom);
            Assert.Equal(SKPoint.Empty, state.PanOffset);
        }

        [Fact]
        public void FitToWindowZoom()
        {
            var state = new CanvasState();
            var canvasSize = new SKSizeI(800, 600);
            var imageSize = new SKSizeI(1920, 1080);

            state.FitToWindow(canvasSize, imageSize);

            Assert.True(state.Zoom <= 1f);
        }

        [Fact]
        public void FitToWindowCenter()
        {
            var state = new CanvasState();
            var canvasSize = new SKSizeI(800, 600);
            var imageSize = new SKSizeI(100, 200);

            state.FitToWindow(canvasSize, imageSize);

            Assert.True(state.PanOffset.X > 0);
        }

        [Fact]
        public void FitToWindowZeroSize()
        {
            var state = new CanvasState();
            state.ZoomIn();

            state.FitToWindow(new SKSizeI(0, 0), new SKSizeI(100, 100));

            Assert.NotEqual(1f, state.Zoom);
        }

        [Fact]
        public void MovePanTest()
        {
            var state = new CanvasState();

            state.MovePan(50, 30);

            Assert.Equal(50f, state.PanOffset.X);
            Assert.Equal(30f, state.PanOffset.Y);
        }

        [Fact]
        public void SetZoomTest()
        {
            var state = new CanvasState();

            state.SetZoom(2.5f);

            Assert.Equal(2.5f, state.Zoom);
        }
    }
}

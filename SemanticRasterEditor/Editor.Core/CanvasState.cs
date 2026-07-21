using SkiaSharp;

namespace Editor.Core
{
    public class CanvasState
    {
        private float _zoom = 1f;
        private SKPoint _panOffset;

        public const float MinZoom = 0.1f;
        public const float MaxZoom = 32f;

        public float Zoom => _zoom;

        public SKPoint PanOffset => _panOffset;

        public void ZoomIn()
        {
            SetZoom(_zoom * 1.25f);
        }

        public void ZoomOut()
        {
            SetZoom(_zoom / 1.25f);
        }

        public void SetZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                return;
            _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        }

        public void ResetZoom()
        {
            _zoom = 1f;
            _panOffset = SKPoint.Empty;
        }

        public void FitToWindow(SKSizeI canvasSize, SKSizeI imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return;
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
                return;

            float scaleX = (float)canvasSize.Width / imageSize.Width;
            float scaleY = (float)canvasSize.Height / imageSize.Height;
            _zoom = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);

            float offsetX = (canvasSize.Width - imageSize.Width * _zoom) / 2f;
            float offsetY = (canvasSize.Height - imageSize.Height * _zoom) / 2f;
            _panOffset = new SKPoint(offsetX, offsetY);
        }

        public void SetPanOffset(SKPoint offset)
        {
            _panOffset = offset;
        }

        public void MovePan(float dx, float dy)
        {
            _panOffset = new SKPoint(_panOffset.X + dx, _panOffset.Y + dy);
        }
    }
}

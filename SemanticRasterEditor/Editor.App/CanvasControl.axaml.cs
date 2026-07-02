using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Editor.Core;
using SkiaSharp;

namespace Editor.App
{
    public partial class CanvasControl : UserControl
    {
        private readonly CanvasState _state = new();
        private readonly ScaleTransform _scaleTransform = new();
        private readonly TranslateTransform _translateTransform = new();
        private SKBitmap? _currentBitmap;
        private Bitmap? _displayBitmap;
        private Point _lastPanPosition;
        private bool _isPanning;

        public CanvasState State => _state;

        public float Zoom => _state.Zoom;

        public event Action<float>? ZoomChanged;

        public CanvasControl()
        {
            InitializeComponent();

            var transforms = new TransformGroup();
            transforms.Children.Add(_scaleTransform);
            transforms.Children.Add(_translateTransform);
            ImageControl.RenderTransform = transforms;

            PointerWheelChanged += OnPointerWheelChanged;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }

        public void SetImage(SKBitmap? bitmap)
        {
            _currentBitmap = bitmap;

            if (bitmap is null)
            {
                ImageControl.Source = null;
                _displayBitmap?.Dispose();
                _displayBitmap = null;
                return;
            }

            _displayBitmap?.Dispose();

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            _displayBitmap = new Bitmap(stream);
            ImageControl.Source = _displayBitmap;
            UpdateTransform();
        }

        public void FitToWindow()
        {
            if (_currentBitmap is null)
                return;

            var canvasSize = new SKSizeI(
                (int)Bounds.Width,
                (int)Bounds.Height);

            if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
                return;

            var imageSize = new SKSizeI(
                _currentBitmap.Width,
                _currentBitmap.Height);

            _state.FitToWindow(canvasSize, imageSize);
            UpdateTransform();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        public void ResetZoom()
        {
            _state.ResetZoom();
            UpdateTransform();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_currentBitmap is null)
                return;

            var position = e.GetPosition(this);
            var zoomFactor = e.Delta.Y > 0 ? 1.1f : 1f / 1.1f;
            var newZoom = _state.Zoom * zoomFactor;

            var mouseX = (float)position.X;
            var mouseY = (float)position.Y;

            var oldZoom = _state.Zoom;
            _state.SetZoom(newZoom);

            var panX = mouseX - (mouseX - _state.PanOffset.X) * (_state.Zoom / oldZoom);
            var panY = mouseY - (mouseY - _state.PanOffset.Y) * (_state.Zoom / oldZoom);
            _state.SetPanOffset(new SKPoint(panX, panY));

            UpdateTransform();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed ||
                e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(this);
                e.Pointer.Capture(this);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning)
                return;

            var position = e.GetPosition(this);
            var dx = (float)(position.X - _lastPanPosition.X);
            var dy = (float)(position.Y - _lastPanPosition.Y);

            _state.MovePan(dx, dy);
            _lastPanPosition = position;

            UpdateTransform();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
            }
        }

        // TODO: потом добавить отрисовку инструментов поверх изображения
        private void UpdateTransform()
        {
            _scaleTransform.ScaleX = _state.Zoom;
            _scaleTransform.ScaleY = _state.Zoom;
            _translateTransform.X = _state.PanOffset.X;
            _translateTransform.Y = _state.PanOffset.Y;
        }
    }
}

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
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
        private int _imageWidth;
        private int _imageHeight;
        private Bitmap? _displayBitmap;
        private Point _lastPanPosition;
        private bool _isPanning;

        public CanvasState State => _state;

        public float Zoom => _state.Zoom;

        public SKPoint PanOffset => _state.PanOffset;

        public bool SmartSelectMode { get; set; }

        public bool HasImage => _imageWidth > 0 && _imageHeight > 0;

        public event Action<float>? ZoomChanged;

        public event Action<float, float>? ClickOnImage;

        public event Action<float, float>? CursorPositionChanged;

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
            if (bitmap is null)
            {
                _imageWidth = 0;
                _imageHeight = 0;
                SetImageSource(null);
                ShowCheckerboard();
                return;
            }

            _imageWidth = bitmap.Width;
            _imageHeight = bitmap.Height;
            HideCheckerboard();

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            SetImageSource(new Bitmap(stream));
            UpdateTransform();
        }

        private void SetImageSource(Bitmap? source)
        {
            _displayBitmap?.Dispose();
            _displayBitmap = source;
            ImageControl.Source = _displayBitmap;
        }

        private void ShowCheckerboard()
        {
            if (CheckerboardCanvas is null)
                return;

            CheckerboardCanvas.Children.Clear();

            var tileSize = 8;
            var light = new SolidColorBrush(Color.Parse("#FFFFFF"));
            var dark = new SolidColorBrush(Color.Parse("#E0E0E0"));

            int cols = (int)(Bounds.Width / tileSize) + 2;
            int rows = (int)(Bounds.Height / tileSize) + 2;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var rect = new Rectangle
                    {
                        Width = tileSize,
                        Height = tileSize,
                        Fill = (x + y) % 2 == 0 ? light : dark
                    };
                    Canvas.SetLeft(rect, x * tileSize);
                    Canvas.SetTop(rect, y * tileSize);
                    CheckerboardCanvas.Children.Add(rect);
                }
            }

            CheckerboardCanvas.Opacity = 1;
        }

        private void HideCheckerboard()
        {
            if (CheckerboardCanvas is not null)
                CheckerboardCanvas.Opacity = 0;
        }

        public void FitToWindow()
        {
            if (!HasImage)
                return;

            var canvasSize = new SKSizeI(
                (int)Bounds.Width,
                (int)Bounds.Height);

            if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
                return;

            var imageSize = new SKSizeI(_imageWidth, _imageHeight);

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
            if (!HasImage)
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
            else if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && SmartSelectMode && HasImage)
            {
                var pos = e.GetPosition(this);
                float pixelX = ((float)pos.X - _state.PanOffset.X) / _state.Zoom;
                float pixelY = ((float)pos.Y - _state.PanOffset.Y) / _state.Zoom;

                if (pixelX >= 0 && pixelX < _imageWidth && pixelY >= 0 && pixelY < _imageHeight)
                    ClickOnImage?.Invoke(pixelX, pixelY);
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var position = e.GetPosition(this);
            float pixelX = ((float)position.X - _state.PanOffset.X) / _state.Zoom;
            float pixelY = ((float)position.Y - _state.PanOffset.Y) / _state.Zoom;
            CursorPositionChanged?.Invoke(pixelX, pixelY);

            if (!_isPanning)
                return;

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

        private void UpdateTransform()
        {
            _scaleTransform.ScaleX = _state.Zoom;
            _scaleTransform.ScaleY = _state.Zoom;
            _translateTransform.X = _state.PanOffset.X;
            _translateTransform.Y = _state.PanOffset.Y;
        }
    }
}

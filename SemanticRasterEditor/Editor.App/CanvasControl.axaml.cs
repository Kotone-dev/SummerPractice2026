using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Editor.Core;
using Editor.Models;
using Editor.Services;
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
        private bool _suppressScrollBarUpdate;

        private LayerService? _layerService;
        private ImageFilterService? _filterService;

        public CanvasState State => _state;
        public float Zoom => _state.Zoom;
        public SKPoint PanOffset => _state.PanOffset;
        public bool HasImage => _imageWidth > 0 && _imageHeight > 0;
        public bool HasSelection => _selectionRect.HasValue || _selectionPoints.Count >= 3;

        public SKRect? SelectionRect => _selectionRect.HasValue
            ? new SKRect(
                Math.Max(0, _selectionRect.Value.Left),
                Math.Max(0, _selectionRect.Value.Top),
                Math.Min(_imageWidth, _selectionRect.Value.Right),
                Math.Min(_imageHeight, _selectionRect.Value.Bottom))
            : null;

        public SKBitmap? GetSelectionBitmap()
        {
            if (!_selectionRect.HasValue) return null;
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return null;

            var r = _selectionRect.Value;
            int x = Math.Max(0, (int)r.Left);
            int y = Math.Max(0, (int)r.Top);
            int w = Math.Min(active.Bitmap.Width - x, (int)r.Width);
            int h = Math.Min(active.Bitmap.Height - y, (int)r.Height);
            if (w <= 0 || h <= 0) return null;

            var result = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            active.Bitmap.ExtractSubset(result, new SKRectI(x, y, x + w, y + h));
            return result;
        }

        public void DeleteSelection()
        {
            if (!_selectionRect.HasValue) return;
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            using var canvas = new SKCanvas(active.Bitmap);
            using var paint = new SKPaint { BlendMode = SKBlendMode.Clear };
            var r = _selectionRect.Value;
            canvas.DrawRect(r, paint);
            ClearSelection();
            ImageModified?.Invoke();
        }

        public void SelectAll()
        {
            if (!HasImage) return;
            _selectionRect = new SKRect(0, 0, _imageWidth, _imageHeight);
            UpdateSelectionOverlay();
            StartMarchingAnts();
        }

        public void DeselectAll()
        {
            ClearSelection();
        }

        public void InvertSelection()
        {
            if (!_selectionRect.HasValue && _selectionPoints.Count < 3) return;
            if (_selectionRect.HasValue)
            {
                _selectionRect = new SKRect(0, 0, _imageWidth, _imageHeight);
                _selectionPath?.Dispose();
                _selectionPath = null;
                _selectionPoints.Clear();
                UpdateSelectionOverlay();
                StartMarchingAnts();
            }
        }

        public SKPoint[]? GetLassoPoints()
        {
            if (_selectionPoints.Count < 3) return null;
            return _selectionPoints.ToArray();
        }

        public SKPath? GetLassoPath()
        {
            if (_selectionPath is null) return null;
            return _selectionPath;
        }

        public event Action<float>? ZoomChanged;
        public event Action<float, float>? ClickOnImage;
        public event Action<float, float>? CursorPositionChanged;
        public event Action? BeforeImageModified;
        public event Action? ImageModified;
        public event EventHandler<SKColor>? ColorPicked;

        public string CurrentTool { get; set; } = "Move";
        public SKColor BrushColor { get; set; } = SKColors.Black;
        public int BrushSize { get; set; } = 20;
        public float BrushOpacity { get; set; } = 1f;
        public string CurrentBlendMode { get; set; } = "Normal";
        public SKBitmap? ClipboardBitmap { get; set; }

        public LayerService? LayerService
        {
            get => _layerService;
            set => _layerService = value;
        }

        public ImageFilterService? FilterService
        {
            get => _filterService;
            set => _filterService = value;
        }

        public bool SmartSelectMode { get; set; }

        private bool _isDragging;
        private Point _startScreenPoint;

        private SKRect? _selectionRect;
        private SKPath? _selectionPath;
        private readonly List<SKPoint> _selectionPoints = new();
        private float _marchingAntsOffset;
        private DispatcherTimer? _marchingAntsTimer;

        private bool _isPainting;
        private SKBitmap? _workingBitmap;
        private SKPoint _lastPaintPoint;

        private float _moveOffsetX;
        private float _moveOffsetY;

        private SKRect? _cropRect;
        private bool _cropActive;

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

            if (HScrollBar is not null)
                HScrollBar.ValueChanged += OnHScrollBarValueChanged;
            if (VScrollBar is not null)
                VScrollBar.ValueChanged += OnVScrollBarValueChanged;

            _marchingAntsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _marchingAntsTimer.Tick += (_, _) =>
            {
                _marchingAntsOffset = (_marchingAntsOffset + 1) % 8;
                UpdateMarchingAntsVisual();
            };

            if (CtxCopy is not null) CtxCopy.Click += (_, _) => CopyToClipboard();
            if (CtxCut is not null) CtxCut.Click += (_, _) => CutToClipboard();
            if (CtxPaste is not null) CtxPaste.Click += (_, _) => PasteFromClipboard();
            if (CtxDelete is not null) CtxDelete.Click += (_, _) => DeleteSelection();
            if (CtxDeselect is not null) CtxDeselect.Click += (_, _) => DeselectAll();
            if (CtxInvert is not null) CtxInvert.Click += (_, _) => InvertSelection();
            if (CtxSelectAll is not null) CtxSelectAll.Click += (_, _) => SelectAll();

            ContextMenu.Opening += OnContextMenuOpening;
        }

        private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CtxCopy is not null) CtxCopy.IsEnabled = HasSelection;
            if (CtxCut is not null) CtxCut.IsEnabled = HasSelection;
            if (CtxPaste is not null) CtxPaste.IsEnabled = ClipboardBitmap is not null;
            if (CtxDelete is not null) CtxDelete.IsEnabled = HasSelection;
            if (CtxDeselect is not null) CtxDeselect.IsEnabled = HasSelection;
            if (CtxInvert is not null) CtxInvert.IsEnabled = HasSelection;
        }

        private void CopyToClipboard()
        {
            var bmp = GetSelectionBitmap();
            if (bmp is null) return;
            ClipboardBitmap = bmp;
        }

        private void CutToClipboard()
        {
            var bmp = GetSelectionBitmap();
            if (bmp is null) return;
            ClipboardBitmap = bmp;
            DeleteSelection();
        }

        private void PasteFromClipboard()
        {
            if (ClipboardBitmap is null) return;
            PasteBitmap(ClipboardBitmap);
        }

        public void SetImage(SKBitmap? bitmap)
        {
            if (bitmap is null)
            {
                _imageWidth = 0;
                _imageHeight = 0;
                SetImageSource(null);
                ShowCheckerboard();
                UpdateScrollBars();
                return;
            }

            _imageWidth = bitmap.Width;
            _imageHeight = bitmap.Height;
            HideCheckerboard();

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            using var stream = new MemoryStream(data.ToArray());

            SetImageSource(new Bitmap(stream));
            UpdateTransform();
            UpdateScrollBars();
        }

        private void SetImageSource(Bitmap? source)
        {
            _displayBitmap?.Dispose();
            _displayBitmap = source;
            ImageControl.Source = _displayBitmap;
        }

        private void ShowCheckerboard()
        {
            if (CheckerboardCanvas is null) return;
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
            if (ImageBorder is not null) ImageBorder.IsVisible = false;
        }

        private void HideCheckerboard()
        {
            if (CheckerboardCanvas is not null) CheckerboardCanvas.Opacity = 0;
            if (ImageBorder is not null) ImageBorder.IsVisible = HasImage;
        }

        public void FitToWindow()
        {
            if (!HasImage) return;
            var canvasSize = new SKSizeI((int)Bounds.Width, (int)Bounds.Height);
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0) return;
            var imageSize = new SKSizeI(_imageWidth, _imageHeight);
            _state.FitToWindow(canvasSize, imageSize);
            UpdateTransform();
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        public void ResetZoom()
        {
            _state.ResetZoom();
            UpdateTransform();
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        private SKPoint ScreenToImage(Point screen)
        {
            float px = ((float)screen.X - _state.PanOffset.X) / _state.Zoom;
            float py = ((float)screen.Y - _state.PanOffset.Y) / _state.Zoom;
            return new SKPoint(px, py);
        }

        private Point ImageToScreen(SKPoint img)
        {
            float sx = img.X * _state.Zoom + _state.PanOffset.X;
            float sy = img.Y * _state.Zoom + _state.PanOffset.Y;
            return new Point(sx, sy);
        }

        private SKBitmap? GetActiveBitmap()
        {
            if (_layerService is null) return null;
            var active = _layerService.ActiveLayer;
            return active?.Bitmap;
        }

        private static SKBitmap CopyBitmap(SKBitmap source)
        {
            var copy = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
            using (var canvas = new SKCanvas(copy))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(source, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }
            return copy;
        }

        public void PasteBitmap(SKBitmap bitmap)
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            using var canvas = new SKCanvas(active.Bitmap);
            canvas.DrawBitmap(bitmap, new SKPoint(0, 0),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            ImageModified?.Invoke();
        }

        public void FlipHorizontalImage()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;
            var result = _filterService?.FlipHorizontal(active.Bitmap);
            if (result is null) return;
            active.SetBitmap(result);
            ImageModified?.Invoke();
        }

        public void FlipVerticalImage()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;
            var result = _filterService?.FlipVertical(active.Bitmap);
            if (result is null) return;
            active.SetBitmap(result);
            ImageModified?.Invoke();
        }

        public void RotateRight()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;
            var result = _filterService?.Rotate90CW(active.Bitmap);
            if (result is null) return;
            active.SetBitmap(result);
            ImageModified?.Invoke();
        }

        public void RotateLeft()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;
            var result = _filterService?.Rotate90CCW(active.Bitmap);
            if (result is null) return;
            active.SetBitmap(result);
            ImageModified?.Invoke();
        }

        private void ClearSelection()
        {
            _selectionRect = null;
            _selectionPath?.Dispose();
            _selectionPath = null;
            _selectionPoints.Clear();
            _marchingAntsTimer?.Stop();
            _marchingAntsOffset = 0;

            MarchingWhite.IsVisible = false;
            MarchingBlack.IsVisible = false;
            MarchingLassoWhite.IsVisible = false;
            MarchingLassoBlack.IsVisible = false;
        }

        private void StartMarchingAnts()
        {
            if (_selectionRect is null && _selectionPath is null) return;
            _marchingAntsTimer?.Start();
            UpdateMarchingAntsVisual();
        }

        private void UpdateSelectionOverlay()
        {
            if (_selectionRect is null)
            {
                MarchingWhite.IsVisible = false;
                MarchingBlack.IsVisible = false;
                return;
            }

            var r = _selectionRect.Value;
            var topLeft = ImageToScreen(new SKPoint(r.Left, r.Top));
            var bottomRight = ImageToScreen(new SKPoint(r.Right, r.Bottom));
            double x = topLeft.X;
            double y = topLeft.Y;
            double w = bottomRight.X - topLeft.X;
            double h = bottomRight.Y - topLeft.Y;

            if (w < 1) w = 1;
            if (h < 1) h = 1;

            Canvas.SetLeft(MarchingWhite, x);
            Canvas.SetTop(MarchingWhite, y);
            MarchingWhite.Width = w;
            MarchingWhite.Height = h;
            MarchingWhite.IsVisible = true;

            Canvas.SetLeft(MarchingBlack, x);
            Canvas.SetTop(MarchingBlack, y);
            MarchingBlack.Width = w;
            MarchingBlack.Height = h;
            MarchingBlack.IsVisible = true;

            MarchingLassoWhite.IsVisible = false;
            MarchingLassoBlack.IsVisible = false;
        }

        private void UpdateLassoOverlay()
        {
            if (_selectionPoints.Count < 2)
            {
                MarchingLassoWhite.IsVisible = false;
                MarchingLassoBlack.IsVisible = false;
                return;
            }

            var whiteGeom = new StreamGeometry();
            var blackGeom = new StreamGeometry();
            using (var ctx = whiteGeom.Open())
            {
                bool first = true;
                foreach (var pt in _selectionPoints)
                {
                    var sp = ImageToScreen(pt);
                    if (first) { ctx.BeginFigure(sp, true); first = false; }
                    else ctx.LineTo(sp);
                }
                ctx.EndFigure(true);
            }
            using (var ctx = blackGeom.Open())
            {
                bool first = true;
                foreach (var pt in _selectionPoints)
                {
                    var sp = ImageToScreen(pt);
                    if (first) { ctx.BeginFigure(sp, true); first = false; }
                    else ctx.LineTo(sp);
                }
                ctx.EndFigure(true);
            }

            MarchingLassoWhite.Data = whiteGeom;
            MarchingLassoWhite.IsVisible = true;
            MarchingLassoBlack.Data = blackGeom;
            MarchingLassoBlack.IsVisible = true;

            MarchingWhite.IsVisible = false;
            MarchingBlack.IsVisible = false;
        }

        private void UpdateMarchingAntsVisual()
        {
            if (_selectionRect.HasValue)
            {
                MarchingWhite.StrokeDashOffset = _marchingAntsOffset;
                MarchingBlack.StrokeDashOffset = _marchingAntsOffset + 4;
            }
            if (_selectionPoints.Count >= 2)
            {
                MarchingLassoWhite.StrokeDashOffset = _marchingAntsOffset;
                MarchingLassoBlack.StrokeDashOffset = _marchingAntsOffset + 4;
            }
        }

        private void UpdateCropOverlay()
        {
            if (!_cropRect.HasValue || !HasImage)
            {
                CropOverlay.IsVisible = false;
                return;
            }

            var r = _cropRect.Value;
            var tl = ImageToScreen(new SKPoint(r.Left, r.Top));
            var br = ImageToScreen(new SKPoint(r.Right, r.Bottom));
            double x = tl.X, y = tl.Y;
            double w = br.X - tl.X, h = br.Y - tl.Y;
            if (w < 1) w = 1;
            if (h < 1) h = 1;

            double canvasW = Bounds.Width;
            double canvasH = Bounds.Height;

            CropDimTop.Width = canvasW; CropDimTop.Height = Math.Max(0, y);
            Canvas.SetLeft(CropDimTop, 0); Canvas.SetTop(CropDimTop, 0);

            CropDimBottom.Width = canvasW; CropDimBottom.Height = Math.Max(0, canvasH - y - h);
            Canvas.SetLeft(CropDimBottom, 0); Canvas.SetTop(CropDimBottom, y + h);

            CropDimLeft.Width = Math.Max(0, x); CropDimLeft.Height = h;
            Canvas.SetLeft(CropDimLeft, 0); Canvas.SetTop(CropDimLeft, y);

            CropDimRight.Width = Math.Max(0, canvasW - x - w); CropDimRight.Height = h;
            Canvas.SetLeft(CropDimRight, x + w); Canvas.SetTop(CropDimRight, y);

            Canvas.SetLeft(CropBorder, x);
            Canvas.SetTop(CropBorder, y);
            CropBorder.Width = w;
            CropBorder.Height = h;

            CropOverlay.IsVisible = true;
        }

        #region Pointer events

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (!HasImage) return;
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
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
            if (_selectionRect.HasValue) UpdateSelectionOverlay();
            if (_selectionPoints.Count >= 2) UpdateLassoOverlay();
            if (_cropRect.HasValue) UpdateCropOverlay();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed ||
                e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(this);
                e.Pointer.Capture(this);
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var pos = e.GetPosition(this);
            var imgPt = ScreenToImage(pos);

            if (!HasImage && CurrentTool != "Hand" && CurrentTool != "Zoom")
                return;

            switch (CurrentTool)
            {
                case "Hand":
                    _isPanning = true;
                    _lastPanPosition = pos;
                    e.Pointer.Capture(this);
                    break;

                case "Zoom":
                    HandleZoomClick(pos, e.KeyModifiers.HasFlag(KeyModifiers.Alt));
                    break;

                case "Marquee":
                    _isDragging = true;
                    _startScreenPoint = pos;
                    e.Pointer.Capture(this);
                    ClearSelection();
                    break;

                case "Lasso":
                    _isDragging = true;
                    _startScreenPoint = pos;
                    e.Pointer.Capture(this);
                    ClearSelection();
                    _selectionPoints.Clear();
                    _selectionPoints.Add(imgPt);
                    _selectionPath = new SKPath();
                    _selectionPath.MoveTo(imgPt);
                    break;

                case "Brush":
                case "Eraser":
                    if (GetActiveBitmap() is { } bmpBrush)
                    {
                        _isPainting = true;
                        _workingBitmap = CopyBitmap(bmpBrush);
                        _lastPaintPoint = imgPt;
                        PaintStroke(imgPt, imgPt);
                        PreviewWorkingBitmap();
                        e.Pointer.Capture(this);
                    }
                    break;

                case "Fill":
                    if (GetActiveBitmap() is { } bmpFill)
                    {
                        BeforeImageModified?.Invoke();
                        int ix = Math.Clamp((int)imgPt.X, 0, bmpFill.Width - 1);
                        int iy = Math.Clamp((int)imgPt.Y, 0, bmpFill.Height - 1);
                        FloodFill(bmpFill, ix, iy);
                        SetImage(GetActiveBitmap());
                        ImageModified?.Invoke();
                    }
                    break;

                case "Eyedropper":
                    if (GetActiveBitmap() is { } bmpEye)
                    {
                        int ix = Math.Clamp((int)imgPt.X, 0, bmpEye.Width - 1);
                        int iy = Math.Clamp((int)imgPt.Y, 0, bmpEye.Height - 1);
                        var color = bmpEye.GetPixel(ix, iy);
                        BrushColor = color;
                        ColorPicked?.Invoke(this, color);
                    }
                    break;

                case "Move":
                    _isDragging = true;
                    _startScreenPoint = pos;
                    _moveOffsetX = 0;
                    _moveOffsetY = 0;
                    e.Pointer.Capture(this);
                    break;

                case "Crop":
                    if (_cropActive)
                    {
                        ApplyCrop();
                    }
                    else
                    {
                        _isDragging = true;
                        _startScreenPoint = pos;
                        e.Pointer.Capture(this);
                        _cropRect = null;
                        CropOverlay.IsVisible = false;
                    }
                    break;

                case "Text":
                    HandleTextClick(imgPt);
                    break;

                case "SmartSelect":
                    _isPanning = false;
                    if (imgPt.X >= 0 && imgPt.X < _imageWidth && imgPt.Y >= 0 && imgPt.Y < _imageHeight)
                        ClickOnImage?.Invoke(imgPt.X, imgPt.Y);
                    break;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var position = e.GetPosition(this);
            float pixelX = ((float)position.X - _state.PanOffset.X) / _state.Zoom;
            float pixelY = ((float)position.Y - _state.PanOffset.Y) / _state.Zoom;
            CursorPositionChanged?.Invoke(pixelX, pixelY);

            if (_isPanning)
            {
                var dx = (float)(position.X - _lastPanPosition.X);
                var dy = (float)(position.Y - _lastPanPosition.Y);
                _state.MovePan(dx, dy);
                _lastPanPosition = position;
                UpdateTransform();
                UpdateScrollBars();
                if (_selectionRect.HasValue) UpdateSelectionOverlay();
                if (_selectionPoints.Count >= 2) UpdateLassoOverlay();
                if (_cropRect.HasValue) UpdateCropOverlay();
                return;
            }

            if (!_isDragging && !_isPainting)
                return;

            var imgPt = ScreenToImage(position);

            switch (CurrentTool)
            {
                case "Marquee":
                    float mx1 = (float)Math.Min(_startScreenPoint.X, position.X);
                    float my1 = (float)Math.Min(_startScreenPoint.Y, position.Y);
                    float mx2 = (float)Math.Max(_startScreenPoint.X, position.X);
                    float my2 = (float)Math.Max(_startScreenPoint.Y, position.Y);
                    _selectionRect = new SKRect(
                        (mx1 - _state.PanOffset.X) / _state.Zoom,
                        (my1 - _state.PanOffset.Y) / _state.Zoom,
                        (mx2 - _state.PanOffset.X) / _state.Zoom,
                        (my2 - _state.PanOffset.Y) / _state.Zoom);
                    UpdateSelectionOverlay();
                    break;

                case "Lasso":
                    _selectionPoints.Add(imgPt);
                    _selectionPath?.LineTo(imgPt);
                    UpdateLassoOverlay();
                    break;

                case "Brush":
                case "Eraser":
                    if (_isPainting && _workingBitmap is not null)
                    {
                        PaintStroke(_lastPaintPoint, imgPt);
                        _lastPaintPoint = imgPt;
                        PreviewWorkingBitmap();
                    }
                    break;

                case "Move":
                    {
                        float dx = (float)(position.X - _startScreenPoint.X) / _state.Zoom;
                        float dy = (float)(position.Y - _startScreenPoint.Y) / _state.Zoom;
                        _moveOffsetX = dx;
                        _moveOffsetY = dy;
                        PreviewMove();
                    }
                    break;

                case "Crop":
                    if (_isDragging)
                    {
                        var tl = ScreenToImage(_startScreenPoint);
                        _cropRect = new SKRect(
                            Math.Min(tl.X, imgPt.X),
                            Math.Min(tl.Y, imgPt.Y),
                            Math.Max(tl.X, imgPt.X),
                            Math.Max(tl.Y, imgPt.Y));
                        UpdateCropOverlay();
                    }
                    break;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
                return;
            }

            var pos = e.GetPosition(this);
            var imgPt = ScreenToImage(pos);

            switch (CurrentTool)
            {
                case "Marquee":
                    _isDragging = false;
                    e.Pointer.Capture(null);
                    if (_selectionRect.HasValue)
                    {
                        var r = _selectionRect.Value;
                        if (Math.Abs(r.Width) > 2 && Math.Abs(r.Height) > 2)
                        {
                            _selectionRect = new SKRect(
                                Math.Max(0, r.Left),
                                Math.Max(0, r.Top),
                                Math.Min(_imageWidth, r.Right),
                                Math.Min(_imageHeight, r.Bottom));
                            UpdateSelectionOverlay();
                            StartMarchingAnts();
                        }
                        else
                        {
                            ClearSelection();
                        }
                    }
                    break;

                case "Lasso":
                    _isDragging = false;
                    e.Pointer.Capture(null);
                    if (_selectionPoints.Count >= 3)
                    {
                        _selectionPath?.Close();
                        float minX = _selectionPoints[0].X, minY = _selectionPoints[0].Y;
                        float maxX = minX, maxY = minY;
                        foreach (var pt in _selectionPoints)
                        {
                            if (pt.X < minX) minX = pt.X;
                            if (pt.Y < minY) minY = pt.Y;
                            if (pt.X > maxX) maxX = pt.X;
                            if (pt.Y > maxY) maxY = pt.Y;
                        }
                        _selectionRect = new SKRect(minX, minY, maxX, maxY);
                        StartMarchingAnts();
                    }
                    else
                    {
                        ClearSelection();
                    }
                    break;

                case "Brush":
                case "Eraser":
                    _isPainting = false;
                    e.Pointer.Capture(null);
                    if (_workingBitmap is not null)
                    {
                        var active = _layerService?.ActiveLayer;
                        if (active?.Bitmap is not null)
                        {
                            BeforeImageModified?.Invoke();
                            active.SetBitmap(_workingBitmap);
                            _workingBitmap = null;
                            ImageModified?.Invoke();
                        }
                        else
                        {
                            _workingBitmap?.Dispose();
                            _workingBitmap = null;
                        }
                    }
                    break;

                case "Move":
                    _isDragging = false;
                    e.Pointer.Capture(null);
                    if (Math.Abs(_moveOffsetX) > 0.5f || Math.Abs(_moveOffsetY) > 0.5f)
                    {
                        BeforeImageModified?.Invoke();
                        ApplyMove();
                    }
                    else
                    {
                        SetImage(GetActiveBitmap());
                    }
                    break;

                case "Crop":
                    _isDragging = false;
                    e.Pointer.Capture(null);
                    if (_cropRect.HasValue)
                    {
                        _cropActive = true;
                    }
                    break;
            }
        }

        #endregion

        #region Tool implementations

        private void HandleZoomClick(Point pos, bool zoomOut)
        {
            var oldZoom = _state.Zoom;
            if (zoomOut)
                _state.SetZoom(oldZoom / 1.25f);
            else
                _state.SetZoom(oldZoom * 1.25f);

            var mouseX = (float)pos.X;
            var mouseY = (float)pos.Y;
            var panX = mouseX - (mouseX - _state.PanOffset.X) * (_state.Zoom / oldZoom);
            var panY = mouseY - (mouseY - _state.PanOffset.Y) * (_state.Zoom / oldZoom);
            _state.SetPanOffset(new SKPoint(panX, panY));

            UpdateTransform();
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        private void PaintStroke(SKPoint from, SKPoint to)
        {
            if (_workingBitmap is null) return;

            using var canvas = new SKCanvas(_workingBitmap);
            using var paint = new SKPaint
            {
                StrokeWidth = BrushSize,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            if (CurrentTool == "Eraser")
            {
                paint.BlendMode = SKBlendMode.Clear;
                paint.Color = SKColors.Transparent;
            }
            else
            {
                paint.Color = BrushColor.WithAlpha((byte)(BrushOpacity * 255));
            }

            canvas.DrawLine(from.X, from.Y, to.X, to.Y, paint);
        }

        private void PreviewWorkingBitmap()
        {
            if (_workingBitmap is null) return;
            using var image = SKImage.FromBitmap(_workingBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 70);
            using var stream = new MemoryStream(data.ToArray());
            SetImageSource(new Bitmap(stream));
        }

        private void PreviewMove()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            var bmp = active.Bitmap;
            int dx = (int)Math.Round(_moveOffsetX);
            int dy = (int)Math.Round(_moveOffsetY);

            int newLeft = Math.Min(0, dx);
            int newTop = Math.Min(0, dy);
            int newRight = Math.Max(bmp.Width, bmp.Width + dx);
            int newBottom = Math.Max(bmp.Height, bmp.Height + dy);
            int newW = newRight - newLeft;
            int newH = newBottom - newTop;

            var result = new SKBitmap(newW, newH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bmp, dx - newLeft, dy - newTop,
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }
            var oldDisplay = _displayBitmap;
            SetImage(result);
            oldDisplay?.Dispose();
        }

        private void ApplyMove()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            var bmp = active.Bitmap;
            int dx = (int)Math.Round(_moveOffsetX);
            int dy = (int)Math.Round(_moveOffsetY);

            int newLeft = Math.Min(0, dx);
            int newTop = Math.Min(0, dy);
            int newRight = Math.Max(bmp.Width, bmp.Width + dx);
            int newBottom = Math.Max(bmp.Height, bmp.Height + dy);
            int newW = newRight - newLeft;
            int newH = newBottom - newTop;

            var result = new SKBitmap(newW, newH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(result))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(bmp, dx - newLeft, dy - newTop,
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }
            active.SetBitmap(result);
            _imageWidth = newW;
            _imageHeight = newH;
            _moveOffsetX = 0;
            _moveOffsetY = 0;
            ImageModified?.Invoke();
        }

        private void FloodFill(SKBitmap bitmap, int startX, int startY)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

            var targetColor = bitmap.GetPixel(startX, startY);
            var fillColor = BrushColor.WithAlpha((byte)(BrushOpacity * 255));

            if (targetColor == fillColor) return;

            var result = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var srcCanvas = new SKCanvas(result))
            {
                srcCanvas.Clear(SKColors.Transparent);
                srcCanvas.DrawBitmap(bitmap, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }

            var pixels = result.GetPixels(out _);
            int bytesPerPixel = 4;
            int stride = w * bytesPerPixel;

            var buffer = new byte[stride * h];
            System.Runtime.InteropServices.Marshal.Copy(pixels, buffer, 0, buffer.Length);

            bool Match(int x, int y)
            {
                int idx = (y * w + x) * bytesPerPixel;
                return buffer[idx] == targetColor.Blue &&
                       buffer[idx + 1] == targetColor.Green &&
                       buffer[idx + 2] == targetColor.Red &&
                       buffer[idx + 3] == targetColor.Alpha;
            }

            void SetPixel(int x, int y)
            {
                int idx = (y * w + x) * bytesPerPixel;
                buffer[idx] = fillColor.Blue;
                buffer[idx + 1] = fillColor.Green;
                buffer[idx + 2] = fillColor.Red;
                buffer[idx + 3] = fillColor.Alpha;
            }

            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));

            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (cx < 0 || cx >= w || cy < 0 || cy >= h) continue;
                if (!Match(cx, cy)) continue;

                int lx = cx;
                while (lx > 0 && Match(lx - 1, cy)) lx--;
                int rx = cx;
                while (rx < w - 1 && Match(rx + 1, cy)) rx++;

                for (int x = lx; x <= rx; x++)
                {
                    SetPixel(x, cy);
                    if (cy > 0 && Match(x, cy - 1)) stack.Push((x, cy - 1));
                    if (cy < h - 1 && Match(x, cy + 1)) stack.Push((x, cy + 1));
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, pixels, buffer.Length);
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is not null)
                active.SetBitmap(result);
        }

        private void HandleTextClick(SKPoint imagePoint)
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            var parentWindow = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
            if (parentWindow is null) return;

            var dialog = new TextEntryDialog();

            _ = dialog.ShowDialog(parentWindow);

            if (dialog.DialogResult != true) return;
            if (string.IsNullOrWhiteSpace(dialog.TextValue)) return;

            using var canvas = new SKCanvas(active.Bitmap);
            using var font = new SKFont(
                SKTypeface.FromFamilyName(
                    dialog.FontName ?? "Arial",
                    SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Upright),
                dialog.FontSize);
            using var paint = new SKPaint
            {
                Color = BrushColor.WithAlpha((byte)(BrushOpacity * 255)),
                IsAntialias = true
            };

            canvas.DrawText(dialog.TextValue, imagePoint.X, imagePoint.Y, font, paint);
            ImageModified?.Invoke();
        }

        private void ApplyCrop()
        {
            if (!_cropRect.HasValue) return;
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            BeforeImageModified?.Invoke();

            var r = _cropRect.Value;
            int x = Math.Max(0, (int)r.Left);
            int y = Math.Max(0, (int)r.Top);
            int w = Math.Min(active.Bitmap.Width - x, (int)r.Width);
            int h = Math.Min(active.Bitmap.Height - y, (int)r.Height);
            if (w <= 0 || h <= 0) return;

            var cropped = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            var subset = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            active.Bitmap.ExtractSubset(subset, new SKRectI(x, y, x + w, y + h));
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(subset, new SKPoint(0, 0));
            }
            subset.Dispose();

            active.SetBitmap(cropped);
            _cropRect = null;
            _cropActive = false;
            CropOverlay.IsVisible = false;
            ImageModified?.Invoke();
        }

        #endregion

        #region Transform and scroll

        private void UpdateTransform()
        {
            _scaleTransform.ScaleX = _state.Zoom;
            _scaleTransform.ScaleY = _state.Zoom;
            _translateTransform.X = _state.PanOffset.X;
            _translateTransform.Y = _state.PanOffset.Y;
        }

        private void OnHScrollBarValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressScrollBarUpdate) return;
            _state.SetPanOffset(new SKPoint(-(float)e.NewValue, _state.PanOffset.Y));
            UpdateTransform();
        }

        private void OnVScrollBarValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressScrollBarUpdate) return;
            _state.SetPanOffset(new SKPoint(_state.PanOffset.X, -(float)e.NewValue));
            UpdateTransform();
        }

        private void UpdateScrollBars()
        {
            if (HScrollBar is null || VScrollBar is null) return;
            if (!HasImage)
            {
                HScrollBar.IsVisible = false;
                VScrollBar.IsVisible = false;
                if (ScrollBarCorner is not null) ScrollBarCorner.IsVisible = false;
                return;
            }
            var viewportW = Bounds.Width;
            var viewportH = Bounds.Height;
            if (viewportW <= 0 || viewportH <= 0) return;

            var imageW = _imageWidth * _state.Zoom;
            var imageH = _imageHeight * _state.Zoom;
            bool needH = imageW > viewportW;
            bool needV = imageH > viewportH;
            HScrollBar.IsVisible = needH;
            VScrollBar.IsVisible = needV;
            if (ScrollBarCorner is not null) ScrollBarCorner.IsVisible = needH && needV;

            _suppressScrollBarUpdate = true;
            if (needH)
            {
                HScrollBar.Maximum = Math.Max(0, imageW - viewportW);
                HScrollBar.LargeChange = Math.Min(viewportW, HScrollBar.Maximum);
                HScrollBar.SmallChange = 20;
                HScrollBar.Value = Math.Clamp(-_state.PanOffset.X, 0, HScrollBar.Maximum);
            }
            if (needV)
            {
                VScrollBar.Maximum = Math.Max(0, imageH - viewportH);
                VScrollBar.LargeChange = Math.Min(viewportH, VScrollBar.Maximum);
                VScrollBar.SmallChange = 20;
                VScrollBar.Value = Math.Clamp(-_state.PanOffset.Y, 0, VScrollBar.Maximum);
            }
            _suppressScrollBarUpdate = false;
        }

        #endregion

        public void SetToolCursor()
        {
            Cursor = CurrentTool switch
            {
                "Hand" => new Cursor(StandardCursorType.Hand),
                "Zoom" => new Cursor(StandardCursorType.Help),
                "Eyedropper" => new Cursor(StandardCursorType.Cross),
                "Brush" or "Eraser" => new Cursor(StandardCursorType.Cross),
                "Fill" => new Cursor(StandardCursorType.Cross),
                "Move" => new Cursor(StandardCursorType.SizeAll),
                "Crop" => new Cursor(StandardCursorType.Cross),
                "Text" => new Cursor(StandardCursorType.Ibeam),
                _ => new Cursor(StandardCursorType.Arrow)
            };
        }
    }
}

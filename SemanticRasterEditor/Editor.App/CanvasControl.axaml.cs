using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
        private int _imageWidth;
        private int _imageHeight;
        private Bitmap? _displayBitmap;
        private Point _lastPanPosition;
        private bool _isPanning;
        private bool _suppressScrollBarUpdate;
        private bool _needsFitToWindow;

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
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return null;

            if (!_selectionRect.HasValue && _selectionPoints.Count < 3) return null;
            if (!_selectionRect.HasValue) return null;

            var r = _selectionRect.Value;

            if (!_selectionInverted)
            {
                int x = Math.Max(0, (int)r.Left);
                int y = Math.Max(0, (int)r.Top);
                int w = Math.Min(active.Bitmap.Width - x, (int)r.Width);
                int h = Math.Min(active.Bitmap.Height - y, (int)r.Height);
                if (w <= 0 || h <= 0) return null;

                var result = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
                active.Bitmap.ExtractSubset(result, new SKRectI(x, y, x + w, y + h));
                return result;
            }
            else
            {
                var clampedR = new SKRect(
                    Math.Max(0, r.Left),
                    Math.Max(0, r.Top),
                    Math.Min(_imageWidth, r.Right),
                    Math.Min(_imageHeight, r.Bottom));
                if (clampedR.Width <= 0 || clampedR.Height <= 0) return null;

                var result = CopyBitmap(active.Bitmap);
                using var canvas = new SKCanvas(result);
                using var clearPaint = new SKPaint { BlendMode = SKBlendMode.Clear };
                canvas.DrawRect(clampedR, clearPaint);
                return result;
            }
        }

        public void DeleteSelection()
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            if (!_selectionRect.HasValue && _selectionPoints.Count < 3) return;

            using var canvas = new SKCanvas(active.Bitmap);
            using var paint = new SKPaint { BlendMode = SKBlendMode.Clear };

            if (_selectionInverted && _selectionRect.HasValue)
            {
                var r = _selectionRect.Value;
                if (r.Top > 0) canvas.DrawRect(0, 0, _imageWidth, r.Top, paint);
                if (r.Bottom < _imageHeight) canvas.DrawRect(0, r.Bottom, _imageWidth, _imageHeight - r.Bottom, paint);
                if (r.Left > 0) canvas.DrawRect(0, r.Top, r.Left, r.Height, paint);
                if (r.Right < _imageWidth) canvas.DrawRect(r.Right, r.Top, _imageWidth - r.Right, r.Height, paint);
            }
            else if (_selectionRect.HasValue)
            {
                canvas.DrawRect(_selectionRect.Value, paint);
            }
            else if (_selectionPath != null)
            {
                canvas.DrawPath(_selectionPath, paint);
            }

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
            if (!HasImage) return;

            if (!_selectionRect.HasValue && _selectionPoints.Count < 3)
            {
                SelectAll();
                return;
            }

            _selectionInverted = !_selectionInverted;

            if (_selectionInverted && !_selectionRect.HasValue)
            {
                _selectionRect = new SKRect(0, 0, _imageWidth, _imageHeight);
                UpdateSelectionOverlay();
            }

            if (!_selectionInverted && _selectionPath != null && _selectionRect?.Width == _imageWidth && _selectionRect?.Height == _imageHeight)
            {
                _selectionRect = null;
                UpdateSelectionOverlay();
            }

            StartMarchingAnts();
        }

        public event Action<float>? ZoomChanged;
        public event Action<float, float>? CursorPositionChanged;
        public event Action? BeforeImageModified;
        public event Action? ImageModified;
        public event Action? LayerStateChanged;
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

        private bool _selectionInverted;

        public CanvasControl()
        {
            InitializeComponent();

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
            if (CtxCopyToLayer is not null) CtxCopyToLayer.Click += (_, _) => CopySelectionToNewLayer();
            if (CtxCutToLayer is not null) CtxCutToLayer.Click += (_, _) => CutSelectionToNewLayer();
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
            if (CtxCopyToLayer is not null) CtxCopyToLayer.IsEnabled = HasSelection;
            if (CtxCutToLayer is not null) CtxCutToLayer.IsEnabled = HasSelection;
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

            var sw = System.Diagnostics.Stopwatch.StartNew();

            _imageWidth = bitmap.Width;
            _imageHeight = bitmap.Height;
            HideCheckerboard();

            var avaloniaBitmap = SkiaToAvaloniaBitmap(bitmap);
            var t1 = sw.ElapsedMilliseconds;

            SetImageSource(avaloniaBitmap);
            UpdateScrollBars();

            sw.Stop();
            System.Diagnostics.Debug.WriteLine(
                $"[SetImage] total={sw.ElapsedMilliseconds}ms convert={t1}ms " +
                $"src={bitmap.Width}x{bitmap.Height}");
        }

        internal static Bitmap SkiaToAvaloniaBitmap(SKBitmap bitmap)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var wb = new WriteableBitmap(
                new PixelSize(bitmap.Width, bitmap.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            var srcRowBytes = bitmap.RowBytes;
            var expectedRowBytes = bitmap.Width * 4;

            using (var fb = wb.Lock())
            {
                var srcPtr = bitmap.GetPixels();
                if (srcPtr == IntPtr.Zero)
                    goto done;

                var srcBytes = bitmap.Info.BytesSize;

                if (srcRowBytes == expectedRowBytes && fb.RowBytes == expectedRowBytes)
                {
                    unsafe
                    {
                        new ReadOnlySpan<byte>(srcPtr.ToPointer(), srcBytes)
                            .CopyTo(new Span<byte>(fb.Address.ToPointer(), srcBytes));
                    }
                }
                else
                {
                    var dstPtr = fb.Address;
                    var row = new byte[expectedRowBytes];

                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(srcPtr, y * srcRowBytes), row, 0, expectedRowBytes);
                        Marshal.Copy(row, 0, IntPtr.Add(dstPtr, y * fb.RowBytes), expectedRowBytes);
                    }
                }
            }

        done:
            sw.Stop();
            System.Diagnostics.Debug.WriteLine(
                $"[SetImage] SkiaToAv={sw.ElapsedMilliseconds}ms {bitmap.Width}x{bitmap.Height} " +
                $"strideMatch={srcRowBytes == expectedRowBytes}");

            return wb;
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
            ImageControl.IsVisible = false;
        }

        private void HideCheckerboard()
        {
            if (CheckerboardCanvas is not null) CheckerboardCanvas.Opacity = 0;
            ImageControl.IsVisible = HasImage;
        }

        public void MarkFitToWindow()
        {
            _needsFitToWindow = true;
            System.Diagnostics.Debug.WriteLine(
                $"[MarkFitToWindow] _needsFitToWindow=true, HasImage={HasImage}, " +
                $"ImageSize={_imageWidth}x{_imageHeight}, Bounds={Bounds.Width:F0}x{Bounds.Height:F0}");
        }

        public void FitToWindow()
        {
            if (!HasImage) return;
            var canvasSize = new SKSizeI((int)Bounds.Width, (int)Bounds.Height);
            if (canvasSize.Width <= 0 || canvasSize.Height <= 0) return;
            var imageSize = new SKSizeI(_imageWidth, _imageHeight);
            _state.FitToWindow(canvasSize, imageSize);
            ApplyViewState();
            UpdateSelectionOverlay();
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
        }

        public void ResetZoom()
        {
            _state.ResetZoom();
            ApplyViewState();
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

        public void CopySelectionToNewLayer()
        {
            var selection = GetSelectionBitmap();
            if (selection is null) return;

            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            int w = active.Bitmap.Width;
            int h = active.Bitmap.Height;

            var newLayerBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(newLayerBitmap))
            {
                canvas.Clear(SKColors.Transparent);

                if (_selectionRect.HasValue)
                {
                    var r = _selectionRect.Value;
                    if (!_selectionInverted)
                    {
                        canvas.DrawBitmap(selection, new SKPoint(r.Left, r.Top),
                            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                    }
                    else
                    {
                        canvas.DrawBitmap(selection, new SKPoint(0, 0),
                            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                    }
                }
            }

            _layerService?.Add(newLayerBitmap, "Копия выделения");
            selection.Dispose();
            ClearSelection();
            LayerStateChanged?.Invoke();
            ImageModified?.Invoke();
        }

        public void CutSelectionToNewLayer()
        {
            var selection = GetSelectionBitmap();
            if (selection is null) return;

            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null)
            {
                selection.Dispose();
                return;
            }

            BeforeImageModified?.Invoke();

            int w = active.Bitmap.Width;
            int h = active.Bitmap.Height;
            var selRect = _selectionRect;

            using var srcCanvas = new SKCanvas(active.Bitmap);
            using var clearPaint = new SKPaint { BlendMode = SKBlendMode.Clear };

            if (_selectionInverted && selRect.HasValue)
            {
                var r = selRect.Value;
                if (r.Top > 0) srcCanvas.DrawRect(0, 0, _imageWidth, r.Top, clearPaint);
                if (r.Bottom < _imageHeight) srcCanvas.DrawRect(0, r.Bottom, _imageWidth, _imageHeight - r.Bottom, clearPaint);
                if (r.Left > 0) srcCanvas.DrawRect(0, r.Top, r.Left, r.Height, clearPaint);
                if (r.Right < _imageWidth) srcCanvas.DrawRect(r.Right, r.Top, _imageWidth - r.Right, r.Height, clearPaint);
            }
            else if (selRect.HasValue)
            {
                srcCanvas.DrawRect(selRect.Value, clearPaint);
            }

            var newLayerBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(newLayerBitmap))
            {
                canvas.Clear(SKColors.Transparent);

                if (selRect.HasValue)
                {
                    var r = selRect.Value;
                    if (!_selectionInverted)
                    {
                        canvas.DrawBitmap(selection, new SKPoint(r.Left, r.Top),
                            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                    }
                    else
                    {
                        canvas.DrawBitmap(selection, new SKPoint(0, 0),
                            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                    }
                }
            }

            _layerService?.Add(newLayerBitmap, "Вырезка");
            selection.Dispose();
            ClearSelection();
            LayerStateChanged?.Invoke();
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
            _selectionInverted = false;
            _marchingAntsTimer?.Stop();
            _marchingAntsOffset = 0;

            MarchingWhite.IsVisible = false;
            MarchingBlack.IsVisible = false;
            MarchingLassoWhite.IsVisible = false;
            MarchingLassoBlack.IsVisible = false;
        }

        private void MagicWand(SKPoint clickPoint)
        {
            var bmp = GetActiveBitmap();
            if (bmp is null) return;

            int ix = (int)clickPoint.X;
            int iy = (int)clickPoint.Y;
            int w = bmp.Width;
            int h = bmp.Height;

            if (ix < 0 || ix >= w || iy < 0 || iy >= h) return;

            const int tolerance = 32;

            SKColor targetColor;
            byte[] mask;
            int minX, maxX, minY, maxY;
            unsafe
            {
                var allPixels = (byte*)bmp.GetPixels().ToPointer();
                int stride = bmp.RowBytes;

                var px = allPixels + iy * stride + ix * 4;
                targetColor = new SKColor(px[2], px[1], px[0], px[3]);

                mask = new byte[w * h];
                var queue = new Queue<(int, int)>();
                queue.Enqueue((ix, iy));
                mask[iy * w + ix] = 1;

                minX = ix; maxX = ix; minY = iy; maxY = iy;

                Span<int> dirs = stackalloc int[] { -1, 0, 1, 0, -1 };
                while (queue.Count > 0)
                {
                    var (cx, cy) = queue.Dequeue();

                    if (cx < minX) minX = cx;
                    if (cx > maxX) maxX = cx;
                    if (cy < minY) minY = cy;
                    if (cy > maxY) maxY = cy;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + dirs[d];
                        int ny = cy + dirs[d + 1];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        if (mask[ny * w + nx] != 0) continue;

                        var npx = allPixels + ny * stride + nx * 4;
                        int dr = npx[2] - targetColor.Red;
                        int dg = npx[1] - targetColor.Green;
                        int db = npx[0] - targetColor.Blue;
                        if (dr * dr + dg * dg + db * db <= tolerance * tolerance)
                        {
                            mask[ny * w + nx] = 1;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }

            int selW = maxX - minX + 1;
            int selH = maxY - minY + 1;
            if (selW < 1 || selH < 1) return;

            _selectionRect = new SKRect(minX, minY, maxX + 1, maxY + 1);

            var path = new SKPath();
            for (int y = minY; y <= maxY; y++)
            {
                int spanStart = -1;
                for (int x = minX; x <= maxX; x++)
                {
                    if (mask[y * w + x] != 0)
                    {
                        if (spanStart < 0) spanStart = x;
                    }
                    else if (spanStart >= 0)
                    {
                        path.AddRect(new SKRect(spanStart, y, x, y + 1));
                        spanStart = -1;
                    }
                }
                if (spanStart >= 0)
                    path.AddRect(new SKRect(spanStart, y, maxX + 1, y + 1));
            }

            _selectionPath = path;
            UpdateSelectionOverlay();
            StartMarchingAnts();
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

            System.Diagnostics.Debug.WriteLine(
                $"[SelectionOverlay] imgRect=({r.Left:F0},{r.Top:F0})-({r.Right:F0},{r.Bottom:F0}) " +
                $"screenPos=({x:F1},{y:F1}) size={w:F1}x{h:F1} zoom={_state.Zoom:F4}");

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
            var position = e.GetPosition(CanvasPanel);
            var zoomFactor = e.Delta.Y > 0 ? 1.1f : 1f / 1.1f;
            var newZoom = _state.Zoom * zoomFactor;
            var mouseX = (float)position.X;
            var mouseY = (float)position.Y;
            var oldZoom = _state.Zoom;
            _state.SetZoom(newZoom);
            var panX = mouseX - (mouseX - _state.PanOffset.X) * (_state.Zoom / oldZoom);
            var panY = mouseY - (mouseY - _state.PanOffset.Y) * (_state.Zoom / oldZoom);
            _state.SetPanOffset(new SKPoint(panX, panY));
            ApplyViewState();
            UpdateScrollBars();
            ZoomChanged?.Invoke(_state.Zoom);
            if (_selectionRect.HasValue) UpdateSelectionOverlay();
            if (_selectionPoints.Count >= 2) UpdateLassoOverlay();
            if (_cropRect.HasValue) UpdateCropOverlay();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(CanvasPanel).Properties.IsMiddleButtonPressed ||
                e.GetCurrentPoint(CanvasPanel).Properties.IsRightButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(CanvasPanel);
                e.Pointer.Capture(this);
                return;
            }

            if (!e.GetCurrentPoint(CanvasPanel).Properties.IsLeftButtonPressed)
                return;

            var pos = e.GetPosition(CanvasPanel);
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
                    MagicWand(imgPt);
                    break;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var position = e.GetPosition(CanvasPanel);
            float pixelX = ((float)position.X - _state.PanOffset.X) / _state.Zoom;
            float pixelY = ((float)position.Y - _state.PanOffset.Y) / _state.Zoom;
            CursorPositionChanged?.Invoke(pixelX, pixelY);

            if (_isPanning)
            {
                var dx = (float)(position.X - _lastPanPosition.X);
                var dy = (float)(position.Y - _lastPanPosition.Y);
                _state.MovePan(dx, dy);
                _lastPanPosition = position;
                ApplyViewState();
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

            var pos = e.GetPosition(CanvasPanel);
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

            ApplyViewState();
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
            SetImageSource(SkiaToAvaloniaBitmap(_workingBitmap));
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
            _moveOffsetX = 0;
            _moveOffsetY = 0;
            ImageModified?.Invoke();
        }

        private async void HandleTextClick(SKPoint imagePoint)
        {
            var active = _layerService?.ActiveLayer;
            if (active?.Bitmap is null) return;

            var parentWindow = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
            if (parentWindow is null) return;

            var dialog = new TextEntryDialog();

            await dialog.ShowDialog(parentWindow);

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

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);

            System.Diagnostics.Debug.WriteLine(
                $"[ArrangeOverride] needsFit={_needsFitToWindow} HasImage={HasImage} " +
                $"finalSize={finalSize.Width:F0}x{finalSize.Height:F0} " +
                $"imageSize={_imageWidth}x{_imageHeight} zoom={_state.Zoom:F4} pan=({_state.PanOffset.X:F1},{_state.PanOffset.Y:F1})");

            if (_needsFitToWindow && HasImage && finalSize.Width > 0 && finalSize.Height > 0)
            {
                _needsFitToWindow = false;
                _state.FitToWindow(new SKSizeI((int)finalSize.Width, (int)finalSize.Height),
                    new SKSizeI(_imageWidth, _imageHeight));
                System.Diagnostics.Debug.WriteLine(
                    $"[ArrangeOverride] FitToWindow: zoom={_state.Zoom:F4} pan=({_state.PanOffset.X:F1},{_state.PanOffset.Y:F1})");
                ApplyViewState();
                UpdateSelectionOverlay();
                UpdateScrollBars();
                ZoomChanged?.Invoke(_state.Zoom);
            }
            return result;
        }

        #region Transform and scroll

        private void ApplyViewState()
        {
            Canvas.SetLeft(ImageControl, _state.PanOffset.X);
            Canvas.SetTop(ImageControl, _state.PanOffset.Y);
            ImageControl.Width = _imageWidth * _state.Zoom;
            ImageControl.Height = _imageHeight * _state.Zoom;

            System.Diagnostics.Debug.WriteLine(
                $"[ApplyView] Zoom={_state.Zoom:F4} Pan=({_state.PanOffset.X:F1},{_state.PanOffset.Y:F1}) " +
                $"Img={_imageWidth}x{_imageHeight} Size={ImageControl.Width:F0}x{ImageControl.Height:F0}");
        }

        private void OnHScrollBarValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressScrollBarUpdate) return;
            _state.SetPanOffset(new SKPoint(-(float)e.NewValue, _state.PanOffset.Y));
            ApplyViewState();
        }

        private void OnVScrollBarValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressScrollBarUpdate) return;
            _state.SetPanOffset(new SKPoint(_state.PanOffset.X, -(float)e.NewValue));
            ApplyViewState();
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

        public static SKBitmap ApplyChannelFilter(SKBitmap source, bool[] visibility)
        {
            var result = new SKBitmap(source.Width, source.Height,
                SKColorType.Bgra8888, SKAlphaType.Premul);

            var matrix = new float[20]
            {
                visibility[0] && visibility[1] ? 1 : 0, 0, 0, 0, 0,
                0, visibility[0] && visibility[2] ? 1 : 0, 0, 0, 0,
                0, 0, visibility[0] && visibility[3] ? 1 : 0, 0, 0,
                0, 0, 0, visibility[0] && visibility[4] ? 1 : 0, 0
            };

            if (!visibility[4])
                matrix[19] = 255;

            using var filter = SKColorFilter.CreateColorMatrix(matrix);
            using var paint = new SKPaint { ColorFilter = filter };
            using var canvas = new SKCanvas(result);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, new SKPoint(0, 0),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), paint);

            return result;
        }

        public void SetToolCursor()
        {
            Cursor = CurrentTool switch
            {
                "Hand" => new Cursor(StandardCursorType.Hand),
                "Zoom" => new Cursor(StandardCursorType.Help),
                "Eyedropper" => new Cursor(StandardCursorType.Cross),
                "Brush" or "Eraser" => new Cursor(StandardCursorType.Cross),
                "Move" => new Cursor(StandardCursorType.SizeAll),
                "Crop" => new Cursor(StandardCursorType.Cross),
                "Text" => new Cursor(StandardCursorType.Ibeam),
                _ => new Cursor(StandardCursorType.Arrow)
            };
        }
    }
}

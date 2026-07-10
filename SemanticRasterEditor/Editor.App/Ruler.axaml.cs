using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Editor.App
{
    public partial class Ruler : UserControl
    {
        private double _offset;
        private double _zoom = 1.0;
        private bool _isHorizontal = true;
        private double _cursorPos = -1;

        private static readonly ISolidColorBrush BgBrush = new SolidColorBrush(Color.Parse("#1B1E22"));
        private static readonly ISolidColorBrush TickBrush = new SolidColorBrush(Color.Parse("#666666"));
        private static readonly ISolidColorBrush MajorBrush = new SolidColorBrush(Color.Parse("#AAAAAA"));
        private static readonly ISolidColorBrush TextBrush = new SolidColorBrush(Color.Parse("#AAAAAA"));
        private static readonly ISolidColorBrush CursorBrush = new SolidColorBrush(Color.Parse("#D4A843"));
        private static readonly ISolidColorBrush RulerBorderBrush = new SolidColorBrush(Color.Parse("#4A4A4A"));
        private static readonly IPen TickPen = new Pen(TickBrush, 1);
        private static readonly IPen MajorPen = new Pen(MajorBrush, 1);
        private static readonly IPen CursorPen = new Pen(CursorBrush, 1);
        private static readonly IPen BorderPen = new Pen(RulerBorderBrush, 1);
        private static readonly Typeface RulerTypeface = new Typeface("Segoe UI");

        public bool IsHorizontal
        {
            get => _isHorizontal;
            set { _isHorizontal = value; InvalidateVisual(); }
        }

        public double Offset
        {
            get => _offset;
            set { _offset = value; InvalidateVisual(); }
        }

        public double Zoom
        {
            get => _zoom;
            set { _zoom = value; InvalidateVisual(); }
        }

        public double CursorPosition
        {
            get => _cursorPos;
            set { _cursorPos = value; InvalidateVisual(); }
        }

        public Ruler()
        {
            InitializeComponent();
        }

        public override void Render(DrawingContext context)
        {
            var w = Bounds.Width;
            var h = Bounds.Height;
            if (w <= 0 || h <= 0)
                return;

            context.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

            var step = CalculateTickStep();

            if (_isHorizontal)
                DrawHorizontal(context, w, h, step);
            else
                DrawVertical(context, w, h, step);

            if (_cursorPos >= 0)
                DrawCursor(context, w, h);
        }

        private void DrawHorizontal(DrawingContext ctx, double w, double h, double step)
        {
            ctx.DrawLine(BorderPen, new Point(0, h - 1), new Point(w, h - 1));

            double startPixel = -_offset / _zoom;
            double endPixel = (w - _offset) / _zoom;
            double startTick = Math.Floor(startPixel / step) * step;

            var typeface = RulerTypeface;

            for (double px = startTick; px <= endPixel; px += step)
            {
                double screenX = px * _zoom + _offset;
                if (screenX < 0 || screenX > w)
                    continue;

                bool isMajor = Math.Abs(px % (step * 5)) < 0.001;
                double tickH = isMajor ? h * 0.65 : h * 0.3;

                ctx.DrawLine(isMajor ? MajorPen : TickPen,
                    new Point(screenX, h - tickH),
                    new Point(screenX, h - 1));

                if (isMajor && px >= 0)
                {
                    var text = FormatRulerValue(px);
                    var ft = new FormattedText(text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface, 9, TextBrush);
                    ctx.DrawText(ft, new Point(screenX + 3, 2));
                }
            }
        }

        private void DrawVertical(DrawingContext ctx, double w, double h, double step)
        {
            ctx.DrawLine(BorderPen, new Point(w - 1, 0), new Point(w - 1, h));

            double startPixel = -_offset / _zoom;
            double endPixel = (h - _offset) / _zoom;
            double startTick = Math.Floor(startPixel / step) * step;

            var typeface = RulerTypeface;

            for (double py = startTick; py <= endPixel; py += step)
            {
                double screenY = py * _zoom + _offset;
                if (screenY < 0 || screenY > h)
                    continue;

                bool isMajor = Math.Abs(py % (step * 5)) < 0.001;
                double tickW = isMajor ? w * 0.65 : w * 0.3;

                ctx.DrawLine(isMajor ? MajorPen : TickPen,
                    new Point(w - tickW, screenY),
                    new Point(w - 1, screenY));

                if (isMajor && py >= 0)
                {
                    var text = FormatRulerValue(py);
                    var ft = new FormattedText(text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface, 9, TextBrush);
                    ctx.DrawText(ft, new Point(3, screenY + 2));
                }
            }
        }

        private void DrawCursor(DrawingContext ctx, double w, double h)
        {
            if (_isHorizontal)
            {
                double sx = _cursorPos * _zoom + _offset;
                if (sx >= 0 && sx <= w)
                    ctx.DrawLine(CursorPen, new Point(sx, 0), new Point(sx, h));
            }
            else
            {
                double sy = _cursorPos * _zoom + _offset;
                if (sy >= 0 && sy <= h)
                    ctx.DrawLine(CursorPen, new Point(0, sy), new Point(w, sy));
            }
        }

        private double CalculateTickStep()
        {
            double baseStep = 50;
            double minPixelsBetweenTicks = 40;
            double step = baseStep;

            if (_zoom <= 0)
                return baseStep;

            while (step * _zoom < minPixelsBetweenTicks)
                step *= 2;

            while (step * _zoom > minPixelsBetweenTicks * 4)
                step /= 2;

            return Math.Max(1, step);
        }

        private static string FormatRulerValue(double pixels)
        {
            if (pixels >= 1000)
                return $"{pixels / 1000:F1}k";
            return ((int)pixels).ToString();
        }
    }
}

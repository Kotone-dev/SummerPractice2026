using SkiaSharp;

namespace Editor.Services
{
    public class ImageHistoryService : IDisposable
    {
        private readonly List<SKBitmap> _undoStack = new();
        private readonly List<SKBitmap> _redoStack = new();
        private int _maxHistory;
        private bool _disposed;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        public ImageHistoryService(int maxHistory = 30)
        {
            _maxHistory = maxHistory;
        }

        public void PushState(SKBitmap currentState)
        {
            if (currentState is null) return;

            var copy = new SKBitmap(currentState.Width, currentState.Height,
                currentState.ColorType, currentState.AlphaType);
            using (var canvas = new SKCanvas(copy))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(currentState, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }

            _undoStack.Add(copy);

            if (_undoStack.Count > _maxHistory)
            {
                var oldest = _undoStack[0];
                _undoStack.RemoveAt(0);
                oldest.Dispose();
            }

            ClearRedo();
        }

        public SKBitmap? Undo(SKBitmap? currentState)
        {
            if (_undoStack.Count == 0) return null;

            if (currentState is not null)
            {
                var redoCopy = CloneBitmap(currentState);
                _redoStack.Add(redoCopy);
                if (_redoStack.Count > _maxHistory)
                {
                    var oldest = _redoStack[0];
                    _redoStack.RemoveAt(0);
                    oldest.Dispose();
                }
            }

            var index = _undoStack.Count - 1;
            var state = _undoStack[index];
            _undoStack.RemoveAt(index);

            return CloneBitmap(state);
        }

        public SKBitmap? Redo(SKBitmap? currentState)
        {
            if (_redoStack.Count == 0) return null;

            if (currentState is not null)
            {
                var undoCopy = CloneBitmap(currentState);
                _undoStack.Add(undoCopy);
                if (_undoStack.Count > _maxHistory)
                {
                    var oldest = _undoStack[0];
                    _undoStack.RemoveAt(0);
                    oldest.Dispose();
                }
            }

            var index = _redoStack.Count - 1;
            var state = _redoStack[index];
            _redoStack.RemoveAt(index);

            return CloneBitmap(state);
        }

        public void Clear()
        {
            ClearRedo();
            foreach (var bmp in _undoStack)
                bmp.Dispose();
            _undoStack.Clear();
        }

        private void ClearRedo()
        {
            foreach (var bmp in _redoStack)
                bmp.Dispose();
            _redoStack.Clear();
        }

        private static SKBitmap CloneBitmap(SKBitmap source)
        {
            var copy = new SKBitmap(source.Width, source.Height,
                source.ColorType, source.AlphaType);
            using (var canvas = new SKCanvas(copy))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(source, new SKPoint(0, 0),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            }
            return copy;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Clear();
            _disposed = true;
        }
    }
}

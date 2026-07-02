using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Editor.Models;
using Editor.Services;

namespace Editor.App
{
    public partial class MainWindow : Window
    {
        private readonly FileService _fileService = new();
        private readonly ImageDocument _document = new();

        public MainWindow()
        {
            InitializeComponent();
            SetupMenu();
            EditorCanvas.ZoomChanged += OnZoomChanged;
            KeyDown += OnKeyDown;
        }

        private void SetupMenu()
        {
            var fileMenu = this.FindControl<MenuItem>("MenuFile")!;
            fileMenu.Items.Add(CreateMenuItem("Открыть...", OnOpenClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Сохранить", OnSaveClick));
            fileMenu.Items.Add(CreateMenuItem("Сохранить как...", OnSaveAsClick));
            fileMenu.Items.Add(new Separator());
            fileMenu.Items.Add(CreateMenuItem("Выход", OnExitClick));

            var viewMenu = this.FindControl<MenuItem>("MenuView")!;
            viewMenu.Items.Add(CreateMenuItem("Вписать в окно", OnFitToWindowClick));
            viewMenu.Items.Add(CreateMenuItem("Сбросить зум", OnResetZoomClick));
        }

        private static MenuItem CreateMenuItem(string header, EventHandler<RoutedEventArgs> handler)
        {
            var item = new MenuItem { Header = header };
            item.Click += handler;
            return item;
        }

        private async void OnOpenClick(object? sender, RoutedEventArgs e)
        {
            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage is null)
                return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть изображение",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Изображения")
                    {
                        Patterns = FileService.SupportedExtensions
                            .Select(ext => $"*{ext}")
                            .ToArray()
                    }
                }
            });

            if (files.Count == 0)
                return;

            var path = files[0].TryGetLocalPath();
            if (path is null)
                return;

            var bitmap = _fileService.OpenImage(path);
            if (bitmap is null)
                return;

            _document.SetBitmap(bitmap);
            _document.FilePath = path;

            Title = $"SemanticRasterEditor — {Path.GetFileName(path)}";
            EditorCanvas.SetImage(bitmap);
            UpdateStatusBar();
            EditorCanvas.FitToWindow();
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (_document.Bitmap is null)
                return;

            if (_document.FilePath is not null)
            {
                _fileService.SaveImage(_document.Bitmap, _document.FilePath);
                _document.MarkSaved(_document.FilePath);
                UpdateTitle();
                return;
            }

            await SaveAs();
        }

        private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
        {
            await SaveAs();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnFitToWindowClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.FitToWindow();
            UpdateStatusBar();
        }

        private void OnResetZoomClick(object? sender, RoutedEventArgs e)
        {
            EditorCanvas.ResetZoom();
            UpdateStatusBar();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            if (ctrl && shift && e.Key == Key.S)
            {
                OnSaveAsClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                OnSaveClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.O)
            {
                OnOpenClick(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private async Task SaveAs()
        {
            if (_document.Bitmap is null)
                return;

            var storage = GetTopLevel(this)?.StorageProvider;
            if (storage is null)
                return;

            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить изображение",
                SuggestedFileName = Path.GetFileName(_document.FilePath ?? "image.png"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG")
                    {
                        Patterns = new[] { "*.png" }
                    },
                    new FilePickerFileType("JPEG")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg" }
                    },
                    new FilePickerFileType("BMP")
                    {
                        Patterns = new[] { "*.bmp" }
                    }
                }
            });

            if (file is null)
                return;

            var path = file.TryGetLocalPath();
            if (path is null)
                return;

            _fileService.SaveImage(_document.Bitmap, path);
            _document.MarkSaved(path);
            UpdateTitle();
        }

        private void OnZoomChanged(float zoom)
        {
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            var zoomPercent = (int)(EditorCanvas.Zoom * 100);
            ZoomText.Text = $"{zoomPercent}%";

            if (_document.Bitmap is not null)
                SizeText.Text = $"{_document.Width} × {_document.Height}";
            else
                SizeText.Text = string.Empty;
        }

        private void UpdateTitle()
        {
            var fileName = _document.FilePath is not null
                ? Path.GetFileName(_document.FilePath)
                : "Без имени";

            var modified = _document.IsModified ? " *" : "";
            Title = $"SemanticRasterEditor — {fileName}{modified}";
        }
    }
}

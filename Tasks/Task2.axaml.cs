using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.VisualTree;

namespace SortingDemo.Tasks
{
    public partial class Task2 : UserControl, INotifyPropertyChanged
    {
        private List<string[]> _data = new();
        private readonly ObservableCollection<DisplayRow> _tableRows = new();
        private readonly ObservableCollection<MergeStep> _quickSortSteps = new();
        private readonly ObservableCollection<MergeStep> _mergeLeft = new();
        private readonly ObservableCollection<MergeStep> _mergeRight = new();
        private readonly ObservableCollection<MergeStep> _mergeResult = new();

        private bool _stopRequested = false;
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "ExtSort_" + Guid.NewGuid().ToString("N")[..8]);
        private readonly List<string> _tempFiles = new();
        private readonly List<string> _resultBuffer = new(11);

        // Пул объектов — для повторного использования
        private readonly Queue<MergeStep> _stepPool = new();

        public Task2()
        {
            InitializeComponent();
            DataContext = this;
            Directory.CreateDirectory(_tempDir);

            DataTable.ItemsSource = _tableRows;
            QuickSortAnimation.ItemsSource = _quickSortSteps;
            MergeLeft.ItemsSource = _mergeLeft;
            MergeRight.ItemsSource = _mergeRight;
            MergeResult.ItemsSource = _mergeResult;

            LoadFileBtn.Click += LoadFileBtn_Click;
            SaveFileBtn.Click += SaveFileBtn_Click;
            StartBtn.Click += StartBtn_Click;
            StopBtn.Click += (_, __) => _stopRequested = true;

            PreallocatePool(5000); // Хватит на 1M+ строк
        }

        private void PreallocatePool(int count)
        {
            for (int i = 0; i < count; i++)
                _stepPool.Enqueue(new MergeStep());
        }

        private MergeStep RentStep()
        {
            return _stepPool.Count > 0 ? _stepPool.Dequeue() : new MergeStep();
        }

        private void ReturnStep(MergeStep step)
        {
            step.Text = "";
            step.BgColor = "#ffffff";
            step.BorderColor = "Transparent";
            step.BorderThickness = 0;
            step.ShowArrow = false;
            step.IsTitle = false;
            _stepPool.Enqueue(step);
        }

        private void ReturnAllSteps(ObservableCollection<MergeStep> collection)
        {
            foreach (var step in collection.ToList())
            {
                ReturnStep(step);
                collection.Remove(step);
            }
        }

        #region === UI Events ===

        private async void StartBtn_Click(object? sender, RoutedEventArgs e)
        {
            await StartSort();
        }

        private async void LoadFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Выберите CSV", AllowMultiple = false };
            var result = await dlg.ShowAsync(this.FindAncestorOfType<Window>());
            if (result?.Length > 0) LoadCsv(result[0]);
        }

        private void SaveFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "sorted_result.csv");
                File.WriteAllLines(path, _tableRows.Skip(1).Select(r => r.Text.Replace(" | ", ";")), Encoding.UTF8);
                Log($"Сохранено: {path}");
            }
            catch (Exception ex) { Log($"Ошибка: {ex.Message}"); }
        }

        #endregion

        #region === Загрузка ===

        private void LoadCsv(string path)
        {
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();

                if (lines.Length < 2) { Log("Файл пустой"); return; }

                _data.Clear();
                char sep = lines[0].Contains(';') ? ';' : ',';
                var headers = lines[0].Split(sep);

                foreach (var line in lines.Skip(1))
                {
                    var values = line.Split(sep).Select(v => v.Trim()).ToArray();
                    _data.Add(values);
                }

                UpdateTable(_data, "Исходные данные");
                KeyBox.ItemsSource = headers;
                KeyBox.SelectedIndex = 0;
                MethodBox.SelectedIndex = 0;
                Log($"Загружено {_data.Count} записей");
            }
            catch (Exception ex) { Log($"Ошибка: {ex.Message}"); }
        }

        #endregion

        #region === Сортировка ===

        private async Task StartSort()
        {
            if (_data.Count == 0) { Log("Нет данных"); return; }
            _stopRequested = false;
            ClearAnimation();
            _resultBuffer.Clear();
            CleanupTempFiles();

            string method = ((ComboBoxItem)MethodBox.SelectedItem)?.Content?.ToString() ?? "";
            string keyName = KeyBox.SelectedItem?.ToString() ?? "";
            int keyIndex = KeyBox.ItemsSource.Cast<object>().ToList().IndexOf(KeyBox.SelectedItem);
            if (keyIndex < 0) { Log("Ключ не выбран"); return; }

            int delay = (int)(DelayBox.Value ?? 400);

            Log($"Запуск: {method} по '{keyName}'");

            try
            {
                if (method.Contains("прямое", StringComparison.OrdinalIgnoreCase))
                    await DirectMergeSort(keyIndex, delay);
                else if (method.Contains("естественное", StringComparison.OrdinalIgnoreCase))
                    await NaturalMergeSort(keyIndex, delay);
                else
                    await MultiwayMergeSort(keyIndex, delay);

                if (!_stopRequested) Log("Сортировка завершена");
            }
            catch (Exception ex)
            {
                Log($"Ошибка сортировки: {ex.Message}");
            }
        }

        private void ClearAnimation()
        {
            ReturnAllSteps(_quickSortSteps);
            ReturnAllSteps(_mergeLeft);
            ReturnAllSteps(_mergeRight);
            ReturnAllSteps(_mergeResult);
        }

        private void CleanupTempFiles()
        {
            foreach (var file in _tempFiles)
                try { if (File.Exists(file)) File.Delete(file); } catch { }
            _tempFiles.Clear();
        }

        #endregion

        #region === QuickSort ===

        private async Task QuickSortWithAnimation(List<string[]> chunk, int keyIdx, int delay)
        {
            Log($"[QuickSort] Старт Hoare: {chunk.Count} элементов");
            await QuickSortHoare(chunk, 0, chunk.Count - 1, keyIdx, delay);
            Log($"[QuickSort] Завершено");
        }

        private async Task QuickSortHoare(List<string[]> arr, int left, int right, int keyIdx, int delay)
        {
            if (left >= right || _stopRequested) return;

            var pivotIndex = await PartitionHoare(arr, left, right, keyIdx, delay);
            await QuickSortHoare(arr, left, pivotIndex, keyIdx, delay);
            await QuickSortHoare(arr, pivotIndex + 1, right, keyIdx, delay);
        }

        private async Task<int> PartitionHoare(List<string[]> arr, int left, int right, int keyIdx, int delay)
        {
            int mid = left + (right - left) / 2;
            var pivotValue = arr[mid][keyIdx];
            int i = left - 1, j = right + 1;

            await ShowQuickSortStep(arr, keyIdx, mid, left, right, null, null, null, null, delay, "Выбор pivot");

            while (true)
            {
                do { i++; } while (i <= right && Compare(arr[i][keyIdx], pivotValue) < 0);
                do { j--; } while (j >= left && Compare(arr[j][keyIdx], pivotValue) > 0);

                await ShowQuickSortStep(arr, keyIdx, mid, left, right, i, j, null, null, delay,
                    $"Сравниваем: [{i}]='{arr[i][keyIdx]}' и [{j}]='{arr[j][keyIdx]}'");

                if (i >= j) break;

                await ShowQuickSortStep(arr, keyIdx, mid, left, right, null, null, i, j, delay, "Обмен");
                (arr[i], arr[j]) = (arr[j], arr[i]);
                await ShowQuickSortStep(arr, keyIdx, mid, left, right, null, null, null, null, delay, "После обмена");
            }

            return j;
        }

        private async Task ShowQuickSortStep(
            List<string[]> data, int keyIdx,
            int? pivotIndex, int? left, int? right,
            int? compareA, int? compareB, int? swapA, int? swapB,
            int delay, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReturnAllSteps(_quickSortSteps);

                var title = RentStep();
                title.Text = $"QuickSort: {message}";
                title.BgColor = "#ffebee";
                title.IsTitle = true;
                _quickSortSteps.Add(title);

                for (int idx = 0; idx < data.Count; idx++)
                {
                    var step = RentStep();
                    step.Text = $"[{idx}] {string.Join(" | ", data[idx])}";
                    step.ShowArrow = idx == pivotIndex;
                    step.BgColor = "#ffffff";
                    step.BorderColor = "Transparent";
                    step.BorderThickness = 0;

                    bool inRange = left.HasValue && right.HasValue && idx >= left && idx <= right;
                    if (inRange)
                    {
                        step.BorderColor = "BlueViolet";
                        step.BorderThickness = 2;
                    }

                    if (idx == compareA || idx == compareB) step.BgColor = "#ff9800";
                    else if (idx == swapA || idx == swapB) step.BgColor = "#f44336";
                    else if (idx == pivotIndex) step.BgColor = "#ffffff";
                    else if (!inRange) step.BgColor = "#f5f5f5";

                    _quickSortSteps.Add(step);
                }
            }, DispatcherPriority.Normal);

            await Task.Delay(delay);
        }

        #endregion

        #region === Алгоритмы ===

        private async Task DirectMergeSort(int keyIdx, int delay)
        {
            var chunks = SplitIntoChunks(3);
            var files = new List<string>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                await QuickSortWithAnimation(chunk, keyIdx, delay);
                var file = SaveChunk(chunk, $"chunk_{i}");
                files.Add(file);
                await ShowChunkStep($"Кусок {i + 1}", chunk, delay);
            }

            var result = files[0];
            for (int i = 1; i < files.Count; i++)
                result = await MergeTwoFiles(result, files[i], keyIdx, delay, "Прямое слияние");

            UpdateTable(LoadFromFile(result), "Результат: Прямое слияние");
        }

        private async Task NaturalMergeSort(int keyIdx, int delay)
        {
            var series = FindNaturalSeries(keyIdx);
            var files = new List<string>();

            for (int i = 0; i < series.Count; i++)
            {
                var s = series[i];
                await QuickSortWithAnimation(s, keyIdx, delay);
                var file = SaveChunk(s, $"series_{i}");
                files.Add(file);
                await ShowChunkStep($"Серия {i + 1}", s, delay);
            }

            var result = files[0];
            for (int i = 1; i < files.Count; i++)
                result = await MergeTwoFiles(result, files[i], keyIdx, delay, "Естественное слияние");

            UpdateTable(LoadFromFile(result), "Результат: Естественное слияние");
        }

        private async Task MultiwayMergeSort(int keyIdx, int delay)
        {
            var chunks = SplitIntoChunks(3);
            var files = new List<string>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                await QuickSortWithAnimation(chunk, keyIdx, delay);
                var file = SaveChunk(chunk, $"chunk_{i}");
                files.Add(file);
                await ShowChunkStep($"Кусок {i + 1}", chunk, delay);
            }

            var result = await MultiwayMerge(files, keyIdx, delay);
            UpdateTable(LoadFromFile(result), "Результат: Многопутевое слияние");
        }

        #endregion

        #region === Многопутевое слияние ===

        private class HeapNode : IComparable<HeapNode>
        {
            public string[] Row { get; set; } = Array.Empty<string>();
            public int FileIndex { get; set; }
            public string Key { get; set; } = "";
            public bool IsNumeric { get; set; }
            public double NumericValue { get; set; }

            public int CompareTo(HeapNode? other)
            {
                if (other == null) return 1;
                if (IsNumeric && other.IsNumeric)
                    return NumericValue.CompareTo(other.NumericValue);
                return string.Compare(Key, other.Key, StringComparison.OrdinalIgnoreCase);
            }
        }

        private async Task<string> MultiwayMerge(List<string> files, int keyIdx, int delay)
        {
            var output = Path.Combine(_tempDir, "multiway_result.tmp");
            _tempFiles.Add(output);
            if (File.Exists(output)) File.Delete(output);

            await using var writer = new StreamWriter(output, false, Encoding.UTF8);
            var readers = new List<StreamReader>();
            var heap = new PriorityQueue<HeapNode, HeapNode>();

            try
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var reader = new StreamReader(files[i], Encoding.UTF8);
                    readers.Add(reader);
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        var row = line.Split(';').Select(v => v.Trim()).ToArray();
                        var node = CreateHeapNode(row, keyIdx, i);
                        if (node != null) heap.Enqueue(node, node);
                    }
                }

                int total = _data.Count, done = 0;
                await ShowHeapVisualization(heap, null, delay);
                await ShowMultiwayMergeVisualization(heap, null, total, done, delay);

                while (heap.Count > 0 && !_stopRequested)
                {
                    var min = heap.Dequeue();
                    await writer.WriteLineAsync(string.Join(";", min.Row));

                    if (_resultBuffer.Count >= 10) _resultBuffer.RemoveAt(0);
                    _resultBuffer.Add(string.Join(" | ", min.Row));
                    done++;

                    await ShowHeapVisualization(heap, min, delay);
                    await ShowMultiwayMergeVisualization(heap, min, total, done, delay);

                    var nextLine = readers[min.FileIndex].ReadLine();
                    if (nextLine != null)
                    {
                        var row = nextLine.Split(';').Select(v => v.Trim()).ToArray();
                        var node = CreateHeapNode(row, keyIdx, min.FileIndex);
                        if (node != null) heap.Enqueue(node, node);
                    }
                }

                Log($"Многопутевое слияние: {done} строк");
            }
            finally
            {
                foreach (var r in readers)
                    try { r.Close(); r.Dispose(); } catch { }
            }

            return output;
        }

        private HeapNode? CreateHeapNode(string[] row, int keyIdx, int fileIdx)
        {
            if (keyIdx >= row.Length) return null;
            var key = row[keyIdx];
            var node = new HeapNode { Row = row, FileIndex = fileIdx, Key = key };

            if (double.TryParse(key, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
            {
                node.IsNumeric = true;
                node.NumericValue = num;
            }
            return node;
        }

        private async Task ShowHeapVisualization(PriorityQueue<HeapNode, HeapNode> heap, HeapNode? selected, int delay)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReturnAllSteps(_quickSortSteps);

                var title = RentStep();
                title.Text = "min-heap: текущие минимальные элементы";
                title.BgColor = "#fff3e0";
                title.IsTitle = true;
                _quickSortSteps.Add(title);

                var items = heap.UnorderedItems.Select(x => x.Element).ToList();
                foreach (var node in items)
                {
                    var step = RentStep();
                    step.Text = $"Ф{node.FileIndex + 1}: {node.Key} → {string.Join(" | ", node.Row)}";
                    step.BgColor = selected?.FileIndex == node.FileIndex ? "#ff8a65" : "#ffe0b2";
                    step.ShowArrow = selected?.FileIndex == node.FileIndex;
                    step.BorderThickness = selected?.FileIndex == node.FileIndex ? 2 : 0;
                    step.BorderColor = "BlueViolet";
                    _quickSortSteps.Add(step);
                }
            }, DispatcherPriority.Normal);

            await Task.Delay(delay);
        }

        private async Task ShowMultiwayMergeVisualization(PriorityQueue<HeapNode, HeapNode> heap, HeapNode? selected, int total, int done, int delay)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReturnAllSteps(_mergeLeft);
                ReturnAllSteps(_mergeRight);
                ReturnAllSteps(_mergeResult);

                var progress = total > 0 ? (done * 100 / total) : 0;
                var title = RentStep();
                title.Text = $"Многопутевое слияние: {done}/{total} ({progress}%)";
                title.BgColor = "#e3f2fd";
                title.IsTitle = true;
                _mergeLeft.Add(title);

                var items = heap.UnorderedItems.Select(x => x.Element).ToList();
                int leftCount = 0, rightCount = 0;
                foreach (var node in items)
                {
                    var step = RentStep();
                    step.Text = string.Join(" | ", node.Row);
                    step.BgColor = selected?.FileIndex == node.FileIndex ? "#42a5f5" : "#bbdefb";
                    step.ShowArrow = selected?.FileIndex == node.FileIndex;

                    if (leftCount <= rightCount) { _mergeLeft.Add(step); leftCount++; }
                    else { _mergeRight.Add(step); rightCount++; }
                }

                foreach (var line in _resultBuffer)
                {
                    var step = RentStep();
                    step.Text = line;
                    step.BgColor = "#e0e0e0";
                    _mergeResult.Add(step);
                }
            }, DispatcherPriority.Normal);

            await Task.Delay(delay);
        }

        #endregion

        #region === Слияние двух файлов ===

        private async Task<string> MergeTwoFiles(string f1, string f2, int keyIdx, int delay, string type)
        {
            var left = File.ReadAllLines(f1, Encoding.UTF8).Select(l => l.Split(';').Select(v => v.Trim()).ToArray()).ToList();
            var right = File.ReadAllLines(f2, Encoding.UTF8).Select(l => l.Split(';').Select(v => v.Trim()).ToArray()).ToList();
            var output = Path.Combine(_tempDir, "merge.tmp");
            _tempFiles.Add(output);
            if (File.Exists(output)) File.Delete(output);

            await using var writer = new StreamWriter(output, false, Encoding.UTF8);
            int i = 0, j = 0, total = left.Count + right.Count, done = 0;

            while (i < left.Count && j < right.Count && !_stopRequested)
            {
                await ShowMergeVisualization(left, right, i, j, total, done, delay);

                if (Compare(left[i][keyIdx], right[j][keyIdx]) <= 0)
                {
                    await writer.WriteLineAsync(string.Join(";", left[i]));
                    i++; done++;
                }
                else
                {
                    await writer.WriteLineAsync(string.Join(";", right[j]));
                    j++; done++;
                }
            }

            while (i < left.Count && !_stopRequested) { await writer.WriteLineAsync(string.Join(";", left[i])); i++; done++; }
            while (j < right.Count && !_stopRequested) { await writer.WriteLineAsync(string.Join(";", right[j])); j++; done++; }

            await ShowMergeVisualization(left, right, i, j, total, done, delay);
            return output;
        }

        private async Task ShowMergeVisualization(List<string[]> left, List<string[]> right, int i, int j, int total, int done, int delay)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReturnAllSteps(_mergeLeft);
                ReturnAllSteps(_mergeRight);

                var progress = total > 0 ? (done * 100 / total) : 0;
                var title = RentStep();
                title.Text = $"Слияние: {done}/{total} ({progress}%)";
                title.BgColor = "#e3f2fd";
                title.IsTitle = true;
                _mergeLeft.Add(title);

                for (int idx = 0; idx < left.Count; idx++)
                {
                    var step = RentStep();
                    step.Text = string.Join(" | ", left[idx]);
                    step.BgColor = idx == i ? "#42a5f5" : "#bbdefb";
                    step.ShowArrow = idx == i;
                    _mergeLeft.Add(step);
                }

                for (int idx = 0; idx < right.Count; idx++)
                {
                    var step = RentStep();
                    step.Text = string.Join(" | ", right[idx]);
                    step.BgColor = idx == j ? "#66bb6a" : "#c8e6c9";
                    step.ShowArrow = idx == j;
                    _mergeRight.Add(step);
                }
            }, DispatcherPriority.Normal);

            await Task.Delay(delay);
        }

        #endregion

        #region === Вспомогательное ===

        private List<List<string[]>> SplitIntoChunks(int size) =>
            Enumerable.Range(0, (int)Math.Ceiling(_data.Count / (double)size))
                      .Select(k => _data.Skip(k * size).Take(size).ToList())
                      .ToList();

        private string SaveChunk(List<string[]> chunk, string name)
        {
            var path = Path.Combine(_tempDir, $"{name}.tmp");
            _tempFiles.Add(path);
            File.WriteAllLines(path, chunk.Select(r => string.Join(";", r)), Encoding.UTF8);
            return path;
        }

        private List<string[]> LoadFromFile(string path) =>
            File.ReadAllLines(path, Encoding.UTF8)
                .Select(l => l.Split(';').Select(v => v.Trim()).ToArray())
                .ToList();

        private List<List<string[]>> FindNaturalSeries(int keyIdx)
        {
            var series = new List<List<string[]>>();
            var current = new List<string[]>();

            foreach (var row in _data)
            {
                if (current.Count == 0 || Compare(current.Last()[keyIdx], row[keyIdx]) <= 0)
                    current.Add(row);
                else
                {
                    series.Add(current);
                    current = new List<string[]> { row };
                }
            }
            if (current.Count > 0) series.Add(current);
            return series;
        }

        private async Task ShowChunkStep(string title, IEnumerable<string[]> data, int delay)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReturnAllSteps(_quickSortSteps);

                var t = RentStep();
                t.Text = $"Фаза: сортировка чанков — {title}";
                t.BgColor = "#e8f5e8";
                t.IsTitle = true;
                _quickSortSteps.Add(t);

                foreach (var row in data)
                {
                    var step = RentStep();
                    step.Text = string.Join(" | ", row);
                    step.BgColor = "#f9f9f9";
                    _quickSortSteps.Add(step);
                }
            }, DispatcherPriority.Normal);

            await Task.Delay(delay);
        }

        private int Compare(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : 1;
            if (string.IsNullOrEmpty(b)) return -1;

            if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out double da) &&
                double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out double db))
                return da.CompareTo(db);

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region === UI ===

        private void UpdateTable(List<string[]> data, string title)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _tableRows.Clear();
                _tableRows.Add(new DisplayRow { Text = title, BgColor = "#e8f5e8" });
                foreach (var row in data)
                    _tableRows.Add(new DisplayRow { Text = string.Join(" | ", row), BgColor = "#f9f9f9" });
            });
        }

        private void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:HH:mm:ss} — {msg}");
            StatusText.Text = msg;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            LoadFileBtn.Click -= LoadFileBtn_Click;
            SaveFileBtn.Click -= SaveFileBtn_Click;
            StartBtn.Click -= StartBtn_Click;
            CleanupTempFiles();
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #region === Модели ===

    public class DisplayRow
    {
        public string Text { get; set; } = "";
        public string BgColor { get; set; } = "#ffffff";
    }

    public class MergeStep
    {
        public string Text { get; set; } = "";
        public string BgColor { get; set; } = "#ffffff";
        public string BorderColor { get; set; } = "Transparent";
        public double BorderThickness { get; set; } = 0;
        public bool ShowArrow { get; set; } = false;
        public bool IsTitle { get; set; } = false;
    }

    #endregion

    #region === Конвертеры ===

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? 1.0 : 0.0;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? FontWeight.Bold : FontWeight.Normal;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class BoolToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double thickness && thickness > 0 ? 2 : 0;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
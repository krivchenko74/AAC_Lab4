// <DOCUMENT filename="Task2.axaml.cs">
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
        private ObservableCollection<DisplayRow> _tableRows = new();
        private ObservableCollection<MergeStep> _quickSortSteps = new();
        private ObservableCollection<MergeStep> _mergeLeft = new();
        private ObservableCollection<MergeStep> _mergeRight = new();
        private ObservableCollection<MergeStep> _mergeResult = new();
        private bool _stopRequested = false;
        private string _tempDir = Path.Combine(Path.GetTempPath(), "ExtSort_" + Guid.NewGuid().ToString("N")[..8]);
        private List<string> _resultBuffer = new();

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
            StartBtn.Click += async (_, __) => await StartSort();
            StopBtn.Click += (_, __) => _stopRequested = true;
        }

        #region === Загрузка ===

        private async void LoadFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Title = "Выберите CSV", AllowMultiple = false };
            var result = await dlg.ShowAsync(this.FindAncestorOfType<Window>());
            if (result?.Length > 0) LoadCsv(result[0]);
        }

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

            string method = ((ComboBoxItem)MethodBox.SelectedItem)?.Content?.ToString() ?? "";
            string keyName = KeyBox.SelectedItem?.ToString() ?? "";
            int keyIndex = KeyBox.ItemsSource.Cast<object>().ToList().IndexOf(KeyBox.SelectedItem);
            if (keyIndex < 0) { Log("Ключ не выбран"); return; }

            int delay = (int)(DelayBox.Value ?? 400);

            Log($"Запуск: {method} по '{keyName}'");

            if (method.Contains("прямое", StringComparison.OrdinalIgnoreCase))
                await DirectMergeSort(keyIndex, delay);
            else if (method.Contains("естественное", StringComparison.OrdinalIgnoreCase))
                await NaturalMergeSort(keyIndex, delay);
            else
                await MultiwayMergeSort(keyIndex, delay);

            if (!_stopRequested) Log("Сортировка завершена");
        }

        private void ClearAnimation()
        {
            _quickSortSteps.Clear();
            _mergeLeft.Clear();
            _mergeRight.Clear();
            _mergeResult.Clear();
            _resultBuffer.Clear();
        }

        #endregion

        #region === QuickSort ===

        private async Task QuickSortWithAnimation(List<string[]> chunk, int keyIdx, int delay)
        {
            Log($"[QuickSort] Старт Lomuto: {chunk.Count} элементов");
            await QuickSortLomuto(chunk, 0, chunk.Count - 1, keyIdx, delay);
            Log($"[QuickSort] Завершено");
        }

        private async Task QuickSortLomuto(
            List<string[]> arr,
            int left,
            int right,
            int keyIdx,
            int delay)
        {
            if (left >= right || _stopRequested) return;

            Log($"[Lomuto] Рекурсия: [{left}..{right}] (размер: {right - left + 1})");

            await ShowQuickSortStep(arr, keyIdx, null, left, right, null, null, null, null, delay,
                $"Разбиение: [{left}..{right}]");

            int partitionIndex = await PartitionLomuto(arr, left, right, keyIdx, delay);

            await QuickSortLomuto(arr, left, partitionIndex - 1, keyIdx, delay);
            await QuickSortLomuto(arr, partitionIndex + 1, right, keyIdx, delay);
        }

        private async Task<int> PartitionLomuto(
            List<string[]> arr,
            int left,
            int right,
            int keyIdx,
            int delay)
        {
            var pivotValue = arr[right][keyIdx];
            int i = left - 1;

            await ShowQuickSortStep(arr, keyIdx, right, left, right, null, null, null, null, delay,
                $"Pivot: [{right}] = '{pivotValue}' (в конце)");

            for (int j = left; j < right; j++)
            {
                await ShowQuickSortStep(arr, keyIdx, right, left, right, j, right, null, null, delay,
                    $"Сравниваем: [{j}]='{arr[j][keyIdx]}' и pivot='{pivotValue}'");

                if (Compare(arr[j][keyIdx], pivotValue) <= 0)
                {
                    i++;
                    if (i != j)
                    {
                        (arr[i], arr[j]) = (arr[j], arr[i]);
                        await ShowQuickSortStep(arr, keyIdx, right, left, right, null, null, i, j, delay,
                            "Обмен");
                        await ShowQuickSortStep(arr, keyIdx, right, left, right, null, null, null, null, delay,
                            "После обмена");
                    }
                }
            }

            i++;
            if (i != right)
            {
                (arr[i], arr[right]) = (arr[right], arr[i]);
                await ShowQuickSortStep(arr, keyIdx, right, left, right, null, null, i, right, delay,
                    "Финальный обмен: pivot на место");
                await ShowQuickSortStep(arr, keyIdx, right, left, right, null, null, null, null, delay,
                    $"Pivot зафиксирован на индексе {i}");
            }

            return i;
        }

        private async Task ShowQuickSortStep(
            List<string[]> data,
            int keyIdx,
            int? pivotIndex = null,
            int? left = null,
            int? right = null,
            int? compareA = null,
            int? compareB = null,
            int? swapA = null,
            int? swapB = null,
            int delay = 200,
            string message = "")
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _quickSortSteps.Clear();
                _quickSortSteps.Add(new MergeStep
                {
                    Text = $"QuickSort: {message}",
                    BgColor = "#ffebee",
                    IsTitle = true
                });

                for (int idx = 0; idx < data.Count; idx++)
                {
                    var step = new MergeStep
                    {
                        Text = $"[{idx}] {string.Join(" | ", data[idx])}",
                        ShowArrow = idx == pivotIndex,
                        BgColor = "#ffffff",
                        BorderColor = "Transparent",
                        BorderThickness = 0
                    };

                    bool inCurrentSubarray = idx >= left && idx <= right;

                    if (inCurrentSubarray)
                    {
                        step.BorderColor = "#000000";
                        step.BorderThickness = 2;
                    }

                    if (idx == compareA || idx == compareB)
                    {
                        step.BgColor = "#ff9800";
                    }
                    else if (idx == swapA || idx == swapB)
                    {
                        step.BgColor = "#f44336";
                    }
                    else if (idx == pivotIndex)
                    {
                        step.BgColor = "#ffffff";
                    }
                    else if (!inCurrentSubarray)
                    {
                        step.BgColor = "#f5f5f5";
                    }

                    _quickSortSteps.Add(step);
                }
            }, DispatcherPriority.Render);

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
            var chunks = SplitIntoChunks(5);
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

        #region === Многопутевое слияние с min-heap ===

        private class HeapNode : IComparable<HeapNode>
        {
            public string[] Row { get; set; }
            public int FileIndex { get; set; }
            public string Key { get; set; }
            public bool IsNumeric { get; set; }
            public double NumericValue { get; set; }

            public int CompareTo(HeapNode other)
            {
                if (IsNumeric && other.IsNumeric)
                    return NumericValue.CompareTo(other.NumericValue);
                return string.Compare(Key, other.Key, StringComparison.OrdinalIgnoreCase);
            }

            public override string ToString() => $"F{FileIndex + 1}: {Key}";
        }

        private async Task<string> MultiwayMerge(List<string> files, int keyIdx, int delay)
{
    var readers = files.Select(f => new StreamReader(f, Encoding.UTF8)).ToList();
    var heap = new PriorityQueue<HeapNode, HeapNode>();
    var output = Path.Combine(_tempDir, "result.tmp");
    if (File.Exists(output)) File.Delete(output);
    await using var writer = new StreamWriter(output, false, Encoding.UTF8);

    // Инициализация: первая строка из каждого файла
    for (int i = 0; i < readers.Count; i++)
    {
        var line = readers[i].ReadLine();
        if (line != null)
        {
            var row = line.Split(';').Select(v => v.Trim()).ToArray();
            var node = CreateHeapNode(row, keyIdx, i);
            if (node != null) heap.Enqueue(node, node);
        }
    }

    int total = _data.Count;
    int done = 0;

    // Первый кадр: показать начальное состояние
    await ShowMultiwayMergeVisualization(heap, null, total, done, delay);

    while (heap.Count > 0 && !_stopRequested)
    {
        var min = heap.Dequeue();  // Извлекаем минимум
        await writer.WriteLineAsync(string.Join(";", min.Row));
        _resultBuffer.Add(string.Join(" | ", min.Row));
        if (_resultBuffer.Count > 10) _resultBuffer.RemoveAt(0);
        done++;

        // Показываем состояние ПОСЛЕ извлечения, но ДО добавления новой строки
        await ShowMultiwayMergeVisualization(heap, min, total, done, delay);

        // Читаем следующую строку из ТОГО ЖЕ файла
        var nextLine = readers[min.FileIndex].ReadLine();
        if (nextLine != null)
        {
            var row = nextLine.Split(';').Select(v => v.Trim()).ToArray();
            var node = CreateHeapNode(row, keyIdx, min.FileIndex);
            if (node != null)
            {
                heap.Enqueue(node, node);  // Добавляем новую голову
            }
        }

        // Показываем состояние ПОСЛЕ добавления (если было)
        // Это и есть кадр, где куча должна содержать 2 элемента
        await ShowMultiwayMergeVisualization(heap, null, total, done, delay);
    }

    foreach (var r in readers) r.Close();
    Log($"Многопутевое слияние: {done} строк");
    return output;
}

        private HeapNode CreateHeapNode(string[] row, int keyIdx, int fileIdx)
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

        private async Task ShowMultiwayMergeVisualization(
    PriorityQueue<HeapNode, HeapNode> heap,
    HeapNode? selected,
    int total, int done,
    int delay)
{
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        _quickSortSteps.Clear();
        _mergeLeft.Clear();
        _mergeRight.Clear();
        _mergeResult.Clear();

        int progress = total > 0 ? (done * 100 / total) : 0;

        _quickSortSteps.Add(new MergeStep
        {
            Text = $"Многопутевое слияние: {done}/{total} строк ({progress}%)",
            BgColor = "#e8f5e9",
            IsTitle = true
        });

        // КЛЮЧЕВОЕ: Принудительно собираем ВСЕ элементы из кучи
        var heads = heap.UnorderedItems
                        .Select(x => x.Element)
                        .OrderBy(n => n.FileIndex)
                        .ToList();

        if (heads.Count == 0)
        {
            _quickSortSteps.Add(new MergeStep
            {
                Text = "Слияние завершено — куча пуста",
                BgColor = "#fff3e0",
                IsTitle = true
            });
        }
        else
        {
            _quickSortSteps.Add(new MergeStep
            {
                Text = $"min-heap: {heads.Count} активных файлов",
                BgColor = "#fff3e0",
                IsTitle = true
            });

            foreach (var node in heads)
            {
                bool isSelected = selected != null && node.FileIndex == selected.FileIndex;
                string keyVal = node.IsNumeric ? node.NumericValue.ToString("G") : $"\"{node.Key}\"";

                _quickSortSteps.Add(new MergeStep
                {
                    Text = $"Файл {node.FileIndex + 1}: [{keyVal}] → {string.Join(" | ", node.Row)}",
                    BgColor = isSelected ? "#ff7043" : "#ffe0b2",
                    BorderColor = isSelected ? "#d84315" : "#fb8c00",
                    BorderThickness = isSelected ? 3 : 1,
                    ShowArrow = isSelected
                });
            }

            // Подсказка: следующий минимум
            if (selected == null && heads.Count > 0)
            {
                var nextMin = heads.MinBy(h => h);
                string nextKey = nextMin.IsNumeric ? nextMin.NumericValue.ToString("G") : nextMin.Key;
                _quickSortSteps.Add(new MergeStep
                {
                    Text = $"Следующий минимум → Файл {nextMin.FileIndex + 1}: [{nextKey}]",
                    BgColor = "#e8f5e9",
                    IsTitle = true
                });
            }
        }

        // Результат
        _quickSortSteps.Add(new MergeStep
        {
            Text = "Результат (последние 10 строк):",
            BgColor = "#f5f5f5",
            IsTitle = true
        });
        foreach (var line in _resultBuffer)
        {
            _quickSortSteps.Add(new MergeStep { Text = "→ " + line, BgColor = "#fafafa" });
        }

    }, DispatcherPriority.Render);

    await Task.Delay(delay);
}

        #endregion

        #region === Слияние двух файлов ===

        private async Task<string> MergeTwoFiles(string f1, string f2, int keyIdx, int delay, string type)
        {
            var left = File.ReadAllLines(f1, Encoding.UTF8).Select(l => l.Split(';').Select(v => v.Trim()).ToArray()).ToList();
            var right = File.ReadAllLines(f2, Encoding.UTF8).Select(l => l.Split(';').Select(v => v.Trim()).ToArray()).ToList();
            var result = new List<string[]>();
            var output = Path.Combine(_tempDir, "merge.tmp");
            var writer = new StreamWriter(output, false, Encoding.UTF8);

            int i = 0, j = 0, total = left.Count + right.Count, done = 0;

            while (i < left.Count && j < right.Count && !_stopRequested)
            {
                await ShowMergeVisualization(left, right, result, i, j, total, done, delay);

                if (Compare(left[i][keyIdx], right[j][keyIdx]) <= 0)
                {
                    result.Add(left[i]);
                    await writer.WriteLineAsync(string.Join(";", left[i]));
                    i++; done++;
                }
                else
                {
                    result.Add(right[j]);
                    await writer.WriteLineAsync(string.Join(";", right[j]));
                    j++; done++;
                }
            }

            while (i < left.Count) { result.Add(left[i]); await writer.WriteLineAsync(string.Join(";", left[i])); i++; done++; }
            while (j < right.Count) { result.Add(right[j]); await writer.WriteLineAsync(string.Join(";", right[j])); j++; done++; }

            await ShowMergeVisualization(left, right, result, i, j, total, done, delay);
            writer.Close();
            return output;
        }

        private async Task ShowMergeVisualization(
            List<string[]> left, List<string[]> right, List<string[]> result,
            int i, int j, int total, int done, int delay)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mergeLeft.Clear();
                _mergeRight.Clear();
                _mergeResult.Clear();

                var progress = total > 0 ? (done * 100 / total) : 0;
                _mergeLeft.Add(new MergeStep
                {
                    Text = $"Слияние: {done}/{total} ({progress}%)",
                    BgColor = "#e3f2fd",
                    IsTitle = true
                });

                for (int idx = 0; idx < left.Count; idx++)
                {
                    _mergeLeft.Add(new MergeStep
                    {
                        Text = string.Join(" | ", left[idx]),
                        BgColor = idx == i ? "#42a5f5" : "#bbdefb",
                        ShowArrow = idx == i
                    });
                }

                for (int idx = 0; idx < right.Count; idx++)
                {
                    _mergeRight.Add(new MergeStep
                    {
                        Text = string.Join(" | ", right[idx]),
                        BgColor = idx == j ? "#66bb6a" : "#c8e6c9",
                        ShowArrow = idx == j
                    });
                }

                foreach (var row in result.TakeLast(10))
                {
                    _mergeResult.Add(new MergeStep
                    {
                        Text = string.Join(" | ", row),
                        BgColor = "#e0e0e0"
                    });
                }
            }, DispatcherPriority.Render);

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
                _quickSortSteps.Clear();
                _quickSortSteps.Add(new MergeStep 
                { 
                    Text = $"Фаза: сортировка чанков — {title}", 
                    BgColor = "#e8f5e8", 
                    IsTitle = true 
                });
                foreach (var row in data)
                    _quickSortSteps.Add(new MergeStep { Text = string.Join(" | ", row), BgColor = "#f9f9f9" });
            }, DispatcherPriority.Render);

            await Task.Delay(delay);
        }

        private int Compare(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : 1;
            if (string.IsNullOrEmpty(b)) return -1;

            if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out double da) &&
                double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out double db))
            {
                return da.CompareTo(db);
            }

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

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
            base.OnDetachedFromVisualTree(e);
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #region === Модели и конвертеры ===

    public class DisplayRow { public string Text { get; set; } = ""; public string BgColor { get; set; } = "#ffffff"; }

    public class MergeStep : INotifyPropertyChanged
    {
        public string _text = "";
        public string _bgColor = "#ffffff";
        public string _borderColor = "Transparent";
        public double _borderThickness = 0;
        public bool _showArrow = false;
        public bool _isTitle = false;

        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }
        public string BgColor { get => _bgColor; set { _bgColor = value; OnPropertyChanged(); } }
        public string BorderColor { get => _borderColor; set { _borderColor = value; OnPropertyChanged(); } }
        public double BorderThickness { get => _borderThickness; set { _borderThickness = value; OnPropertyChanged(); } }
        public bool ShowArrow { get => _showArrow; set { _showArrow = value; OnPropertyChanged(); } }
        public bool IsTitle { get => _isTitle; set { _isTitle = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? 1.0 : 0.0;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
    
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
    
    public class BoolToBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double thickness && thickness != 0) return new Thickness(2);
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? FontWeight.Bold : FontWeight.Normal;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    #endregion
}
// </DOCUMENT>
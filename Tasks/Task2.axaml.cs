using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Interactivity;

namespace SortingDemo.Tasks
{
    public partial class Task2 : UserControl
    {
        private DataTable _tableData = new();
        private bool _stopRequested;

        public Task2()
        {
            InitializeComponent();
            SaveFileBtn.Click += SaveBtn_Click;
            LoadFileBtn.Click += LoadFileBtn_Click;
            StartBtn.Click += async (_, __) => await StartSort();
            StopBtn.Click += (_, __) => _stopRequested = true;
        }

        private void LoadFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogBox.Text = $"Файл не найден: {filePath}\n";
                return;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2)
                {
                    LogBox.Text = "Файл пустой или содержит только заголовок\n";
                    return;
                }

                _tableData = new DataTable();

                // Заголовок CSV
                var headers = lines[0].Split(',');
                Log(string.Join(' ', headers));
                var firstDataRow = lines[1].Split(',');
                Log(string.Join(' ', firstDataRow));
                var keyList = new List<string>();

                for (int i = 0; i < headers.Length; i++)
                {
                    // Определяем тип столбца
                    var columnType = double.TryParse(firstDataRow[i], System.Globalization.NumberStyles.Any, 
                                                     System.Globalization.CultureInfo.InvariantCulture, out _) 
                                      ? typeof(double) 
                                      : typeof(string);

                    _tableData.Columns.Add(headers[i].Trim(), columnType);
                    keyList.Add(headers[i].Trim());
                }

                // Считываем строки
                for (int i = 1; i < lines.Length; i++)
                {
                    Log(lines[i]);
                    var values = lines[i].Split(',');
                    var row = _tableData.NewRow();
                    for (int j = 0; j < values.Length; j++)
                    {
                        if (_tableData.Columns[j].DataType == typeof(double))
                            row[j] = double.Parse(values[j], System.Globalization.CultureInfo.InvariantCulture);
                        else
                            row[j] = values[j];
                    }
                    _tableData.Rows.Add(row);
                }

                Log($"Столбцов: {_tableData.Columns.Count}");
                Log($"Строк: {_tableData.Rows.Count}");

                foreach (DataColumn col in _tableData.Columns)
                    Log($"Колонка: {col.ColumnName}, Тип: {col.DataType}");
                Dispatcher.UIThread.Post(() =>
                {
                    Table.ItemsSource = _tableData.DefaultView;
                    Table.InvalidateMeasure();

                    // Для отладки
                    LogBox.Text += $"[UI] Columns={Table.Columns.Count}\n";
                });

                // Устанавливаем ключи в ComboBox через ItemsSource
                KeyBox.ItemsSource = keyList;
                KeyBox.SelectedIndex = 0; // по умолчанию первый столбец

                LogBox.Text = $"Файл '{Path.GetFileName(filePath)}' загружен. Строк: {lines.Length - 1}\n";
            }
            catch (Exception ex)
            {
                LogBox.Text = $"Ошибка при чтении файла: {ex.Message}\n";
            }
        }



        
        private async void LoadFileBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Выберите CSV файл",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "CSV files", Extensions = { "csv" } }
                },
                AllowMultiple = false
            };

            string[]? result = await dlg.ShowAsync(this.VisualRoot as Window);

            if (result != null && result.Length > 0)
            {
                LoadFile(result[0]);
                FillKeyBox();
            }
        }

        private void FillKeyBox()
        {
            if (_tableData != null)
            {
                var columns = _tableData.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                KeyBox.ItemsSource = columns; // <-- используем ItemsSource
                if (columns.Count > 0)
                    KeyBox.SelectedIndex = 0; // выбираем первый столбец по умолчанию
            }
        }


        private async Task StartSort()
        {
            if (_tableData.Rows.Count == 0) return;
            _stopRequested = false;

            int delay = (int)(DelayBox?.Value ?? 500);
            string key = KeyBox.SelectedItem?.ToString() ?? _tableData.Columns[0].ColumnName;

            LogBox.Text += $"Начало сортировки по ключу '{key}'\n";

            await DirectMergeSort(key, delay);

            LogBox.Text += "Сортировка завершена\n";
        }

        private void PopulateKeyBox()
        {
            KeyBox.ItemsSource = _tableData.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            if (_tableData.Columns.Count > 0)
                KeyBox.SelectedIndex = 0; // выбрать первый элемент по умолчанию
        }
        
        private async Task DirectMergeSort(string key, int delay)
        {
            if (_tableData.Rows.Count == 0) return;
            _stopRequested = false;

            var rows = _tableData.Rows.Cast<DataRow>().ToList();
            int n = rows.Count;
            int width = 1;

            while (width < n && !_stopRequested)
            {
                for (int i = 0; i < n; i += 2 * width)
                {
                    int left = i;
                    int mid = Math.Min(i + width, n);
                    int right = Math.Min(i + 2 * width, n);

                    var leftPart = rows.Skip(left).Take(mid - left).ToList();
                    var rightPart = rows.Skip(mid).Take(right - mid).ToList();

                    var merged = Merge(leftPart, rightPart, key);

                    for (int j = 0; j < merged.Count; j++)
                        rows[left + j] = merged[j];

                    // Промежуточное обновление UI
                    _tableData.Clear();
                    foreach (var row in rows)
                        _tableData.ImportRow(row);

                    await Task.Delay(delay);
                }
                width *= 2;
            }

            // Финальное обновление
            if (!_stopRequested)
            {
                _tableData.Clear();
                foreach (var row in rows)
                    _tableData.ImportRow(row);

                Dispatcher.UIThread.Post(() =>
                {
                    LogBox.Text += "Сортировка завершена\n";
                });
            }
        }

        private List<DataRow> Merge(List<DataRow> left, List<DataRow> right, string key)
        {
            List<DataRow> result = new();
            int i = 0, j = 0;

            while (i < left.Count && j < right.Count)
            {
                int cmp = Comparer<object>.Default.Compare(left[i][key], right[j][key]);
                if (cmp <= 0)
                {
                    result.Add(left[i]);
                    i++;
                }
                else
                {
                    result.Add(right[j]);
                    j++;
                }
            }

            while (i < left.Count) { result.Add(left[i]); i++; }
            while (j < right.Count) { result.Add(right[j]); j++; }

            return result;
        }
        
        private void SaveToCsv(string fileName = "/Users/slava/Downloads/SortingDemo_final/Tasks/output.csv")
        {
            if (_tableData == null || _tableData.Rows.Count == 0)
            {
                Log("Нет данных для сохранения.");
                return;
            }

            var sb = new StringBuilder();

            // Заголовки
            var columnNames = _tableData.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            sb.AppendLine(string.Join(";", columnNames));

            // Данные
            foreach (DataRow row in _tableData.Rows)
            {
                var fields = row.ItemArray.Select(f => f.ToString());
                sb.AppendLine(string.Join(";", fields));
            }

            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
            Log($"Данные сохранены в {fileName}");
        }

        private void SaveBtn_Click(object? sender, EventArgs e)
        {
            SaveToCsv();
        }
        
        private void Log(string text)
        {
            Dispatcher.UIThread.Post(() => LogBox.Text += text + "\n");
        }
    }
    
    

    public static class Extensions
    {
        // Конвертация списка DataRow обратно в DataTable
        public static DataTable CopyToDataTable(this List<DataRow> rows)
        {
            if (rows.Count == 0) return new DataTable();
            DataTable table = rows[0].Table.Clone();
            foreach (var r in rows)
                table.ImportRow(r);
            return table;
        }
    }
}

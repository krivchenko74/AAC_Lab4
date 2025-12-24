using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using OxyPlot;

namespace SortingDemo.Tasks;

public partial class Task3 : UserControl
{
    private string[] words = Array.Empty<string>();
    
    private readonly ObservableCollection<DataPoint> quickSortTimes = new();
    private readonly ObservableCollection<DataPoint> radixSortTimes = new();

    public ObservableCollection<DataPoint> QuickSortTimes => quickSortTimes;
    public ObservableCollection<DataPoint> RadixSortTimes => radixSortTimes;

    public Task3()
    {
        InitializeComponent();
        this.DataContext = this;
        Loaded += async (s, e) => await RunTask();
    }

    private async Task RunTask()
    {
        try
        {
            string inputPath = "/Users/slava/Downloads/SortingDemo_final/Tasks/Task3/Input.txt";
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"[ОШИБКА] Файл не найден: {inputPath}");
                return;
            }

            string text = await File.ReadAllTextAsync(inputPath);
            var allWords = Regex.Split(text, @"\W+")
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.ToLower())
                .ToArray();

            int maxWords = Math.Min(5000, allWords.Length);
            words = allWords.Take(maxWords).ToArray();

            await RunExperimentsAsync();
            await CountAndSaveFrequenciesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ИСКЛЮЧЕНИЕ] {ex.Message}");
        }
    }

    private async Task CountAndSaveFrequenciesAsync()
    {
        var sorted = words.ToArray();
        RadixSortLSС(sorted);

        var frequency = new List<(string Word, int Count)>();
        if (sorted.Length > 0)
        {
            string current = sorted[0];
            int count = 1;
            for (int i = 1; i < sorted.Length; i++)
            {
                if (sorted[i] == current)
                    count++;
                else
                {
                    frequency.Add((current, count));
                    current = sorted[i];
                    count = 1;
                }
            }
            frequency.Add((current, count));
        }
        
        string outputPath = "/Users/slava/Downloads/SortingDemo_final/Tasks/Task3/Output.txt";
        var sb = new StringBuilder();
        foreach (var (word, count) in frequency)
        {
            sb.AppendLine($"{word}: {count}");
        }
        await File.WriteAllTextAsync(outputPath, sb.ToString());
    }
    
    private async Task RunExperimentsAsync()
    {
        int[] sizes = { 100, 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000 };
        quickSortTimes.Clear();
        radixSortTimes.Clear();

        for(var size = 0; size < 40000; size += 50)
        {
            if (size > words.Length) break;

            var subset = words.Take(size).ToArray();

            double avgQuick = MeasureSortTime(() =>
            {
                var arr = subset.ToArray();
                QuickSortLomuto(arr, 0, arr.Length - 1);
            }, runs: 10);

            double avgRadix = MeasureSortTime(() =>
            {
                var arr = subset.ToArray();
                RadixSortLSС(arr);
            }, runs: 10);

            quickSortTimes.Add(new DataPoint(size, avgQuick));
            radixSortTimes.Add(new DataPoint(size, avgRadix));
        }

        // Обновляем график
        PlotControl.InvalidatePlot(true);
        await Task.Delay(1); // Даем UI время на отрисовку
    }

    private double MeasureSortTime(Action sortAction, int runs = 10)
    {
        var times = new System.Collections.Generic.List<double>();
        for (int i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            sortAction();
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        return times.Average();
    }

    // === QuickSort Lomuto ===
    private void QuickSortLomuto(string[] arr, int low, int high)
    {
        if (low < high)
        {
            int pi = Partition(arr, low, high);
            QuickSortLomuto(arr, low, pi - 1);
            QuickSortLomuto(arr, pi + 1, high);
        }
    }

    private int Partition(string[] arr, int low, int high)
    {
        string pivot = arr[high];
        int i = low - 1;
        for (int j = low; j < high; j++)
        {
            if (string.Compare(arr[j], pivot, StringComparison.Ordinal) <= 0)
            {
                i++;
                Swap(arr, i, j);
            }
        }
        Swap(arr, i + 1, high);
        return i + 1;
    }

    private void Swap(string[] arr, int i, int j)
    {
        (arr[i], arr[j]) = (arr[j], arr[i]);
    }
    
    private void RadixSortLSС(string[] arr)
    {
        if (arr.Length <= 1) return;

        int maxLen = arr.Max(s => s.Length);
        var temp = new string[arr.Length];

        for (int digit = maxLen - 1; digit >= 0; digit--)
        {
            var buckets = new System.Collections.Generic.List<string>[256];
            for (int i = 0; i < 256; i++)
                buckets[i] = new System.Collections.Generic.List<string>();

            foreach (var s in arr)
            {
                int index = digit < s.Length ? s[digit] : 0;
                buckets[index].Add(s);
            }

            int pos = 0;
            foreach (var bucket in buckets)
            {
                foreach (var s in bucket)
                    temp[pos++] = s;
            }

            Array.Copy(temp, arr, arr.Length);
        }
    }
}
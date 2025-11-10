using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SortingDemo.Algorithms;

namespace SortingDemo.Tasks
{
    public partial class Task1 : UserControl
    {
        private Random _rand = new();
        private List<int> _array = new();
        private Canvas? _visualizer;
        private TextBox? _logBox;
        private TextBlock? _explanation;
        private TextBlock? _legend;
        private NumericUpDown? _delayBox;
        private NumericUpDown? _sizeBox;
        private Button? _startBtn;
        private Button? _shuffleBtn;
        private ComboBox? _algoBox;

        private readonly List<IAlgorithm> _algorithms = new()
        {
            new BubbleSort(),
            new SelectSort(),
            new QuickSort(),
            new HeapSort()
        };
        
        // UI elements representing items
        private List<Border> _itemControls = new();
        // positions: index -> (x,y) on canvas
        private Dictionary<int, (double x, double y)> _positions = new();

        // State
        private HashSet<int> _sortedIndices = new();
        private HashSet<int> _activeIndices = new();
        private HashSet<int> _rangeIndices = new();


        public Task1()
        {
            InitializeComponent();
            _visualizer = this.FindControl<Canvas>("Visualizer");
            _logBox = this.FindControl<TextBox>("LogBox");
            _explanation = this.FindControl<TextBlock>("Explanation");
            _legend = this.FindControl<TextBlock>("Legend");
            _delayBox = this.FindControl<NumericUpDown>("DelayBox");
            _sizeBox = this.FindControl<NumericUpDown>("SizeBox");
            _startBtn = this.FindControl<Button>("StartBtn");
            _shuffleBtn = this.FindControl<Button>("ShuffleBtn");
            _algoBox = this.FindControl<ComboBox>("AlgorithmBox");

            _startBtn!.Click += async (_, __) => await StartSort();
            
            _shuffleBtn!.Click += (_, __) =>
            {
                GenerateArray((int)(_sizeBox?.Value ?? 2));
                BuildVisuals();
                Log("Array shuffled: " + '[' + string.Join(',', _array) + ']');
            };

            GenerateArray((int)(_sizeBox?.Value ?? 2));
            BuildVisuals();
            SetExplanation();
            SetLegend();
            _algoBox!.SelectionChanged += (_, __) =>
            {
                SetExplanation();
                SetLegend();
            };

            // rebuild visuals on window bounds change
            this.GetObservable(BoundsProperty).Subscribe(_ => BuildVisuals());
        }

        private void SetExplanation()
        {
            var txt = (_algoBox?.SelectedIndex) switch
            {
                0 => "Пузырьковая сортировка: проходим по списку и меняем соседние элементы, если они неправильно расположены. Повторяем, пока всё не станет в порядке.",
                1 => "Сортировка выбором: на каждом шаге ищем минимальный элемент в неотсортированной части массива и меняем его местами с первым элементом этой части. Повторяем, пока весь массив не станет отсортированным.",
                2 => "Быстрая сортировка: выбираем опорный элемент и разделяем массив на меньшие и большие относительно опорного, затем рекурсивно сортируем части.",
                3 => "Пирамидальная сортировка: строим кучу (heap) из элементов массива, затем последовательно извлекаем максимальный элемент из кучи и помещаем его в конец массива. Повторяем, пока все элементы не окажутся на своих местах.",
                _ => ""
            };
            if (_explanation != null)
            {
                Dispatcher.UIThread.Post(() => _explanation.Text = txt);
            }
        }
        
        private void SetLegend()
        {
            var txt = (_algoBox?.SelectedIndex) switch
            {
                0 => "Черный - не используется, Синий - сравнивается, Серый - уже отсортирован",
                1 => "Черный - не используется, Синий - сравнивается, Серый - уже отсортирован",
                2 => "Фиолетовый - используемый подмассив, Синий - опорный элемент, Серый - отсортированный",
                3 => "Фиолетовый - используемый подмассив, Синий - сравнивается, Серый - отсортированный",
                _ => "Черный - не используется, Синий - сравнивается, Серый - уже отсортирован"
            };
            if (_explanation != null)
            {
                Dispatcher.UIThread.Post(() => _legend.Text = txt);
            }
        }

        private void GenerateArray(int size)
        {
            if (size < 1) size = 1;
            _array = Enumerable.Range(1, size).OrderBy(_ => _rand.Next()).ToList();
            _sortedIndices.Clear();
            _activeIndices.Clear();
        }

        private void BuildVisuals()
        {
            if (_visualizer == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                _visualizer.Children.Clear();
                _itemControls.Clear();
                _positions.Clear();

                var width = Math.Max(600, this.Bounds.Width - 320);
                double square = Math.Clamp(40, 24, 80); // base square size; will scale by available width
                var n = _array.Count;

                var cols = Math.Max(1, (int)(width / (square + 8)));
                double spacing = 8;
                var startX = spacing;
                var startY = spacing;
                double x = startX, y = startY;
                var col = 0;
                for (int i = 0; i < n; i++)
                {
                    // create control
                    var border = new Border
                    {
                        Width = square,
                        Height = square,
                        CornerRadius = new CornerRadius(6),
                        Background = Brushes.Black,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        Child = new TextBlock
                        {
                            Text = _array[i].ToString(),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Foreground = Brushes.White,
                        }
                    };

                    // position
                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, y);
                    _visualizer.Children.Add(border);
                    _itemControls.Add(border);
                    _positions[i] = (x, y);

                    // move to next
                    col++;
                    if (col >= cols)
                    {
                        col = 0;
                        x = startX;
                        y += square + spacing;
                    }
                    else x += square + spacing;
                }

                RefreshColors(); // обновит цвета и тексты
            });
        }

        private void RefreshColors()
        {
            // Обновление UI через Dispatcher
            Dispatcher.UIThread.Post(() =>
            {
                for (int i = 0; i < _itemControls.Count; i++)
                {
                    var b = _itemControls[i];

                    // приоритет: sorted > active > range > default
                    if (_sortedIndices.Contains(i))
                    {
                        if (_rangeIndices.Contains(i))
                        {
                            b.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 120, 255)); // мягкий сиреневый
                            b.BorderThickness = new Thickness(3);
                        }
                        else
                        {
                            b.BorderThickness = new Thickness(0);
                        }
                        b.Background = Brushes.LightGray;
                        if (b.Child is TextBlock tb) tb.Foreground = Brushes.Black;
                    }
                    else if (_activeIndices.Contains(i))
                    {
                        if (_rangeIndices.Contains(i))
                        {
                            b.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 120, 255)); // мягкий сиреневый
                            b.BorderThickness = new Thickness(3);
                        }
                        else
                        {
                            b.BorderThickness = new Thickness(0);
                        }
                        b.Background = Brushes.DodgerBlue;
                        if (b.Child is TextBlock tb) tb.Foreground = Brushes.White;
                    }
                    else
                    {
                        if (_rangeIndices.Contains(i))
                        {
                            b.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 120, 255)); // мягкий сиреневый
                            b.BorderThickness = new Thickness(3);
                        }
                        else
                        {
                            b.BorderThickness = new Thickness(0);
                        }
                        b.Background = Brushes.Black;
                        if (b.Child is TextBlock tb) tb.Foreground = Brushes.White;
                    }
                }
            });
        }
        
        private async Task HighlightRange(int left, int right)
        {
            _rangeIndices.Clear();
            if (left >= 0 && right >= 0 && left < _array.Count && right < _array.Count)
            {
                for (var i = left; i <= right; i++)
                    _rangeIndices.Add(i);
            }
            RefreshColors();
            await Task.CompletedTask;
        }

        private void Log(string s)
        {
            if (_logBox == null) return;
            Dispatcher.UIThread.Post(() =>
            {
                _logBox.Text += s + "\n";
                _logBox.CaretIndex = _logBox.Text.Length;
            });
        }

        private async Task StartSort()
        {
            _startBtn!.IsEnabled = false;
            _shuffleBtn!.IsEnabled = false;
            int delay = (int)(_delayBox?.Value ?? 300);
            _sortedIndices.Clear();
            _activeIndices.Clear();
            RefreshColors();
            
            
            var algo = _algorithms[_algoBox!.SelectedIndex];
            Log($"Starting {algo.Name}");
            
            await algo.Sort(
                _array,
                async (i, j) => await ReportCompare(i, j, delay),
                async (i, j) => await ReportSwapAnimated(i, j, delay),
                async() => RefreshColors(),
                _sortedIndices,
                Log,
                async (l, r) => await HighlightRange(l, r)
            );
            // finalize: mark all sorted
            for (int i = 0; i < _array.Count; i++) _sortedIndices.Add(i);
            RefreshColors();
            _startBtn.IsEnabled = true;
            _shuffleBtn.IsEnabled = true;
        }

        // Helpers: compare and swap with logs and animations
        private async Task ReportCompare(int i, int j, int delay)
        {
            _activeIndices.Clear();
            if (i >= 0 && i < _array.Count) _activeIndices.Add(i);
            if (j >= 0 && j < _array.Count) _activeIndices.Add(j);
            RefreshColors();
            await Task.Delay(Math.Max(30, delay / 2));
        }

        private async Task ReportSwapAnimated(int i, int j, int delay)
        {
            if (_visualizer == null) return;
            if (i == j) return;
            var c1 = _itemControls[i];
            var c2 = _itemControls[j];
            var p1 = _positions[i];
            var p2 = _positions[j];

            var steps = Math.Max(3, delay / 16);
            var stepDelay = delay / (double)steps;
            
            for (var s = 1; s <= steps; s++)
            {
                var t = s / (double)steps;
                Canvas.SetLeft(c1, Lerp(p1.x, p2.x, t));
                Canvas.SetTop(c1, Lerp(p1.y, p2.y, t));
                Canvas.SetLeft(c2, Lerp(p2.x, p1.x, t));
                Canvas.SetTop(c2, Lerp(p2.y, p1.y, t));
                await Task.Delay((int)stepDelay);
            }
            
            Canvas.SetLeft(c1, p2.x);
            Canvas.SetTop(c1, p2.y);
            Canvas.SetLeft(c2, p1.x);
            Canvas.SetTop(c2, p1.y);
            
            (_itemControls[i], _itemControls[j]) = (_itemControls[j], _itemControls[i]);

            _activeIndices.Clear();
            RefreshColors();
        }


        private double Lerp(double a, double b, double t) => a + (b - a) * t;
        
    }
}

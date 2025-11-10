using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms;

public class HeapSort : IAlgorithm
{
    public string Name => "Пирамидальная сортировка (Heap Sort)";

    public async Task Sort(
        List<int> array,
        Func<int, int, Task> onCompare,
        Func<int, int, Task> onSwap,
        Func<Task> onRefresh,
        HashSet<int> sorted,
        Action<string> Log,
        Func<int, int, Task>? onHighlight)
    {
        int n = array.Count;
        if (n <= 1)
        {
            Log($"[HeapSort] Массив из {n} элемента(ов) — сортировка не требуется.");
            return;
        }

        Log($"[HeapSort] === ЗАПУСК ПИРАМИДАЛЬНОЙ СОРТИРОВКИ ===\n" +
            $"Исходный массив: [{string.Join(", ", array)}]\n" +
            $"Размер: {n}");

        // === ФАЗА 1: Построение максимальной кучи ===
        Log($"\nФАЗА 1: Построение максимальной кучи (heapify снизу вверх)");
        for (int i = n / 2 - 1; i >= 0; i--)
        {
            Log($"\nHeapify корня на индексе {i} (дети: {2*i+1}, {2*i+2})");
            await Heapify(array, n, i, onCompare, onSwap, onRefresh, onHighlight, Log, depth: 1);
        }
        Log($"Куча построена: [{string.Join(", ", array)}]");

        // === ФАЗА 2: Извлечение максимума ===
        Log($"\nФАЗА 2: Извлечение максимумов (сортировка)");
        for (int i = n - 1; i >= 0; i--)
        {
            Log($"\nШаг {n - i}: извлекаем максимум (корень) → позиция {i}");

            if (onHighlight != null)
            {
                Log($"Подсветка: корень [0] ↔ последний неотсортированный [{i}]");
                await onHighlight(0, i);
            }

            // Меняем корень с последним элементом
            Log($"Обмен: array[0]={array[0]} ↔ array[{i}]={array[i]}");
            (array[0], array[i]) = (array[i], array[0]);
            await onSwap(0, i);
            await onRefresh();

            // Помечаем позицию как отсортированную
            sorted.Add(i);
            Log($"Позиция {i} зафиксирована: array[{i}] = {array[i]}");

            // Восстанавливаем кучу на оставшейся части [0..i)
            if (i > 0)
            {
                Log($"Heapify корня [0] на подкуче размером {i}");
                await Heapify(array, i, 0, onCompare, onSwap, onRefresh, onHighlight, Log, depth: 1);
            }

            if (onHighlight != null)
                await onHighlight(-1, -1);
        }

        Log($"\n[HeapSort] === СОРТИРОВКА ЗАВЕРШЕНА ===\n" +
            $"Отсортированный массив: [{string.Join(", ", array)}]");
    }

    private async Task Heapify(
        List<int> array,
        int heapSize,
        int rootIndex,
        Func<int, int, Task> onCompare,
        Func<int, int, Task> onSwap,
        Func<Task> onRefresh,
        Func<int, int, Task>? onHighlight,
        Action<string> Log,
        int depth)
    {
        string indent = Indent(depth);
        int largest = rootIndex;
        int left = 2 * rootIndex + 1;
        int right = 2 * rootIndex + 2;

        Log($"{indent}Heapify(root={rootIndex}, heapSize={heapSize})");
        Log($"{indent}  Текущий узел: array[{rootIndex}] = {array[rootIndex]}");
        Log($"{indent}  Дети: left={left}, right={right} (largest={largest})");

        // Сравнение с левым ребёнком
        if (left < heapSize)
        {
            Log($"{indent}  Сравниваем left[{left}]={array[left]} с largest[{largest}]={array[largest]}");
            await onCompare(left, largest);
            if (array[left] > array[largest])
            {
                Log($"{indent}  left больше → largest = {left}");
                largest = left;
            }
            else
            {
                Log($"{indent}  left не больше → largest остаётся {largest}");
            }
        }
        else
        {
            Log($"{indent}  left={left} ≥ heapSize → нет левого ребёнка");
        }

        // Сравнение с правым ребёнком
        if (right < heapSize)
        {
            Log($"{indent}  Сравниваем right[{right}]={array[right]} с largest[{largest}]={array[largest]}");
            await onCompare(right, largest);
            if (array[right] > array[largest])
            {
                Log($"{indent}  right больше → largest = {right}");
                largest = right;
            }
            else
            {
                Log($"{indent}  right не больше → largest остаётся {largest}");
            }
        }
        else
        {
            Log($"{indent}  right={right} ≥ heapSize → нет правого ребёнка");
        }

        // Если нужно — обмен и рекурсия
        if (largest != rootIndex)
        {
            Log($"{indent}  Нарушение кучи! Обмен: array[{rootIndex}] ↔ array[{largest}]");
            (array[rootIndex], array[largest]) = (array[largest], array[rootIndex]);
            await onSwap(rootIndex, largest);
            await onRefresh();

            Log($"{indent}  После обмена: array[{rootIndex}]={array[rootIndex]}, array[{largest}]={array[largest]}");
            Log($"{indent}  Рекурсивный Heapify на индексе {largest} (heapSize={heapSize})");
            await Heapify(array, heapSize, largest, onCompare, onSwap, onRefresh, onHighlight, Log, depth + 1);
        }
        else
        {
            Log($"{indent}  Куча в порядке — обмен не требуется");
        }
    }

    // === Вспомогательные методы ===
    private static string Indent(int depth) => new string(' ', depth * 2);
}
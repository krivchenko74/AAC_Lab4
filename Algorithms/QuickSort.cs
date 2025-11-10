using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms
{
    public class QuickSort : IAlgorithm
    {
        public string Name => "Быстрая сортировка (Lomuto)";

        public async Task Sort(
            List<int> array,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            HashSet<int> sorted,
            Action<string> Log,
            Func<int, int, Task>? onHighlight)
        {
            if (array.Count <= 1)
            {
                Log($"[QuickSort] Массив из {array.Count} элемента(ов) — сортировка не требуется.");
                return;
            }

            Log($"[QuickSort] === ЗАПУСК СОРТИРОВКИ ===\n" +
                $"Исходный массив: [{string.Join(", ", array)}]\n" +
                $"Размер: {array.Count}, границы: [0..{array.Count - 1}]");

            await QuickSortLomuto(array, 0, array.Count - 1, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth: 0);

            Log($"[QuickSort] === СОРТИРОВКА ЗАВЕРШЕНА ===\n" +
                $"Отсортированный массив: [{string.Join(", ", array)}]");

            // Помечаем всё как отсортированное
            for (int i = 0; i < array.Count; i++)
                sorted.Add(i);
            await onRefresh();
        }

        private async Task QuickSortLomuto(
            List<int> array,
            int left,
            int right,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            HashSet<int> sorted,
            Func<int, int, Task>? onHighlight,
            Action<string> Log,
            int depth)
        {
            if (left >= right)
            {
                Log($"{Indent(depth)}[Lomuto] База рекурсии: left={left}, right={right} → ничего не делаем.");
                return;
            }

            string indent = Indent(depth);
            string subarray = ArraySlice(array, left, right);
            Log($"{indent}┌── РАЗБИЕНИЕ ПОДМАССИВА [{left}..{right}] (размер: {right - left + 1})");
            Log($"{indent}│ Подмассив: [{subarray}]");

            // Подсвечиваем текущий подмассив
            if (onHighlight != null)
            {
                Log($"{indent}│ Подсветка: [{left}..{right}]");
                await onHighlight(left, right);
            }

            // === Lomuto: pivot — последний элемент ===
            int pivotIndex = right;
            int pivot = array[pivotIndex];
            Log($"{indent}│ Pivot: array[{pivotIndex}] = {pivot} (в конце подмассива)");

            int i = left - 1;
            Log($"{indent}│ i = {i} (указатель на последний меньший элемент)");

            // Проходим по подмассиву от left до right-1
            for (int j = left; j < right; j++)
            {
                Log($"{indent}│   → j={j}: сравниваем array[{j}]={array[j]} с pivot={pivot}");
                await onCompare(j, pivotIndex);

                if (array[j] <= pivot)
                {
                    i++;
                    Log($"{indent}│   ✓ array[{j}] <= {pivot} → i++ → i={i}");

                    if (i != j)
                    {
                        Log($"{indent}│   Обмен: array[{i}] ↔ array[{j}]");
                        (array[i], array[j]) = (array[j], array[i]);
                        await onSwap(i, j);
                    }
                    else
                    {
                        Log($"{indent}│   i == j → обмен не нужен");
                    }
                    await onRefresh();
                }
                else
                {
                    Log($"{indent}│   ✗ array[{j}] > {pivot} → пропускаем");
                }
            }

            // Финальный обмен: ставим pivot на своё место
            i++;
            Log($"{indent}│ Финальный обмен: pivot (array[{right}]) → позиция i={i}");
            if (i != right)
            {
                Log($"{indent}│   Обмен: array[{i}] ↔ array[{right}]");
                (array[i], array[right]) = (array[right], array[i]);
                await onSwap(i, right);
            }
            else
            {
                Log($"{indent}│   i == right → pivot уже на месте");
            }
            await onRefresh();

            int partitionIndex = i;
            Log($"{indent}│ Разбиение завершено. Pivot теперь на индексе: {partitionIndex}");
            Log($"{indent}│ Левая часть:  [{left}..{partitionIndex-1}]");
            Log($"{indent}│ Правая часть: [{partitionIndex+1}..{right}]");

            // Снимаем подсветку
            if (onHighlight != null)
            {
                Log($"{indent}│ Снимаем подсветку");
                await onHighlight(-1, -1);
            }

            Log($"{indent}└── Рекурсия: левая часть → [{left}..{partitionIndex-1}]");
            await QuickSortLomuto(array, left, partitionIndex - 1, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth + 1);

            Log($"{indent}└── Рекурсия: правая часть → [{partitionIndex+1}..{right}]");
            await QuickSortLomuto(array, partitionIndex + 1, right, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth + 1);

            Log($"{indent}✔ Подмассив [{left}..{right}] полностью отсортирован.");
        }

        // === Вспомогательные методы ===

        private static string Indent(int depth) => new string(' ', depth * 2);

        private static string ArraySlice(List<int> array, int start, int end)
        {
            if (start > end) return "<пусто>";
            var slice = new List<int>();
            for (int i = start; i <= end && i < array.Count; i++)
                slice.Add(array[i]);
            return string.Join(", ", slice);
        }
    }
}
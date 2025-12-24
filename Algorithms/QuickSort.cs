using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms
{
    public class QuickSort : IAlgorithm
    {
        public string Name => "Быстрая сортировка (Hoare)";

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

            Log($"[QuickSort] === ЗАПУСК СОРТИРОВКИ (Hoare) ===\n" +
                $"Исходный массив: [{string.Join(", ", array)}]\n" +
                $"Размер: {array.Count}, границы: [0..{array.Count - 1}]");

            await QuickSortHoare(array, 0, array.Count - 1, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth: 0);

            Log($"[QuickSort] === СОРТИРОВКА ЗАВЕРШЕНА ===\n" +
                $"Отсортированный массив: [{string.Join(", ", array)}]");

            // Помечаем всё как отсортированное
            for (int i = 0; i < array.Count; i++)
                sorted.Add(i);
            await onRefresh();
        }

        private async Task QuickSortHoare(
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
                Log($"{Indent(depth)}[Hoare] База рекурсии: left={left}, right={right} → ничего не делаем.");
                return;
            }

            string indent = Indent(depth);
            string subarray = ArraySlice(array, left, right);
            Log($"{indent}┌── РАЗБИЕНИЕ HOARE [{left}..{right}] (размер: {right - left + 1})");
            Log($"{indent}│ Подмассив: [{subarray}]");

            // Подсвечиваем текущий подмассив
            if (onHighlight != null)
            {
                Log($"{indent}│ Подсветка: [{left}..{right}]");
                await onHighlight(left, right);
            }

            // === Hoare: выбор опорного элемента по центру ===
            int pivotIndex = (left + right) / 2;
            int pivot = array[pivotIndex];
            Log($"{indent}│ Pivot: array[{pivotIndex}] = {pivot} (в центре подмассива)");

            int i = left;
            int j = right;
            Log($"{indent}│ Инициализация: i={i} (идёт слева), j={right} (идёт справа)");

            while (true)
            {
                Log($"{indent}│   Цикл разбиения:");

                // Двигаем i вправо, пока не найдём элемент >= pivot
                while (array[i] < pivot)
                {
                    Log($"{indent}│   → i={i}: array[{i}]={array[i]} < {pivot} → двигаем i вправо");
                    await onCompare(i, pivotIndex);
                    i++;
                    if (i > j) break;
                }
                if (i <= j && array[i] >= pivot)
                {
                    Log($"{indent}│   → i={i}: array[{i}]={array[i]} >= {pivot} → остановка i");
                    await onCompare(i, pivotIndex);
                }

                // Двигаем j влево, пока не найдём элемент <= pivot
                while (array[j] > pivot)
                {
                    Log($"{indent}│   → j={j}: array[{j}]={array[j]} > {pivot} → двигаем j влево");
                    await onCompare(j, pivotIndex);
                    j--;
                    if (i > j) break;
                }
                if (i <= j && array[j] <= pivot)
                {
                    Log($"{indent}│   → j={j}: array[{j}]={array[j]} <= {pivot} → остановка j");
                    await onCompare(j, pivotIndex);
                }

                // Проверяем условие завершения
                if (i >= j)
                {
                    Log($"{indent}│   Условие завершения: i={i} >= j={j}");
                    break;
                }

                // Меняем элементы местами
                Log($"{indent}│   Обмен: i={i} ↔ j={j} (array[{i}]={array[i]}, array[{j}]={array[j]})");
                (array[i], array[j]) = (array[j], array[i]);
                await onSwap(i, j);
                await onRefresh();

                // Сдвигаем указатели после обмена
                i++;
                j--;
                Log($"{indent}│   После обмена: i++={i}, j--={j}");
            }

            // Точка разбиения Hoare
            int partitionIndex = j;
            Log($"{indent}│ Разбиение Hoare завершено. Точка разбиения: j={partitionIndex}");
            Log($"{indent}│ Левая часть:  [{left}..{partitionIndex}]");
            Log($"{indent}│ Правая часть: [{partitionIndex + 1}..{right}]");

            // Снимаем подсветку
            if (onHighlight != null)
            {
                Log($"{indent}│ Снимаем подсветку");
                await onHighlight(-1, -1);
            }

            // Рекурсивно сортируем обе части
            Log($"{indent}└── Рекурсия: левая часть → [{left}..{partitionIndex}]");
            await QuickSortHoare(array, left, partitionIndex, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth + 1);

            Log($"{indent}└── Рекурсия: правая часть → [{partitionIndex + 1}..{right}]");
            await QuickSortHoare(array, partitionIndex + 1, right, onCompare, onSwap, onRefresh, sorted, onHighlight, Log, depth + 1);

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
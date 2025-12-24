using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms;

public class SelectSort : IAlgorithm
{
    public string Name => "Сортировка выбором (Selection Sort)";

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
            Log($"[SelectSort] Массив из {n} элемента(ов) — сортировка не требуется.");
            return;
        }

        Log($"[SelectSort] === ЗАПУСК СОРТИРОВКИ ===\n" +
            $"Исходный массив: [{string.Join(", ", array)}]\n" +
            $"Размер: {n}");

        for (int i = 0; i < n - 1; i++)
        {
            Log($"\n┌── ШАГ {i + 1}: фиксируем позицию {i}");
            Log($"│ Текущий массив: [{string.Join(", ", array)}]");

            // Подсвечиваем текущую позицию и неотсортированную часть
            if (onHighlight != null)
            {
                Log($"│ Подсветка: текущая позиция [{i}], поиск в [{i + 1}..{n - 1}]");
                await onHighlight(i, n - 1);
            }

            int minIndex = i;
            Log($"│ Ищем минимум в подмассиве [{i + 1}..{n - 1}]");
            Log($"│ Начинаем с предположения: minIndex = {minIndex} (array[{minIndex}] = {array[minIndex]})");

            for (int j = i + 1; j < n; j++)
            {
                Log($"│   → j={j}: сравниваем array[{minIndex}]={array[minIndex]} и array[{j}]={array[j]}");
                await onCompare(minIndex, j);

                if (array[minIndex] > array[j])
                {
                    Log($"│   Found smaller: array[{j}] ({array[j]}) < array[{minIndex}] ({array[minIndex]}) → minIndex = {j}");
                    minIndex = j;
                }
                else
                {
                    Log($"│   No change: array[{j}] ({array[j]}) >= array[{minIndex}] ({array[minIndex]}) → minIndex остаётся {minIndex}");
                }
            }

            // Если нашли меньший — меняем
            if (minIndex != i)
            {
                Log($"│ Final minimum: array[{minIndex}] = {array[minIndex]} < array[{i}] = {array[i]}");
                Log($"│   Обмен: array[{i}] ↔ array[{minIndex}]");

                (array[i], array[minIndex]) = (array[minIndex], array[i]);
                await onSwap(i, minIndex);
                await onRefresh();
            }
            else
            {
                Log($"│ No change: array[{i}] уже минимальный в подмассиве");
            }

            // Помечаем позицию как отсортированную
            sorted.Add(i);
            Log($"│ Position {i} fixed: array[{i}] = {array[i]} теперь на своём месте.");

            // Снимаем подсветку
            if (onHighlight != null)
            {
                Log($"│ Подсветка снята");
                await onHighlight(-1, -1);
            }

            Log($"└── Шаг {i + 1} завершён. Массив: [{string.Join(", ", array)}]");
        }

        // Последний элемент автоматически отсортирован
        sorted.Add(n - 1);
        Log($"\n[SelectSort] === СОРТИРОВКА ЗАВЕРШЕНА ===\n" +
            $"Отсортированный массив: [{string.Join(", ", array)}]");
    }
}
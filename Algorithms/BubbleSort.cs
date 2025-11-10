using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms;

public class BubbleSort : IAlgorithm
{
    public string Name => "Пузырьковая сортировка (Bubble Sort)";

    public async Task Sort(
        List<int> array,
        Func<int, int, Task> onCompare,
        Func<int, int, Task> onSwap,
        Func<Task> onRefresh,
        HashSet<int> sorted,
        Action<string> Log,
        Func<int, int, Task>? onHighlight)
    {
        if (array == null || array.Count <= 1)
        {
            Log($"[BubbleSort] Массив из {(array?.Count ?? 0)} элемента(ов) — сортировка не требуется.");
            return;
        }

        int n = array.Count;
        Log($"[BubbleSort] === ЗАПУСК СОРТИРОВКИ ===\n" +
            $"Исходный массив: [{string.Join(", ", array)}]\n" +
            $"Размер: {n}");

        bool anySwapsInPass = false;

        for (int i = 0; i < n - 1; i++)
        {
            bool swapped = false;
            Log($"\n┌── ПРОХОД {i + 1}: всплывание максимума к позиции {n - 1 - i}");
            Log($"│ Текущий массив: [{string.Join(", ", array)}]");

            // Подсвечиваем неотсортированную часть
            if (onHighlight != null)
            {
                Log($"│ Подсветка: сравнения в [0..{n - 2 - i}]");
                await onHighlight(0, n - 2 - i);
            }

            Log($"│ Сравниваем соседние пары: j от 0 до {n - 2 - i}");

            for (int j = 0; j < n - 1 - i; j++)
            {
                Log($"│   → j={j}: сравниваем array[{j}]={array[j]} и array[{j + 1}]={array[j + 1]}");
                await onCompare(j, j + 1);

                if (array[j] > array[j + 1])
                {
                    Log($"│   Found inversion: {array[j]} > {array[j + 1]} → ОБМЕН");
                    Log($"│     Обмениваем: array[{j}] ↔ array[{j + 1}]");

                    (array[j], array[j + 1]) = (array[j + 1], array[j]);
                    await onSwap(j, j + 1);

                    swapped = true;
                    anySwapsInPass = true;

                    Log($"│     После обмена: array[{j}]={array[j]}, array[{j + 1}]={array[j + 1]}");
                    await onRefresh();
                }
                else
                {
                    Log($"│   No inversion: {array[j]} ≤ {array[j + 1]} → пропуск");
                }
            }

            // Фиксируем последний элемент прохода
            int fixedIndex = n - 1 - i;
            sorted.Add(fixedIndex);
            Log($"│ Position {fixedIndex} fixed: array[{fixedIndex}] = {array[fixedIndex]} теперь на своём месте.");

            // Снимаем подсветку
            if (onHighlight != null)
            {
                Log($"│ Подсветка снята");
                await onHighlight(-1, -1);
            }

            if (!swapped)
            {
                Log($"│ No swaps in pass — массив уже отсортирован!");
                Log($"└── РАННИЙ ВЫХОД после прохода {i + 1}");
                break;
            }
            else
            {
                Log($"│ Swaps occurred — продолжаем следующий проход");
                Log($"└── Проход {i + 1} завершён. Массив: [{string.Join(", ", array)}]");
            }
        }

        // Если не было ни одного обмена за все проходы — всё было отсортировано
        if (!anySwapsInPass)
        {
            Log($"[BubbleSort] Массив был уже отсортирован — ни одного обмена не произошло.");
        }

        Log($"\n[BubbleSort] === СОРТИРОВКА ЗАВЕРШЕНА ===\n" +
            $"Отсортированный массив: [{string.Join(", ", array)}]");
    }
}
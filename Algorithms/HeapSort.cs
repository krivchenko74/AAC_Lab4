using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms
{
    public class HeapSort : IAlgorithm
    {
        public string Name => "Пирамидальная сортировка";

        public async Task Sort(
            List<int> array,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            HashSet<int> sorted,
            Func<int, int, Task>? onHighlight)
        {
            var n = array.Count;

            // Построение максимальной кучи
            for (var i = n / 2 - 1; i >= 0; i--)
                await Heapify(array, n, i, onCompare, onSwap, onRefresh, onHighlight);

            // Извлечение элементов из кучи
            for (var i = n - 1; i >= 0; i--)
            {
                // Меняем корень (максимальный элемент) с последним элементом
                (array[0], array[i]) = (array[i], array[0]);
                await onSwap(0, i);

                sorted.Add(i); // последний элемент теперь на месте
                await onRefresh();

                // Восстанавливаем кучу для оставшейся части
                await Heapify(array, i, 0, onCompare, onSwap, onRefresh, onHighlight);
            }

            // Помечаем все элементы как отсортированные
            for (var i = 0; i < n; i++)
                sorted.Add(i);
            await onRefresh();

            // Сброс подсветки
            if (onHighlight != null)
                await onHighlight(-1, -1);
        }

        private async Task Heapify(
            List<int> array,
            int heapSize,
            int rootIndex,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            Func<int, int, Task>? onHighlight)
        {
            var largest = rootIndex;
            var left = 2 * rootIndex + 1;
            var right = 2 * rootIndex + 2;

            if (left < heapSize)
            {
                await onCompare(left, largest);
                if (array[left] > array[largest])
                    largest = left;
            }

            if (right < heapSize)
            {
                await onCompare(right, largest);
                if (array[right] > array[largest])
                    largest = right;
            }

            if (largest != rootIndex)
            {
                (array[rootIndex], array[largest]) = (array[largest], array[rootIndex]);
                await onSwap(rootIndex, largest);
                await onRefresh();
                
                await Heapify(array, heapSize, largest, onCompare, onSwap, onRefresh, onHighlight);
            }
        }
    }
}

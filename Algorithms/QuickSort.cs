using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms
{
    public class QuickSort : IAlgorithm
    {
        public string Name => "Быстрая сортировка";

        public async Task Sort(
            List<int> array,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            HashSet<int> sorted,
            Func<int, int, Task>? onHighlight)
        {
            await QuickSortRecursive(array, 0, array.Count - 1, onCompare, onSwap, onRefresh, sorted, onHighlight);
            // после завершения помечаем все как отсортированные
            for (var i = 0; i < array.Count; i++)
                sorted.Add(i);
            await onRefresh();
        }

        private async Task QuickSortRecursive(
            List<int> array,
            int left,
            int right,
            Func<int, int, Task> onCompare,
            Func<int, int, Task> onSwap,
            Func<Task> onRefresh,
            HashSet<int> sorted,
            Func<int, int, Task>? onHighlight)
        {
            if (left >= right)
                return;
            
            if (onHighlight != null)
                await onHighlight(left, right);
            
            var pivotIndex = left + (right - left) / 2;
            var pivot = array[pivotIndex];
            int i = left, j = right;
            
            
            while (i <= j)
            {
                await onCompare(pivotIndex, pivotIndex);
                while (array[i] < pivot)
                {
                    i++;
                }
                while (array[j] > pivot)
                {
                    j--;
                }

                if (i <= j)
                {
                    if (i != j)
                    {
                        (array[i], array[j]) = (array[j], array[i]);
                        await onSwap(i, j);
                    }
                    i++;
                    j--;
                    await onRefresh();
                }
            }
            
            if (left < j)
                await QuickSortRecursive(array, left, j, onCompare, onSwap, onRefresh, sorted, onHighlight);
            if (i < right)
                await QuickSortRecursive(array, i, right, onCompare, onSwap, onRefresh, sorted, onHighlight);
            
            if (onHighlight != null)
                await onHighlight(-1, -1);
        }
    }
}

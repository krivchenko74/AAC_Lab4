namespace SortingDemo.Algorithms;

public class BubbleSort: IAlgorithm
{
    public string Name => "Bubble Sort";
    public async Task Sort(List<int> _array, Func<int, int, Task> onCompare, Func<int, int, Task> onSwap, Func<Task> onRefresh, HashSet<int> sorted, Func<int, int, Task>? onHighlight)
    {
        if (_array == null || _array.Count <= 1) return;

        var n = _array.Count;
        for (var i = 0; i < n - 1; i++)
        {
            var swapped = false;
            for (int j = 0; j < n - 1 - i; j++)
            {
                await onCompare(j, j + 1);
                if (_array[j] > _array[j + 1])
                {
                    await onSwap(j, j + 1);
                    
                    (_array[j], _array[j + 1]) = (_array[j + 1], _array[j]);
                    
                    swapped = true;
                }
            }
            
            sorted.Add(n - 1 - i);
            await onRefresh();
            if (!swapped) break;
        }
    }
}
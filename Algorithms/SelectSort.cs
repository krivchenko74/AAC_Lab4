namespace SortingDemo.Algorithms;

public class SelectSort: IAlgorithm
{
    public string Name => "Select Sort";

    public async Task Sort(List<int> array, Func<int, int, Task> onCompare,
        Func<int, int, Task> onSwap,
        Func<Task> onRefresh,
        HashSet<int> sorted,
        Func<int, int, Task>? onHighlight)
    {
        var n = array.Count;
        int j;
        for (var i = 0; i < n - 1; i++)
        {
            for (j = i + 1; j < n; j++)
            {
                await onCompare(i,j);
                if (array[i] > array[j])
                {
                    (array[i], array[j]) = (array[j], array[i]);
                    await onSwap(i,j);
                }
            }
            sorted.Add(i);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SortingDemo.Algorithms;

public interface IAlgorithm
{
    string Name { get; }

    Task Sort(
        List<int> array,
        Func<int, int, Task> onCompare,
        Func<int, int, Task> onSwap,
        Func<Task> onRefresh,
        HashSet<int> sorted,
        Func<int, int, Task>? onHighlight = null 
        );
}
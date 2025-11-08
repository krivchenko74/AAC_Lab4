SortingDemo_final - Avalonia sorting visualization (squares) - example

Features:
- 4 algorithms: Bubble (with early exit), Insertion, Quick, Heap
- Visualization: squares with numbers, adaptive rows, animated swaps (movement duration depends on Delay)
- Colors: black = not processed, blue = active, light gray = sorted
- Logs each compare and swap in the right-side log window

Build & Run (macOS Apple Silicon / any platform with .NET SDK):
1. Install .NET 7+ SDK.
2. From the project folder run:
   dotnet restore
   dotnet run

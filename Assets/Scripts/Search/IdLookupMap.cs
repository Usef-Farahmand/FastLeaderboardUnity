using Unity.Collections;

public struct IdLookupMap : System.IDisposable
{
    private NativeParallelHashMap<int, int> _map;

    public IdLookupMap(int capacity, Allocator allocator)
    {
        _map = new NativeParallelHashMap<int, int>(
            capacity,
            allocator
        );
    }

    public NativeParallelHashMap<int, int>.ParallelWriter AsParallelWriter()
    {
        return _map.AsParallelWriter();
    }

    public bool TryGetIndex(int id, out int index)
    {
        return _map.TryGetValue(id, out index);
    }

    public bool Contains(int id)
    {
        return _map.ContainsKey(id);
    }

    public void Dispose()
    {
        if (_map.IsCreated)
            _map.Dispose();
    }
}
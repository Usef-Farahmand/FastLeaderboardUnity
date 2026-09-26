using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct IdSearchJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<LeaderboardEntry> Entries;
    [ReadOnly] public int Query;

    [WriteOnly]
    public NativeList<int>.ParallelWriter Results;

    public void Execute(int index)
    {
        int entryId = Entries[index].Id;

        if (IsPrefixMatch(entryId, Query))
        {
            Results.AddNoResize(index);
        }
    }

    private static bool IsPrefixMatch(int number, int prefix)
    {
        if (prefix <= 0 || number <= 0)
            return false;

        while (number > prefix)
        {
            number /= 10;
        }

        return number == prefix;
    }
}

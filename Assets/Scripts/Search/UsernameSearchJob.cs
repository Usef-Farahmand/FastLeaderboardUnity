using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct UsernameSearchJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<LeaderboardEntry> Entries;
    [ReadOnly] public FixedString64Bytes Query;
    [WriteOnly] public NativeList<int>.ParallelWriter Results;

    public void Execute(int index)
    {
        if (StartsWithIgnoreCase(Entries[index].Username, Query))
        {
            Results.AddNoResize(index);
        }
    }

    private static bool StartsWithIgnoreCase(FixedString64Bytes username, FixedString64Bytes query)
    {
        if (query.Length == 0)
            return true;

        if (query.Length > username.Length)
            return false;

        for (int i = 0; i < query.Length; i++)
        {
            byte usernameByte = username[i];
            byte queryByte = query[i];

            if (ToLowerAscii(usernameByte) != ToLowerAscii(queryByte))
                return false;
        }

        return true;
    }

    private static byte ToLowerAscii(byte value)
    {
        if (value >= (byte)'A' && value <= (byte)'Z')
            return (byte)(value + ('a' - 'A'));

        return value;
    }
}

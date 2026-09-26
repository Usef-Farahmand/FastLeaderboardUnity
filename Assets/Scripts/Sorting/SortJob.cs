using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct SortJob : IJob
{
    public NativeArray<LeaderboardEntry> Entries;

    public void Execute()
    {
        Entries.Sort(new ScoreComparer());
    }
}

struct ScoreComparer : IComparer<LeaderboardEntry>
{
    public int Compare(LeaderboardEntry x, LeaderboardEntry y)
    {
        return y.Score.CompareTo(x.Score);
    }
}

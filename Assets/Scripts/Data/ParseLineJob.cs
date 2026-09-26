using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct ParseLineJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte> FileBytes;
    [ReadOnly] public NativeArray<int> LineStartOffsets;
    [ReadOnly] public NativeArray<int> LineLengths;

    [WriteOnly] public NativeArray<LeaderboardEntry> Output;

    public NativeParallelHashMap<int, int>.ParallelWriter IdToIndex;

    public void Execute(int index)
    {
        // Skip CSV header.
        int lineIndex = index + 1;

        int start = LineStartOffsets[lineIndex];
        int length = LineLengths[lineIndex];
        int end = start + length;

        int field = 0;
        int fieldStart = start;

        int id = 0;
        FixedString64Bytes username = default;
        long score = 0;

        for (int i = start; i <= end; i++)
        {
            bool isFieldEnd =
                i == end ||
                FileBytes[i] == (byte)',';

            if (!isFieldEnd)
                continue;

            int fieldEnd = i;

            switch (field)
            {
                case 0:
                    id = ParseInt(fieldStart, fieldEnd);
                    break;

                case 1:
                    username = ParseUsername(fieldStart, fieldEnd);
                    break;

                case 2:
                    score = ParseLong(fieldStart, fieldEnd);
                    break;
            }

            field++;
            fieldStart = i + 1;
        }

        Output[index] = new LeaderboardEntry
        {
            Id = id,
            Username = username,
            Score = score,
            OriginalIndex = index
        };

        IdToIndex.TryAdd(id, index);
    }

    private int ParseInt(int start, int end)
    {
        int value = 0;

        for (int i = start; i < end; i++)
        {
            value = value * 10 + (FileBytes[i] - (byte)'0');
        }

        return value;
    }

    private long ParseLong(int start, int end)
    {
        long value = 0;

        for (int i = start; i < end; i++)
        {
            value = value * 10 + (FileBytes[i] - (byte)'0');
        }

        return value;
    }

    private FixedString64Bytes ParseUsername(int start, int end)
    {
        FixedString64Bytes value = default;

        for (int i = start; i < end; i++)
        {
            value.Append((char)FileBytes[i]);
        }

        return value;
    }
}

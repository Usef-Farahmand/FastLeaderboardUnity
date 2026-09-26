using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct FindLineOffsetsJob : IJob
{
    [ReadOnly]
    public NativeArray<byte> FileBytes;

    public NativeList<int> LineStartOffsets;
    public NativeList<int> LineLengths;

    public void Execute()
    {
        int lineStart = 0;

        for (int i = 0; i < FileBytes.Length; i++)
        {
            if (FileBytes[i] != (byte)'\n')
                continue;

            int lineLength = i - lineStart;

            // Handle Windows CRLF.
            if (lineLength > 0 && FileBytes[i - 1] == (byte)'\r')
                lineLength--;

            LineStartOffsets.Add(lineStart);
            LineLengths.Add(lineLength);

            lineStart = i + 1;
        }

        // Handle the last line if the file does not end with '\n'.
        if (lineStart < FileBytes.Length)
        {
            LineStartOffsets.Add(lineStart);
            LineLengths.Add(FileBytes.Length - lineStart);
        }
    }
}

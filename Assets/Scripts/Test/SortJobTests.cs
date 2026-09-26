using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class SortJobTests : MonoBehaviour
{
    [SerializeField]
    private string path;

    [SerializeField, Min(0)]
    private int MaxPreviewCount = 100;

    private readonly FileReader fileReader = new();

    [ContextMenu("Load And Sort")]
    public async void LoadAndSort()
    {
        NativeArray<byte> fileBytes = default;
        NativeList<int> lineStartOffsets = default;
        NativeList<int> lineLengths = default;
        NativeArray<LeaderboardEntry> entries = default;

        try
        {
            // ---------------------------------
            // 1. Read CSV
            // ---------------------------------

            fileBytes = await fileReader.ReadAsync(path);

            Debug.Log($"CSV loaded: {fileBytes.Length:N0} bytes");


            // ---------------------------------
            // 2. Find line offsets
            // ---------------------------------

            lineStartOffsets = new NativeList<int>(Allocator.TempJob);

            lineLengths = new NativeList<int>(Allocator.TempJob);

            var findLinesJob = new FindLineOffsetsJob
            {
                FileBytes = fileBytes,
                LineStartOffsets = lineStartOffsets,
                LineLengths = lineLengths
            };

            JobHandle findLinesHandle = findLinesJob.Schedule();

            findLinesHandle.Complete();

            Debug.Log($"Lines found: {lineStartOffsets.Length:N0}");


            // ---------------------------------
            // 3. Create output array
            // ---------------------------------

            int recordCount = lineStartOffsets.Length - 1;

            entries = new NativeArray<LeaderboardEntry>(recordCount, Allocator.TempJob);


            // ---------------------------------
            // 4. Parse CSV
            // ---------------------------------

            var parseJob = new ParseLineJob
            {
                FileBytes = fileBytes,
                LineStartOffsets = lineStartOffsets.AsArray(),
                LineLengths = lineLengths.AsArray(),
                Output = entries
            };

            JobHandle parseHandle = parseJob.Schedule(recordCount, 64);

            parseHandle.Complete();

            Debug.Log($"Parsed records: {entries.Length:N0}");


            // ---------------------------------
            // 5. Sort
            // ---------------------------------

            var sortJob = new SortJob
            {
                Entries = entries
            };

            JobHandle sortHandle = sortJob.Schedule();

            // Wait only when the sorted data is needed.
            sortHandle.Complete();


            // ---------------------------------
            // 6. Log sorted result
            // ---------------------------------

            int previewCount = Mathf.Min(
                MaxPreviewCount,
                entries.Length
            );

            Debug.Log($"--- Sorted Leaderboard (Top {previewCount:N0}) ---");

            for (int i = 0; i < previewCount; i++)
            {
                LeaderboardEntry entry = entries[i];

                Debug.Log(
                    $"[{i + 1}] " +
                    $"ID: {entry.Id}, " +
                    $"Username: {entry.Username}, " +
                    $"Score: {entry.Score}, " +
                    $"OriginalIndex: {entry.OriginalIndex}"
                );
            }

            Debug.Log($"--- End Sorted Leaderboard ---");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            // ---------------------------------
            // Dispose native containers
            // ---------------------------------

            if (entries.IsCreated)
                entries.Dispose();

            if (lineStartOffsets.IsCreated)
                lineStartOffsets.Dispose();

            if (lineLengths.IsCreated)
                lineLengths.Dispose();

            if (fileBytes.IsCreated)
                fileBytes.Dispose();
        }
    }
}
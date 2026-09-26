using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class IdLookupMapTests : MonoBehaviour
{
    [SerializeField]
    private string path;

    [SerializeField]
    [Min(0)]
    private int MaxPreviewCount = 100;

    [SerializeField]
    private int LookupPlayerId;

    [ContextMenu("Load, Parse, Sort And Test Lookup")]
    public async void LoadParseSortAndTestLookup()
    {
        NativeArray<byte> fileBytes = default;
        NativeList<int> lineStartOffsets = default;
        NativeList<int> lineLengths = default;
        NativeArray<LeaderboardEntry> entries = default;

        IdLookupMap idLookupMap = default;

        try
        {
            // ---------------------------------
            // 1. Read CSV
            // ---------------------------------

            fileBytes = await FileReader.ReadAsync(path);

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
            // 4. Create ID lookup map
            // ---------------------------------

            idLookupMap = new IdLookupMap(
                recordCount,
                Allocator.TempJob
            );


            // ---------------------------------
            // 5. Parse CSV + Build HashMap
            // ---------------------------------

            var parseJob = new ParseLineJob
            {
                FileBytes = fileBytes,
                LineStartOffsets = lineStartOffsets.AsArray(),
                LineLengths = lineLengths.AsArray(),
                Output = entries,

                IdToIndex = idLookupMap.AsParallelWriter()
            };

            JobHandle parseHandle = parseJob.Schedule(recordCount, 64);

            parseHandle.Complete();

            Debug.Log($"Parsed records: {entries.Length:N0}");

            Debug.Log($"Lookup map created for {entries.Length:N0} records.");


            // ---------------------------------
            // 6. Sort
            // ---------------------------------

            var sortJob = new SortJob
            {
                Entries = entries
            };

            JobHandle sortHandle = sortJob.Schedule();

            sortHandle.Complete();


            // ---------------------------------
            // 7. Test ID lookup
            // ---------------------------------

            if (idLookupMap.TryGetIndex(LookupPlayerId, out int index))
            {
                LeaderboardEntry entry = entries[index];

                Debug.Log(
                    $"Lookup PASSED | " +
                    $"ID: {entry.Id}, " +
                    $"Index: {index}, " +
                    $"Username: {entry.Username}, " +
                    $"Score: {entry.Score}, " +
                    $"OriginalIndex: {entry.OriginalIndex}"
                );
            }
            else
            {
                Debug.LogWarning(
                    $"Lookup FAILED | " +
                    $"Player ID {LookupPlayerId} was not found."
                );
            }


            // ---------------------------------
            // 8. Log sorted result
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

            if (idLookupMap.Equals(default(IdLookupMap)) == false)
                idLookupMap.Dispose();

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
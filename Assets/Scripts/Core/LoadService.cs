using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public sealed class LoadService
{
    public readonly struct LoadResult
    {
        public readonly NativeArray<LeaderboardEntry> Entries;
        public readonly IdLookupMap IdLookupMap;

        public LoadResult(NativeArray<LeaderboardEntry> entries, IdLookupMap idLookupMap)
        {
            Entries = entries;
            IdLookupMap = idLookupMap;
        }

        public bool IsValid => Entries.IsCreated && Entries.Length > 0;
    }

    public async Awaitable<LoadResult> LoadAndParse(string path)
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

            if (!fileBytes.IsCreated || fileBytes.Length == 0)
            {
                Debug.LogError($"LoadService: Failed to read file at '{path}'.");
                return default;
            }

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

            if (lineStartOffsets.Length <= 1)
            {
                Debug.LogWarning("LoadService: No entries found in CSV.");
                return default;
            }

            int recordCount = lineStartOffsets.Length - 1;

            // ---------------------------------
            // 3. Create output array
            // ---------------------------------
            entries = new NativeArray<LeaderboardEntry>(recordCount, Allocator.Persistent);


            // ---------------------------------
            // 4. Create ID lookup map
            // ---------------------------------
            idLookupMap = new IdLookupMap(recordCount, Allocator.Persistent);

            // ---------------------------------
            // 5. Parse CSV
            // ---------------------------------
            var parseJob = new ParseLineJob
            {
                FileBytes = fileBytes,
                LineStartOffsets = lineStartOffsets.AsArray(),
                LineLengths = lineLengths.AsArray(),
                Output = entries,
                // Build HashMap
                IdToIndex = idLookupMap.AsParallelWriter()
            };

            JobHandle parseHandle = parseJob.Schedule(recordCount, 64);
            parseHandle.Complete();

            return new LoadResult(entries, idLookupMap);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("LoadService: Exception occurred during parsing.");
            Debug.LogException(exception);

            if (entries.IsCreated) entries.Dispose();
            if (idLookupMap.IsCreated) idLookupMap.Dispose();

            return default;
        }
        finally
        {
            if (lineStartOffsets.IsCreated)
                lineStartOffsets.Dispose();

            if (lineLengths.IsCreated)
                lineLengths.Dispose();

            if (fileBytes.IsCreated)
                fileBytes.Dispose();
        }
    }
}
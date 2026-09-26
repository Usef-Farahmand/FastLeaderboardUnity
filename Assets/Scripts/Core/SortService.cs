using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public sealed class SortService : IDisposable
{
    private NativeArray<LeaderboardEntry> entries;

    private JobHandle sortHandle;

    private bool initialized;
    private bool sortRunning;

    public event Action SortCompleted;

    public bool IsInitialized => initialized;
    public bool IsSorting => sortRunning;

    public void Initialize(NativeArray<LeaderboardEntry> sourceEntries)
    {
        CompleteSort();

        entries = sourceEntries;
        initialized = entries.IsCreated;
    }

    public void Sort()
    {
        if (!initialized)
        {
            Debug.LogWarning("SortService: Service is not initialized.");

            return;
        }

        if (sortRunning)
        {
            Debug.LogWarning("SortService: Sort is already running.");

            return;
        }

        SortJob job = new SortJob
        {
            Entries = entries
        };

        sortHandle = job.Schedule();
        sortRunning = true;
    }

    public void Update()
    {
        if (!sortRunning)
            return;

        if (!sortHandle.IsCompleted)
            return;

        sortHandle.Complete();
        sortRunning = false;

        SortCompleted?.Invoke();
    }

    private void CompleteSort()
    {
        if (!sortRunning)
            return;

        sortHandle.Complete();
        sortRunning = false;
    }

    public void Dispose()
    {
        CompleteSort();

        entries = default;
        initialized = false;
    }
}
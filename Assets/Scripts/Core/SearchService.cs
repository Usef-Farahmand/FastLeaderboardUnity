using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public sealed class SearchService : IDisposable
{
    private NativeArray<LeaderboardEntry> entries;
    private IdLookupMap idLookupMap;

    private NativeList<int> results;

    private JobHandle searchHandle;

    private bool initialized;
    private bool searchRunning;

    private string pendingQuery;

    private int pendingId;
    private bool pendingHasId;

    private readonly int batchSize;

    public event Action<NativeList<int>> SearchCompleted;

    public bool IsInitialized => initialized;

    public bool IsSearching => searchRunning;

    public SearchService(int batchSize = 64)
    {
        this.batchSize = Mathf.Max(1, batchSize);
    }

    // =========================================================
    // Initialize
    // =========================================================

    public void Initialize(NativeArray<LeaderboardEntry> sourceEntries, IdLookupMap sourceIdLookup)
    {
        CompleteSearch();

        entries = sourceEntries;
        idLookupMap = sourceIdLookup;

        initialized = entries.IsCreated;
    }

    // =========================================================
    // Search
    // =========================================================

    public void Search(string query)
    {
        if (!initialized)
        {
            Debug.LogWarning("LeaderboardSearchService: Service is not initialized.");

            return;
        }

        query ??= string.Empty;

        if (searchRunning)
        {
            pendingQuery = query;
            return;
        }

        StartSearch(query);
    }

    private void StartSearch(string query)
    {
        DisposeResults();

        results = new NativeList<int>(Mathf.Max(1, entries.Length), Allocator.Persistent);

        // Empty query = show everything
        if (string.IsNullOrWhiteSpace(query))
        {
            AddAllResults();

            SearchCompleted?.Invoke(results);

            return;
        }

        pendingHasId = int.TryParse(query, out pendingId);

        var job = new UsernameSearchJob
        {
            Entries = entries,

            Query = new FixedString64Bytes(query),

            Results = results.AsParallelWriter()
        };

        searchHandle = job.Schedule(entries.Length, batchSize);

        searchRunning = true;
    }

    // =========================================================
    // Update
    // =========================================================

    public void Update()
    {
        if (!searchRunning)
            return;

        if (!searchHandle.IsCompleted)
            return;

        searchHandle.Complete();

        searchRunning = false;

        // ---------------------------------------------
        // Add ID result
        // ---------------------------------------------

        if (pendingHasId)
        {
            if (idLookupMap.TryGetIndex(pendingId, out int idIndex))
            {
                AddResultIfMissing(idIndex);
            }
        }

        pendingHasId = false;

        // ---------------------------------------------
        // Notify UI
        // ---------------------------------------------

        SearchCompleted?.Invoke(results);

        // ---------------------------------------------
        // Latest pending query
        // ---------------------------------------------

        if (pendingQuery != null)
        {
            string query = pendingQuery;

            pendingQuery = null;

            StartSearch(query);
        }
    }

    // =========================================================
    // Results
    // =========================================================

    private void AddAllResults()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            results.Add(i);
        }
    }

    private void AddResultIfMissing(int index)
    {
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == index)
                return;
        }

        results.Add(index);
    }

    private void DisposeResults()
    {
        if (results.IsCreated)
        {
            results.Dispose();
        }

        results = default;
    }

    // =========================================================
    // Complete
    // =========================================================

    private void CompleteSearch()
    {
        if (!searchRunning)
            return;

        searchHandle.Complete();

        searchRunning = false;
    }

    // =========================================================
    // Dispose
    // =========================================================

    public void Dispose()
    {
        CompleteSearch();

        DisposeResults();

        entries = default;
        idLookupMap = default;

        pendingQuery = null;
        pendingHasId = false;

        initialized = false;
    }
}
using Unity.Collections;
using UnityEngine;

public sealed class LeaderboardTestController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private LeaderboardUIManager uiManager;

    [Header("Test Data")]
    [SerializeField, Min(1)]
    private int sampleCount = 1000;

    [Header("Search")]
    [SerializeField, Min(1)]
    private int searchBatchSize = 64;

    private NativeArray<LeaderboardEntry> entries;

    private NativeList<int> allResults;

    private IdLookupMap idLookupMap;

    private SearchService searchService;
    private SortService sortService;

    private void Awake()
    {
        if (uiManager == null)
        {
            Debug.LogError(
                "LeaderboardTestController: UI Manager is not assigned."
            );

            enabled = false;
            return;
        }

        // ---------------------------------------------
        // Sample Data
        // ---------------------------------------------

        CreateSampleData();

        // ---------------------------------------------
        // Sort Service
        // ---------------------------------------------

        sortService = new SortService();

        sortService.Initialize(entries);

        sortService.Sort();

        sortService.SortCompleted += OnSortCompleted;
    }

    private void Update()
    {
        searchService?.Update();
        sortService?.Update();
    }

    // ==================================================
    // Sample Data
    // ==================================================

    private void CreateSampleData()
    {
        entries = new NativeArray<LeaderboardEntry>(
            sampleCount,
            Allocator.Persistent
        );

        for (int i = 0; i < sampleCount; i++)
        {
            entries[i] = new LeaderboardEntry
            {
                Id = i + 1,

                Username = new FixedString64Bytes($"Player_{i + 1}"),

                Score = i * Random.Range(10, 20),

                OriginalIndex = i
            };
        }
    }

    // ==================================================
    // ID Lookup
    // ==================================================

    private void CreateIdLookupMap()
    {
        idLookupMap = new IdLookupMap(
            entries.Length,
            Allocator.Persistent
        );

        for (int i = 0; i < entries.Length; i++)
        {
            idLookupMap.Add(entries[i].Id, i);
        }

        Debug.Log(
            $"ID lookup map created: " +
            $"{entries.Length:N0} entries."
        );
    }

    // ==================================================
    // Initial Results
    // ==================================================

    private void CreateInitialResults()
    {
        allResults = new NativeList<int>(
            entries.Length,
            Allocator.Persistent
        );

        for (int i = 0; i < entries.Length; i++)
        {
            allResults.Add(i);
        }
    }

    // ==================================================
    // Search
    // ==================================================

    private void OnSearchRequested(
        string query)
    {
        searchService.Search(query);
    }

    private void OnSearchCompleted(
        NativeList<int> results)
    {
        uiManager.SetSearchResults(results);

        Debug.Log(
            $"Search completed. " +
            $"Matches: {results.Length:N0}"
        );
    }

    // ==================================================
    // Sort
    // ==================================================

    private void OnSortCompleted()
    {
        // ---------------------------------------------
        // ID Lookup Map
        // ---------------------------------------------

        CreateIdLookupMap();

        // ---------------------------------------------
        // UI Data
        // ---------------------------------------------

        uiManager.SetData(entries);

        // ---------------------------------------------
        // Initial Results
        // ---------------------------------------------

        CreateInitialResults();

        uiManager.SetSearchResults(allResults);

        // ---------------------------------------------
        // Search Service
        // ---------------------------------------------

        searchService = new SearchService(searchBatchSize);

        searchService.Initialize(entries, idLookupMap);

        // ---------------------------------------------
        // Events
        // ---------------------------------------------

        uiManager.SearchRequested += OnSearchRequested;
        searchService.SearchCompleted += OnSearchCompleted;

        Debug.Log(
            $"Leaderboard initialized with " +
            $"{entries.Length:N0} entries."
        );
    }

    // ==================================================
    // Cleanup
    // ==================================================

    private void OnDestroy()
    {
        if (uiManager != null)
        {
            uiManager.SearchRequested -= OnSearchRequested;
        }

        if (searchService != null)
        {
            searchService.SearchCompleted -= OnSearchCompleted;

            searchService.Dispose();
            searchService = null;
        }

        if (sortService != null)
        {
            sortService.SortCompleted -= OnSortCompleted;

            sortService.Dispose();
            sortService = null;
        }

        if (idLookupMap.IsCreated)
        {
            idLookupMap.Dispose();
            idLookupMap = default;
        }

        if (allResults.IsCreated)
        {
            allResults.Dispose();
            allResults = default;
        }

        if (entries.IsCreated)
        {
            entries.Dispose();
            entries = default;
        }
    }
}
using Unity.Collections;
using UnityEngine;

public class Manager : MonoBehaviour
{
    [SerializeField]
    private string path;

    [Header("UI")]
    private LeaderboardUIManager uiManager;

    [Header("Services")]
    [SerializeField, Min(1)]
    private int searchBatchSize = 64;
    private LoadService loadService;
    private SortService sortService;
    private SearchService searchService;

    private NativeArray<LeaderboardEntry> entries;
    private NativeList<int> allResults;

    #region Unity Callbacks

    private async void Start()
    {
        uiManager = LeaderboardUIManager.Instance;
        if (uiManager == null)
        {
            Debug.LogError("Manager: LeaderboardUIManager instance not found.");
            enabled = false;
            return;
        }

        // ---------------------------------------------
        // 1-4. Load Data
        // ---------------------------------------------

        loadService = new LoadService();
        entries = await loadService.LoadAndParse(path);

        // ---------------------------------------------
        // 5. Sort Service
        // ---------------------------------------------

        sortService = new SortService();
        sortService.SortCompleted += OnSortCompleted;

        sortService.Initialize(this.entries);
        sortService.Sort();
    }

    private void Update()
    {
        searchService?.Update();
        sortService?.Update();
    }

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

    #endregion

    #region Search

    private void OnSearchRequested(string query)
    {
        searchService?.Search(query);
    }

    private void OnSearchCompleted(NativeList<int> results)
    {
        uiManager.SetSearchResults(results);
    }

    #endregion

    #region Sort

    private void OnSortCompleted()
    {
        // ---------------------------------
        // 6. Initialize UI
        // ---------------------------------
        uiManager.SetData(entries);

        CreateInitialResults();
        uiManager.SetSearchResults(allResults);

        // ---------------------------------
        // 7. Search Searvice
        // ---------------------------------
        searchService = new SearchService(searchBatchSize);
        searchService.Initialize(entries);

        uiManager.SearchRequested += OnSearchRequested;
        searchService.SearchCompleted += OnSearchCompleted;
    }

    private void CreateInitialResults()
    {
        allResults = new NativeList<int>(entries.Length, Allocator.Persistent);

        for (int i = 0; i < entries.Length; i++)
        {
            allResults.Add(i);
        }
    }

    #endregion
}
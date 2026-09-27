using System;
using Unity.Collections;
using UnityEngine;

public class LeaderboardUIManager : MonoBehaviour
{
    private static LeaderboardUIManager instance;
    public static LeaderboardUIManager Instance => instance;

    [Header("Search")]
    [SerializeField]
    private SearchInput searchInput;

    [Header("List")]
    [SerializeField]
    private ItemScrollView scrollView;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(this);
    }

    public event Action<string> SearchRequested
    {
        add => searchInput.SearchRequested += value;
        remove => searchInput.SearchRequested -= value;
    }

    public void SetData(NativeArray<LeaderboardEntry> entries)
    {
        scrollView.Initialize(entries);
    }

    public void SetSearchResults(NativeList<int> results)
    {
        scrollView.SetResults(results);
    }
}
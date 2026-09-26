using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ItemScrollView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private ScrollRect scrollRect;

    [SerializeField]
    private RectTransform viewport;

    [SerializeField]
    private RectTransform content;

    [SerializeField]
    private ItemSlot prefab;

    [Header("Item Settings")]
    [SerializeField, Min(1f)]
    private float itemHeight = 60f;

    [SerializeField, Min(0f)]
    private float itemSpacing = 10f;

    [Header("List Padding")]
    [SerializeField, Min(0f)]
    private float topPadding = 20f;

    [SerializeField, Min(0f)]
    private float bottomPadding = 20f;

    [Header("Virtualization")]
    [SerializeField, Min(0)]
    private int bufferCount = 2;

    private ItemContainer container;

    private NativeArray<LeaderboardEntry> entries;

    private NativeList<int> resultIndices;

    private int poolSize;

    private float ItemStep => itemHeight + itemSpacing;

    private int DataCount => resultIndices.IsCreated ? resultIndices.Length : 0;

    private void Awake()
    {
        scrollRect.onValueChanged.AddListener(OnScroll);
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        container?.Dispose();

        DisposeResults();
    }

    // =========================================================
    // Initialization
    // =========================================================

    private void EnsureInitialized()
    {
        if (container != null)
            return;

        poolSize = CalculatePoolSize();

        container = new ItemContainer(prefab, content, poolSize);

        HideAll();
    }

    private int CalculatePoolSize()
    {
        float viewportHeight = Mathf.Max(1f, viewport.rect.height);

        int visibleCount = Mathf.CeilToInt(viewportHeight / ItemStep);

        return Mathf.Max(1, visibleCount + bufferCount + 2);
    }

    // =========================================================
    // Public API
    // =========================================================

    public void Initialize(NativeArray<LeaderboardEntry> sourceEntries)
    {
        EnsureInitialized();

        entries = sourceEntries;

        ClearResults();

        ResetScroll();

        UpdateContentSize();

        Refresh();
    }

    public void SetResults(NativeList<int> results)
    {
        EnsureInitialized();

        CopyResults(results);

        ResetScroll();

        UpdateContentSize();

        Refresh();
    }

    // =========================================================
    // Results
    // =========================================================

    private void CopyResults(NativeList<int> source)
    {
        if (!resultIndices.IsCreated)
        {
            resultIndices = new NativeList<int>(Mathf.Max(1, source.Length), Allocator.Persistent);
        }
        else
        {
            resultIndices.Clear();

            if (resultIndices.Capacity < source.Length)
            {
                resultIndices.Capacity = source.Length;
            }
        }

        for (int i = 0; i < source.Length; i++)
        {
            resultIndices.Add(source[i]);
        }
    }

    private void ClearResults()
    {
        if (resultIndices.IsCreated)
        {
            resultIndices.Clear();
        }
    }

    private void DisposeResults()
    {
        if (resultIndices.IsCreated)
        {
            resultIndices.Dispose();
        }

        resultIndices = default;
    }

    // =========================================================
    // Content
    // =========================================================

    private void UpdateContentSize()
    {
        float height;

        if (DataCount == 0)
        {
            height = viewport.rect.height;
        }
        else
        {
            float itemsHeight = DataCount * itemHeight;

            float spacingHeight = Mathf.Max(0, DataCount - 1) * itemSpacing;

            height = topPadding + itemsHeight + spacingHeight + bottomPadding;

            height = Mathf.Max(height, viewport.rect.height);
        }

        Vector2 size = content.sizeDelta;

        size.y = height;

        content.sizeDelta = size;
    }

    // =========================================================
    // Scroll
    // =========================================================

    private void ResetScroll()
    {
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }

        Vector2 position = content.anchoredPosition;

        position.y = 0f;

        content.anchoredPosition = position;
    }

    private void OnScroll(Vector2 position)
    {
        Refresh();
    }

    // =========================================================
    // Virtualization
    // =========================================================

    private void Refresh()
    {
        EnsureInitialized();

        if (DataCount == 0)
        {
            HideAll();
            return;
        }

        float scrollY = Mathf.Max(0f, content.anchoredPosition.y);

        float itemAreaY = Mathf.Max(0f, scrollY - topPadding);

        int firstItemIndex = Mathf.FloorToInt(itemAreaY / ItemStep);

        firstItemIndex = Mathf.Clamp(firstItemIndex, 0, Mathf.Max(0, DataCount - 1));

        for (int poolIndex = 0; poolIndex < poolSize; poolIndex++)
        {
            int itemIndex = firstItemIndex + poolIndex;

            if (itemIndex >= DataCount)
            {
                container.Hide(poolIndex);
                continue;
            }

            UpdatePoolItem(poolIndex, itemIndex);
        }
    }

    // =========================================================
    // Item
    // =========================================================

    private void UpdatePoolItem(int poolIndex, int itemIndex)
    {
        if (poolIndex < 0 || poolIndex >= poolSize)
        {
            return;
        }

        if (itemIndex < 0 || itemIndex >= DataCount)
        {
            container.Hide(poolIndex);
            return;
        }

        int sourceIndex = resultIndices[itemIndex];

        if (sourceIndex < 0 || sourceIndex >= entries.Length)
        {
            container.Hide(poolIndex);
            return;
        }

        LeaderboardEntry entry = entries[sourceIndex];

        container.SetItem(
            poolIndex,
            itemIndex,
            ItemStep,
            topPadding,
            entry.Id,
            entry.Username.ToString(),
            entry.Score
        );
    }

    // =========================================================
    // Pool
    // =========================================================

    private void HideAll()
    {
        if (container == null)
            return;

        for (int i = 0; i < poolSize; i++)
        {
            container.Hide(i);
        }
    }
}
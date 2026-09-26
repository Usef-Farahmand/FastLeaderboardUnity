using NUnit.Framework.Interfaces;
using System.Collections.Generic;
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

    [Header("Data")]
    [SerializeField, Min(1)]
    private int totalItemCount = 1_000_000;

    private ItemContainer container;

    private int poolSize;

    private int firstItemIndex;
    private int firstPoolIndex;

    private float ItemStep => itemHeight + itemSpacing;

    private void Awake()
    {
        poolSize = CalculatePoolSize();

        container = new ItemContainer(prefab, content, poolSize);

        UpdateContentSize();

        scrollRect.onValueChanged.AddListener(OnScroll);

        Initialize();
    }

    private void OnDestroy()
    {
        if (scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        container?.Dispose();
    }

    private int CalculatePoolSize()
    {
        int visibleCount = Mathf.CeilToInt(Mathf.Max(0f, viewport.rect.height - topPadding) / ItemStep);

        return Mathf.Max(1, visibleCount + 1);
    }

    private void UpdateContentSize()
    {
        float itemsHeight = totalItemCount * itemHeight;

        float spacingHeight = Mathf.Max(0, totalItemCount - 1) * itemSpacing;

        float contentHeight = topPadding + itemsHeight + spacingHeight + bottomPadding;

        Vector2 size = content.sizeDelta;

        size.y = contentHeight;

        content.sizeDelta = size;
    }

    private void Initialize()
    {
        firstItemIndex = 0;
        firstPoolIndex = 0;

        RefreshAll();
    }

    private void OnScroll(Vector2 position)
    {
        Refresh();
    }

    private void Refresh()
    {
        float scrollY = Mathf.Max(0f, content.anchoredPosition.y);

        float itemScrollY = Mathf.Max(0f, scrollY - topPadding);

        int visibleStartIndex = Mathf.FloorToInt(itemScrollY / ItemStep);

        int maxStartIndex = Mathf.Max(0, totalItemCount - poolSize);

        int targetFirstItemIndex = Mathf.Clamp(visibleStartIndex, 0, maxStartIndex);

        int delta = targetFirstItemIndex - firstItemIndex;

        if (delta == 0)
            return;

        if (delta > 0)
        {
            ScrollDown(delta);
        }
        else
        {
            ScrollUp(-delta);
        }

        firstItemIndex = targetFirstItemIndex;
    }

    private void RefreshAll()
    {
        for (int offset = 0; offset < poolSize; offset++)
        {
            int poolIndex = (firstPoolIndex + offset) % poolSize;

            int itemIndex = firstItemIndex + offset;

            if (itemIndex >= totalItemCount)
            {
                container.Hide(poolIndex);
                continue;
            }

            UpdatePoolItem(poolIndex, itemIndex);
        }
    }

    private void ScrollDown(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int recycledPoolIndex = firstPoolIndex;

            firstPoolIndex++;

            if (firstPoolIndex >= poolSize)
            {
                firstPoolIndex = 0;
            }

            int newItemIndex = firstItemIndex + poolSize + i;

            if (newItemIndex >= totalItemCount)
            {
                container.Hide(recycledPoolIndex);

                continue;
            }

            UpdatePoolItem(recycledPoolIndex, newItemIndex);
        }
    }

    private void ScrollUp(int count)
    {
        for (int i = 0; i < count; i++)
        {
            firstPoolIndex--;

            if (firstPoolIndex < 0)
            {
                firstPoolIndex = poolSize - 1;
            }

            int newItemIndex = firstItemIndex - 1 - i;

            if (newItemIndex < 0)
            {
                container.Hide(firstPoolIndex);

                continue;
            }

            UpdatePoolItem(firstPoolIndex, newItemIndex);
        }
    }

    private void UpdatePoolItem(int poolIndex, int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= totalItemCount)
        {
            container.Hide(poolIndex);
            return;
        }

        /*
         * TODO:
         * اینجا Data واقعی را بگیر.
         */

        int id = itemIndex;

        string username =
            $"User {itemIndex}";

        int score =
            itemIndex;

        container.SetItem(
            poolIndex,
            itemIndex,
            ItemStep,
            topPadding,
            id,
            username,
            score
        );
    }
}
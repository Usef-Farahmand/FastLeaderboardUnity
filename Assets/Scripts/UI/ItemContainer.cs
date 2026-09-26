using UnityEngine;

public sealed class ItemContainer
{
    private readonly ItemSlot[] items;

    public ItemContainer(ItemSlot prefab, RectTransform content, int poolSize)
    {
        items = new ItemSlot[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            ItemSlot item = Object.Instantiate(prefab, content);

            item.gameObject.SetActive(false);

            items[i] = item;
        }
    }

    public void SetItem(int poolIndex, int itemIndex, float itemStep, float topPadding, int id, string username, int score)
    {
        if (poolIndex < 0 || poolIndex >= items.Length)
            return;

        ItemSlot item = items[poolIndex];

        RectTransform rect = item.Rect;

        float y = -topPadding - itemIndex * itemStep;

        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);

        item.SetData(itemIndex, id, username, score);

        item.gameObject.SetActive(true);
    }

    public void Hide(int poolIndex)
    {
        if (poolIndex < 0 || poolIndex >= items.Length)
            return;

        items[poolIndex].gameObject.SetActive(false);
    }

    public void Dispose()
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                Object.Destroy(items[i].gameObject);
            }
        }
    }
}
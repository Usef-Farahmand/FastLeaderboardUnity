using Unity.Collections;
using UnityEngine;

public class TestParse : MonoBehaviour
{
    [SerializeField]
    private string path;
    [SerializeField]
    [Min(0)] private int MaxPreviewCount = 100;

    private readonly LoadService service = new LoadService();

    [ContextMenu("Load And Parse")]
    public async void LoadAndParse()
    {
        var entries = await service.LoadAndParse(path);

        Debug.Log($"Parsed records: {entries.Length:N0}");

        int previewCount = Mathf.Min(MaxPreviewCount, entries.Length);

        for (int i = 0; i < previewCount; i++)
        {
            LeaderboardEntry entry = entries[i];

            Debug.Log(
                $"[{i}] " +
                $"ID: {entry.Id}, " +
                $"Username: {entry.Username}, " +
                $"Score: {entry.Score}, " +
                $"OriginalIndex: {entry.OriginalIndex}");
        }
    }
}

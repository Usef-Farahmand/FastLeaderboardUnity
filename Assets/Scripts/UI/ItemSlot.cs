using TMPro;
using UnityEngine;

public class ItemSlot : MonoBehaviour
{
    [SerializeField]
    private TMP_Text indexText;

    [SerializeField]
    private TMP_Text idText;

    [SerializeField]
    private TMP_Text usernameText;

    [SerializeField]
    private TMP_Text scoreText;

    public RectTransform Rect { get; private set; }

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
    }

    public void SetData(int index, int id, string username, int score)
    {
        indexText.SetText(index.ToString());
        idText.SetText(id.ToString());
        usernameText.SetText(username);
        scoreText.SetText(score.ToString("N0"));
    }
}
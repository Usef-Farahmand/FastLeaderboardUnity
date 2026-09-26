using System;
using TMPro;
using UnityEngine;

public class SearchInput : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private TMP_InputField inputField;

    [SerializeField, Min(0.01f)]
    private float debounceDelay = 0.15f;

    private float debounceTimer;
    private bool searchPending;

    private string pendingQuery;

    /// <summary>
    /// Fired when the user stops typing for the debounce duration.
    /// </summary>
    public event Action<string> SearchRequested;

    private void OnEnable()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.AddListener(
                OnInputValueChanged
            );
        }
    }

    private void OnDisable()
    {
        if (inputField != null)
        {
            inputField.onValueChanged.RemoveListener(
                OnInputValueChanged
            );
        }
    }

    private void Update()
    {
        if (!searchPending)
            return;

        debounceTimer -= Time.unscaledDeltaTime;

        if (debounceTimer > 0f)
            return;

        searchPending = false;

        SearchRequested?.Invoke(
            pendingQuery
        );
    }

    private void OnInputValueChanged(string value)
    {
        pendingQuery = value;

        debounceTimer = debounceDelay;
        searchPending = true;
    }

    /// <summary>
    /// Returns the current input text.
    /// </summary>
    public string GetCurrentQuery()
    {
        return inputField != null
            ? inputField.text
            : string.Empty;
    }

    /// <summary>
    /// Clears the input field and requests a search.
    /// </summary>
    public void Clear()
    {
        if (inputField == null)
            return;

        inputField.text = string.Empty;
    }

    /// <summary>
    /// Immediately requests a search with the current text.
    /// </summary>
    public void Submit()
    {
        searchPending = false;
        debounceTimer = 0f;

        SearchRequested?.Invoke(
            GetCurrentQuery()
        );
    }
}
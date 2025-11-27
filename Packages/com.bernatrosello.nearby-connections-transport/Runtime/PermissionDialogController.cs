using UnityEngine;
using UnityEngine.UI;

public class PermissionDialogController : MonoBehaviour
{
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button cancelButton;

    public System.Action OnOpenSettings;
    public System.Action OnCancel;

    private void Awake()
    {
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(() => OnOpenSettings?.Invoke());

        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => OnCancel?.Invoke());
        Debug.Log("Woke PermissionDialogController");
    }
}

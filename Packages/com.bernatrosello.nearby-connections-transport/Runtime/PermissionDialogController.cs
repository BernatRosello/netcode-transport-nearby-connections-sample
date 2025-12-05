using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PermissionDialogController : MonoBehaviour
{
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text permissionText;
    private List<string> deniedPermissions;

    public System.Action OnOpenSettings;
    public System.Action OnCancel;

    private void Awake()
    {
        deniedPermissions = new List<string>();
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(() => OnOpenSettings?.Invoke());

        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => OnCancel?.Invoke());
        Debug.Log("Woke PermissionDialogController");
    }

    public void AddDeniedPermissionToDialog(string permission)
    {
        if (deniedPermissions.Contains(permission)) return;
        
        deniedPermissions.Add(permission);
        SetDeniedPermissionsText();
    }

    private void SetDeniedPermissionsText()
    {
        string permList = "";
        foreach(var p in deniedPermissions)
        {
            permList += p + "\n";
        }
        permissionText.text = permList;
    }
}

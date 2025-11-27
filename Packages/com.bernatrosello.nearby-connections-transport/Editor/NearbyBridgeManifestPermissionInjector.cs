// Source - https://stackoverflow.com/a
// Posted by pale bone, modified by community. See post 'Timeline' for change history
// Retrieved 2025-11-27, License - CC BY-SA 4.0

using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;
using Netcode.Transports.NearbyConnections;

public class ModifyUnityAndroidAppManifestSample : IPostGenerateGradleAndroidProject
{

    public void OnPostGenerateGradleAndroidProject(string basePath)
    {
        // If needed, add condition checks on whether you need to run the modification routine.
        // For example, specific configuration/app options enabled

        var androidManifest = new AndroidManifest(GetManifestPath(basePath));

        androidManifest.SetNearbyConnectionsPermissions();

        // Add your XML manipulation routines

        androidManifest.Save();
    }

    public int callbackOrder { get { return 1; } }

    private string _manifestFilePath;

    private string GetManifestPath(string basePath)
    {
        if (string.IsNullOrEmpty(_manifestFilePath))
        {
            var pathBuilder = new StringBuilder(basePath);
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("src");
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("main");
            pathBuilder.Append(Path.DirectorySeparatorChar).Append("AndroidManifest.xml");
            _manifestFilePath = pathBuilder.ToString();
        }
        return _manifestFilePath;
    }
}


internal class AndroidXmlDocument : XmlDocument
{
    private string m_Path;
    protected XmlNamespaceManager nsMgr;
    public readonly string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";
    public AndroidXmlDocument(string path)
    {
        m_Path = path;
        using (var reader = new XmlTextReader(m_Path))
        {
            reader.Read();
            Load(reader);
        }
        nsMgr = new XmlNamespaceManager(NameTable);
        nsMgr.AddNamespace("android", AndroidXmlNamespace);
    }

    public string Save()
    {
        return SaveAs(m_Path);
    }

    public string SaveAs(string path)
    {
        using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
        {
            writer.Formatting = Formatting.Indented;
            Save(writer);
        }
        return path;
    }
}


internal class AndroidManifest : AndroidXmlDocument
{
    private readonly XmlElement ApplicationElement;

    public AndroidManifest(string path) : base(path)
    {
        ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
    }

    private XmlAttribute CreateAndroidAttribute(string key, string value)
    {
        XmlAttribute attr = CreateAttribute("android", key, AndroidXmlNamespace);
        attr.Value = value;
        return attr;
    }

    internal XmlNode GetActivityWithLaunchIntent()
    {
        return SelectSingleNode("/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and " +
                "intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
    }

    internal void SetApplicationTheme(string appTheme)
    {
        ApplicationElement.Attributes.Append(CreateAndroidAttribute("theme", appTheme));
    }

    internal void SetStartingActivityName(string activityName)
    {
        GetActivityWithLaunchIntent().Attributes.Append(CreateAndroidAttribute("name", activityName));
    }


    internal void SetHardwareAcceleration()
    {
        GetActivityWithLaunchIntent().Attributes.Append(CreateAndroidAttribute("hardwareAccelerated", "true"));
    }

    internal void AddPermission(string permissionName, string minSdk = null, string maxSdk = null)
    {
        var manifest = SelectSingleNode("/manifest");

        //
        // 1. Find *all* existing permissions with the same android:name
        //
        var existingPermissions = SelectNodes(
            $"/manifest/uses-permission[@android:name='{permissionName}']",
            nsMgr
        );

        //
        // 2. Remove all existing nodes so we can overwrite with correct one
        //
        if (existingPermissions != null)
        {
            foreach (XmlNode node in existingPermissions)
            {
                node.ParentNode.RemoveChild(node);
            }
        }

        //
        // 3. Create new permission with correct attributes
        //
        XmlElement child = CreateElement("uses-permission");

        if (!string.IsNullOrEmpty(minSdk))
            child.Attributes.Append(CreateAndroidAttribute("minSdkVersion", minSdk));

        if (!string.IsNullOrEmpty(maxSdk))
            child.Attributes.Append(CreateAndroidAttribute("maxSdkVersion", maxSdk));

        child.Attributes.Append(CreateAndroidAttribute("name", permissionName));

        //
        // 4. Append the new permission
        //
        manifest.AppendChild(child);
    }

    internal void SetNearbyConnectionsPermissions()
    {
        // Required for Nearby Connections
        foreach (var permission in NBCTransport.NearbyPermissionDefinitions.Permissions)
        {
            AddPermission(permission.Name, permission.MinSdk, permission.MaxSdk);
        }
    }
}

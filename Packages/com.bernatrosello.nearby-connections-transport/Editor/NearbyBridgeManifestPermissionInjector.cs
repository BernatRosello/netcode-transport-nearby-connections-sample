// Source - https://stackoverflow.com/a
// Posted by pale bone, modified by community. See post 'Timeline' for change history
// Retrieved 2025-11-27, License - CC BY-SA 4.0

using System.IO;
using System.Text;
using System.Xml;
using UnityEditor.Android;

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

        XmlElement child = CreateElement("uses-permission");

        if (!string.IsNullOrEmpty(minSdk))
            child.Attributes.Append(CreateAndroidAttribute("minSdkVersion", minSdk));

        if (!string.IsNullOrEmpty(maxSdk))
            child.Attributes.Append(CreateAndroidAttribute("maxSdkVersion", maxSdk));

        child.Attributes.Append(CreateAndroidAttribute("name", permissionName));

        manifest.AppendChild(child);
    }

    internal void SetNearbyConnectionsPermissions()
    {
        // Required for Nearby Connections
        AddPermission("android.permission.ACCESS_WIFI_STATE", maxSdk: "34");
        AddPermission("android.permission.CHANGE_WIFI_STATE", maxSdk: "34");
        AddPermission("android.permission.BLUETOOTH", maxSdk: "30");
        AddPermission("android.permission.BLUETOOTH_ADMIN", maxSdk: "30");
        AddPermission("android.permission.ACCESS_COARSE_LOCATION", maxSdk: "34");
        AddPermission("android.permission.ACCESS_FINE_LOCATION", minSdk: "29", maxSdk: "34");
        AddPermission("android.permission.BLUETOOTH_ADVERTISE", minSdk: "31");
        AddPermission("android.permission.BLUETOOTH_CONNECT", minSdk: "31");
        AddPermission("android.permission.BLUETOOTH_SCAN", minSdk: "31");
        AddPermission("android.permission.NEARBY_WIFI_DEVICES", minSdk: "32");

        // Optional (only needed for file payloads)
        AddPermission("android.permission.READ_EXTERNAL_STORAGE");
    }
}

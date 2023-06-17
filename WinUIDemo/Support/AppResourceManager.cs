using System;
using Microsoft.Windows.ApplicationModel.Resources;

namespace WinUIDemo;

/// <summary>
/// Helper class for fetching values contained in a resource file.
/// </summary>
public sealed class AppResourceManager
{
    private static AppResourceManager instance = null;
    private static ResourceManager _resourceManager = null;
    private static ResourceContext _resourceContext = null;

    public static AppResourceManager Instance
    {
        get
        {
            if (instance == null)
                instance = new AppResourceManager();
            return instance;
        }
    }

    private AppResourceManager()
    {
        _resourceManager = new ResourceManager();
        _resourceContext = _resourceManager.CreateResourceContext();
    }

    /// <summary>
    /// string label = AppResourceManager.Instance.GetString("MyButton.Content");
    /// </summary>
    /// <param name="name">property name in Resources.resw</param>
    /// <returns>value contained in Resources.resw which matches the current culture</returns>
    public string GetString(string name)
    {
        var result = _resourceManager.MainResourceMap.GetValue($"Resources/{name.Replace(".", "/")}", _resourceContext).ValueAsString;
        return result;
    }
}
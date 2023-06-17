using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinUIDemo;
/*
  [Based off of https://github.com/CommunityToolkit/WindowsCommunityToolkit/issues/3636]

  //======================================================================================
  using System.Text.Json;
  namespace Microsoft.Toolkit.Uwp.Helpers
  {
      public class JsonObjectSerializer : IObjectSerializer
      {
          public string Serialize<T>(T value) => JsonSerializer.Serialize(value);
  
          public T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value);
      }
  }
  
  //======================================================================================
  using Newtonsoft.Json;
  namespace Microsoft.Toolkit.Uwp.Helpers
  {
      internal class JsonObjectSerializer : IObjectSerializer
      {
          public string Serialize<T>(T value) => JsonConvert.SerializeObject(value);
  
          public T Deserialize<T>(string value) => JsonConvert.DeserializeObject<T>(value);
      }
  }
*/

/// <summary>
/// Offers read/write routines for the <see cref="ApplicationDataContainer"/> when using a packaged application.
/// This class will also initialize the Settings property to <see cref="Windows.Storage.ApplicationData.Current.LocalSettings"/>
/// and the Folder property to <see cref="Windows.Storage.ApplicationData.Current.LocalFolder"/> automatically if they are null.
/// To test as a Packaged application you must change the {WindowsPackageType} from "None" to "MSIX" in your csproj file.
/// </summary>
public class ObjectStorageHelper : IObjectStorageHelper
{
    private readonly IObjectSerializer serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStorageHelper"/> class,
    /// which can read and write data using the provided <see cref="IObjectSerializer"/>;
    /// </summary>
    /// <param name="objectSerializer">The serializer to use.</param>
    public ObjectStorageHelper(IObjectSerializer objectSerializer)
    {
        serializer = objectSerializer ?? throw new ArgumentNullException(nameof(objectSerializer));
    }

    /// <summary>
    /// Gets or sets the settings container.
    /// </summary>
    protected ApplicationDataContainer Settings { get; set; }

    /// <summary>
    /// Gets or sets the storage folder.
    /// </summary>
    protected StorageFolder Folder { get; set; }

    /// <summary>
    /// Determines whether a setting already exists.
    /// </summary>
    /// <param name="key">Key of the setting (that contains object)</param>
    /// <returns><c>true</c> if the setting exists; otherwise, <c>false</c>.</returns>
    public bool KeyExists(string key)
    {
        if (Settings == null)
            Settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        return Settings.Values.ContainsKey(key);
    }

    /// <summary>
    /// Determines whether a setting already exists in composite.
    /// </summary>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="key">Key of the setting (that contains object)</param>
    /// <returns><c>true</c> if the setting exists; otherwise, <c>false</c>.</returns>
    public bool KeyExists(string compositeKey, string key)
    {
        if (KeyExists(compositeKey))
        {
            ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)Settings.Values[compositeKey];
            if (composite != null)
            {
                return composite.ContainsKey(key);
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieves a single item by its key.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="key">Key of the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>The T object</returns>
    public T Read<T>(string key, T @default = default(T))
    {
        if (Settings == null)
            Settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        if (!Settings.Values.TryGetValue(key, out var value) || value == null)
        {
            return @default;
        }

        return serializer.Deserialize<T>(value);
    }

    /// <summary>
    /// Retrieves a single item by its key in composite.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="key">Key of the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>The T object</returns>
    public T Read<T>(string compositeKey, string key, T @default = default(T))
    {
        if (Settings == null)
            Settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)Settings.Values[compositeKey];
        if (composite != null)
        {
            string value = (string)composite[key];
            if (value != null)
            {
                return serializer.Deserialize<T>(value);
            }
        }

        return @default;
    }

    /// <summary>
    /// Saves a single item by its key.
    /// This method should be considered for objects that do not exceed 8k bytes during the lifetime of the application
    /// (refers to <see cref="SaveFileAsync{T}(string, T)"/> for complex/large objects).
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="key">Key of the value saved</param>
    /// <param name="value">Object to save</param>
    public void Save<T>(string key, T value)
    {
        if (Settings == null)
            Settings = Windows.Storage.ApplicationData.Current.LocalSettings;

        var type = typeof(T);
        var typeInfo = type.GetTypeInfo();

        Settings.Values[key] = serializer.Serialize(value);
    }

    /// <summary>
    /// Saves a group of items by its key in a composite.
    /// This method should be considered for objects that do not exceed 8k bytes during the lifetime of the application
    /// (refers to <see cref="SaveFileAsync{T}(string, T)"/> for complex/large objects) and for groups of settings which
    /// need to be treated in an atomic way.
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="values">Objects to save</param>
    public void Save<T>(string compositeKey, IDictionary<string, T> values)
    {
        if (KeyExists(compositeKey))
        {
            ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)Settings.Values[compositeKey];

            foreach (KeyValuePair<string, T> setting in values)
            {
                if (composite.ContainsKey(setting.Key))
                {
                    composite[setting.Key] = serializer.Serialize(setting.Value);
                }
                else
                {
                    composite.Add(setting.Key, serializer.Serialize(setting.Value));
                }
            }
        }
        else
        {
            ApplicationDataCompositeValue composite = new ApplicationDataCompositeValue();
            foreach (KeyValuePair<string, T> setting in values)
            {
                composite.Add(setting.Key, serializer.Serialize(setting.Value));
            }

            Settings.Values[compositeKey] = composite;
        }
    }

    /// <summary>
    /// Determines whether a file already exists.
    /// </summary>
    /// <param name="filePath">Key of the file (that contains object)</param>
    /// <returns><c>true</c> if the file exists; otherwise, <c>false</c>.</returns>
    public Task<bool> FileExistsAsync(string filePath)
    {
        if (Folder == null)
            Folder = Windows.Storage.ApplicationData.Current.LocalFolder;

        return Folder.FileExistsAsync(filePath);
    }

    /// <summary>
    /// Retrieves an object from a file.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="filePath">Path to the file that contains the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>Waiting task until completion with the object in the file</returns>
    public async Task<T> ReadFileAsync<T>(string filePath, T @default = default(T))
    {
        if (Folder == null)
            Folder = Windows.Storage.ApplicationData.Current.LocalFolder;

        string value = await StorageFileHelper.ReadTextFromFileAsync(Folder, filePath);
        return (value != null) ? serializer.Deserialize<T>(value) : @default;
    }

    /// <summary>
    /// Saves an object inside a file.
    /// There is no limitation to use this method (refers to <see cref="Save{T}(string, T)"/> method for simple objects).
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="filePath">Path to the file that will contain the object</param>
    /// <param name="value">Object to save</param>
    /// <returns>The <see cref="StorageFile"/> where the object was saved</returns>
    public Task<StorageFile> SaveFileAsync<T>(string filePath, T value)
    {
        if (Folder == null)
            Folder = Windows.Storage.ApplicationData.Current.LocalFolder;

        return StorageFileHelper.WriteTextToFileAsync(Folder, serializer.Serialize(value)?.ToString(), filePath, CreationCollisionOption.ReplaceExisting);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinUIDemo;

/// <summary>
/// Service used to store data.
/// </summary>
public interface IObjectStorageHelper
{
    /// <summary>
    /// Determines whether a setting already exists.
    /// </summary>
    /// <param name="key">Key of the setting (that contains object)</param>
    /// <returns>True if a value exists</returns>
    bool KeyExists(string key);

    /// <summary>
    /// Determines whether a setting already exists in composite.
    /// </summary>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="key">Key of the setting (that contains object)</param>
    /// <returns>True if a value exists</returns>
    bool KeyExists(string compositeKey, string key);

    /// <summary>
    /// Retrieves a single item by its key.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="key">Key of the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>The T object</returns>
    T Read<T>(string key, T @default = default(T));

    /// <summary>
    /// Retrieves a single item by its key in composite.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="key">Key of the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>The T object</returns>
    T Read<T>(string compositeKey, string key, T @default = default(T));

    /// <summary>
    /// Saves a single item by its key.
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="key">Key of the value saved</param>
    /// <param name="value">Object to save</param>
    void Save<T>(string key, T value);

    /// <summary>
    /// Saves a group of items by its key in a composite.
    /// This method should be considered for objects that do not exceed 8k bytes during the lifetime of the application
    /// (refers to <see cref="SaveFileAsync{T}(string, T)"/> for complex/large objects) and for groups of settings which
    /// need to be treated in an atomic way.
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="compositeKey">Key of the composite (that contains settings)</param>
    /// <param name="values">Objects to save</param>
    void Save<T>(string compositeKey, IDictionary<string, T> values);

    /// <summary>
    /// Determines whether a file already exists.
    /// </summary>
    /// <param name="filePath">Key of the file (that contains object)</param>
    /// <returns>True if a value exists</returns>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Retrieves an object from a file.
    /// </summary>
    /// <typeparam name="T">Type of object retrieved</typeparam>
    /// <param name="filePath">Path to the file that contains the object</param>
    /// <param name="default">Default value of the object</param>
    /// <returns>Waiting task until completion with the object in the file</returns>
    Task<T> ReadFileAsync<T>(string filePath, T @default = default(T));

    /// <summary>
    /// Saves an object inside a file.
    /// </summary>
    /// <typeparam name="T">Type of object saved</typeparam>
    /// <param name="filePath">Path to the file that will contain the object</param>
    /// <param name="value">Object to save</param>
    /// <returns>Waiting task until completion</returns>
    Task<StorageFile> SaveFileAsync<T>(string filePath, T value);
}

/// <summary>
/// A basic serialization service.
/// </summary>
public interface IObjectSerializer
{
    /// <summary>
    /// Serialize an object into a string. It is recommended to use strings as the final format for objects if you plan to use the <see cref="ObjectStorageHelper.SaveFileAsync{T}(string, T)"/> method.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>The serialized object.</returns>
    object Serialize<T>(T value);

    /// <summary>
    /// Deserialize a primitive or string into an object of the given type.
    /// </summary>
    /// <typeparam name="T">The type of the deserialized object.</typeparam>
    /// <param name="value">The string to deserialize.</param>
    /// <returns>The deserialized object.</returns>
    T Deserialize<T>(object value);
}
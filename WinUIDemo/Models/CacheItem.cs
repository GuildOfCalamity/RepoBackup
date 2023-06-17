#nullable enable
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;

namespace WinUIDemo.Models
{
    /// <summary>
    /// Generic cache model.
    /// </summary>
    /// <typeparam name="T">Type is set by consuming cache</typeparam>
    public class CacheItem<T>
    {
        public string Id { get; private set; }
        public DateTime Created { get; private set; }
        public DateTime Updated { get; private set; }
        public T Item { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItem{T}"/> class.
        /// </summary>
        /// <param name="id">uniquely identifies the item</param>
        /// <param name="updated">last updated timestamp</param>
        /// <param name="item">the item being stored</param>
        public CacheItem(string id, DateTime updated, T item)
        {
            Id = id;
            Updated = updated;
            Item = item;
            Created = DateTime.Now;
        }

        #region [Helpers]
        public bool IsItemGeneric()
        {
            if (Item != null && Item.GetType().IsGenericType)
                return true;
            
            return false;
        }

        public string GetItemTypeName()
        {
            if (Item != null)
                return Item.GetType().FullName ?? string.Empty;

            return string.Empty;
        }

        public Type? GetItemBaseType()
        {
            if (Item != null)
                return Item.GetType().BaseType;

            return null;
        }
        #endregion
    }

    /// <summary>
    /// File contents cache model using <see cref="WeakReference"/>.
    /// </summary>
    public class CacheFileWeak
    {
        public string Filename { get; private set; }
        public WeakReference? wrData { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheFileWeak"/> class.
        /// </summary>
        /// <param name="filename">full path to target</param>
        public CacheFileWeak(string filename)
        {
            Filename = filename;
        }

        /// <summary>
        /// Return the contents of the file.
        /// </summary>
        /// <remarks>No disk access is performed, unless a GarbageCollection has occurred in the interim.</remarks>
        public string GetText()
        {
            object? text = null;

            try
            {
                // If our weak reference is still viable then extract the target.
                if (wrData != null)
                    text = wrData.Target ?? string.Empty;

                // If the target returned non-null then our data is still in memory,
                // otherwise we'll need to read it in and insert into the cache again.
                if (text != null)
                    return text.ToString() ?? string.Empty;
                else
                    return ReadFile();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Read the contents of the file and creates a weak reference to the contents.
        /// </summary>
        string ReadFile()
        {
            try
            {
                string text = System.IO.File.ReadAllText(Filename);
                wrData = new WeakReference(text);
                return text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Read the contents of the file and creates a weak reference to the contents.
        /// </summary>
        async Task<string> ReadFileAsync()
        {
            try
            {
                string text = await System.IO.File.ReadAllTextAsync(Filename);
                wrData = new WeakReference(text);
                return text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        #region [Helpers]
        void TriggerGarbageCollection()
        {
            if (wrData != null)
            {
                int gen = GC.GetGeneration(wrData);
                GC.Collect(gen);
                GC.WaitForPendingFinalizers();
            }
        }
        #endregion
    }

}

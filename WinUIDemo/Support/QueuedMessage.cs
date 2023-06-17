using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace WinUIDemo;

/// <summary>
/// Support class for writing to a terminal or some other output.
/// </summary>
public static class QueuedMessage
{
    static int _pollFrequency = 10; // 100 times per second
    static bool _threadRunning = true;

    #region [Collection Options]
    // BlockingCollection represents a collection that allows for thread-safe adding and removal of data. 
    static BlockingCollection<Message> m_Collection = new BlockingCollection<Message>();

    // ConcurrentBags are useful for storing objects when ordering doesn't matter, and unlike sets, bags support duplicates. 
    static ConcurrentBag<Message> m_Bag = new ConcurrentBag<Message>();

    // ConcurrentQueue represents a thread-safe first in-first out (FIFO) collection.
    static ConcurrentQueue<Message> m_Queue = new ConcurrentQueue<Message>();

    // ConcurrentStack represents a thread-safe last in-first out (LIFO) collection.
    static ConcurrentStack<Message> m_Stack = new ConcurrentStack<Message>();

    // ConcurrentDictionary represents a thread-safe collection of key/value pairs that can be accessed by multiple threads.
    static ConcurrentDictionary<int, Message> m_Dictionary = new ConcurrentDictionary<int, Message>();
    #endregion

    static QueuedMessage()
    {
        Thread thread = new Thread(() => 
        {
            while (_threadRunning)
            {
                // NOTE: To use an enumerable with the collection you would call GetConsumingEnumerable()
                //foreach (var item in m_Collection.GetConsumingEnumerable()) {
                //    Debug.WriteLine($"Consuming: {item}");
                //}

                if (m_Collection.Count > 0)
                {
                    Message message;
                    if (m_Collection.TryTake(out message))
                    {
                        string formatted = $"[{message.level}] " + (message.time ? $"{DateTime.Now.ToString("hh:mm:ss.fff tt")} " : " ") + message.text;
                        Debug.WriteLine(formatted);
                    }
                    else
                    {
                        Debug.WriteLine($"WARNING: Unable to remove message from {m_Collection.GetType()}");
                    }
                }
                else
                {
                    Thread.Sleep(_pollFrequency);
                }
            }
            m_Collection.Dispose();
        });

        thread.Name = nameof(QueuedMessage);
        thread.Priority = ThreadPriority.Lowest;
        thread.IsBackground = true; // Background threads are automatically shutdown when the main thread closes.
        thread.Start();
    }

    public static void Write(string value, bool time = true)
    {
        if (!_threadRunning || !m_Collection.TryAdd(new Message(value, LogLevel.Info, time)))
            Debug.WriteLine("WARNING: Unable to add message to BlockingCollection!");
    }

    public static void Write(string value, LogLevel level, bool time = true)
    {
        if (!_threadRunning || !m_Collection.TryAdd(new Message(value, level, time)))
            Debug.WriteLine("WARNING: Unable to add message to BlockingCollection!");
    }

    public static void Dispose()
    {
        _threadRunning = false;
    }

    #region [Message Structure]
    private struct Message
    {
        public string text;
        public LogLevel level;
        public bool time;

        public Message(string value, LogLevel level, bool time)
        {
            this.text = value;
            this.level = level;
            this.time = time;
        }
    }
    #endregion
}





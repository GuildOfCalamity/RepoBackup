using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WinUIDemo.Models
{
    public interface IQueue<T>
    {
        T Buffer { get; set; }
        bool Enabled { get; set; }
        int Maximum { get; set; }
        int Count { get; }
    }

    public class SynchronizedQueue<T> : IQueue<T>
    {
        #region [Properties]
        private bool verbose = false;
        private bool enabled = true;
        private int maximum = 10;
        private Queue<T> buffer = new Queue<T>();
        private int occupiedBufferCount = 0;
        public event EventHandler<Dictionary<DateTime, string>> BufferEmptyEvent;
        public event EventHandler<Dictionary<DateTime, string>> BufferFullEvent;
        public event EventHandler<PropertyChangingEventArgs<bool>> EnabledChanging;
        public event EventHandler EnabledChanged;

        /// <summary>
        /// A property to indicate if the maximum allowed items in the <see cref="Queue{T}"/>.
        /// </summary>
        public int Maximum { get { return maximum; } set { maximum = value; } }

        /// <summary>
        /// Count property.
        /// </summary>
        public int Count { get { return buffer.Count; } }

        /// <summary>
        /// A property to indicate if the buffer is allowed to be used; has nothing to do with read/write cycle.
        /// </summary>
        public bool Enabled
        {
            get => enabled;
            set
            {   // Old way...
                /*
                if (enabled != value)
                {
                    PropertyChangingEventArgs<bool> e = new PropertyChangingEventArgs<bool>(value);
                    OnEnabledChanging(e);
                    if (e.Cancel) { return; }
                    enabled = value;
                    OnEnabledChanged(EventArgs.Empty);
                }
                */

                // New way...
                EventHelper.AssignProperty<bool>(ref enabled, value, OnEnabledChanging, OnEnabledChanged);
            }
        }

        /// <summary>
        /// The shared <see cref="Queue{T}"/> buffer used by producer and consumer threads.
        /// Reading from this property will activate a lock on the SynchronizedQueue object.
        /// Writing to this property will activate a lock on the SynchronizedQueue object.
        /// </summary>
        public T Buffer
        {
            get
            {   // Acquire lock on this object
                Monitor.Enter(this);

                if (verbose)
                    Debug.WriteLine($"{Thread.CurrentThread.Name} trying to read...");

                // If there is no data to read, place invoking thread in WaitSleepJoin state
                if (occupiedBufferCount == 0)
                {
                    BufferEmptyEvent?.Invoke(null, new() { { DateTime.Now, $"{Thread.CurrentThread.Name}: BufferEmptyEvent" } });
                    if (verbose)
                        ShowState($"> Buffer empty, {Thread.CurrentThread.Name} is waiting.");

                    Monitor.Wait(this); // enter WaitSleepJoin state
                }

                // Indicate that producer can store another value 
                // because consumer is about to retrieve a buffer value
                if (occupiedBufferCount > 0)
                    --occupiedBufferCount;

                // Sample the next element without removing it...
                if (verbose)
                    ShowState($"{Thread.CurrentThread.Name} reads {buffer.Peek()}");

                // Tell waiting thread (if there is one) to become ready to execute (Running state)
                Monitor.Pulse(this);

                // Get copy of buffer before releasing lock. It is possible
                // that the producer could be assigned the processor immediately
                // after the monitor is released and before the return statement
                // executes. In this case, the producer would assign a new value
                // to buffer before the return statement returns the value to the 
                // consumer. Thus, the consumer would receive the new value.
                // Making a copy of buffer and returning the copy ensures that
                // the consumer receives the proper value.
                T bufferCopy = buffer.Dequeue();

                // Release lock on this object
                Monitor.Exit(this);

                return bufferCopy;
            }
            set
            {   // Acquire lock for this object
                Monitor.Enter(this);

                if (verbose)
                    Debug.WriteLine($"{Thread.CurrentThread.Name} trying to write {value}...");

                // if there are no empty locations, place invoking thread in WaitSleepJoin state
                if (occupiedBufferCount == maximum)
                {
                    BufferFullEvent?.Invoke(null, new() { { DateTime.Now, $"{Thread.CurrentThread.Name}: BufferFullEvent" } });

                    if (verbose)
                        ShowState($"> Buffer full, {Thread.CurrentThread.Name} is waiting.");

                    // We could add a timeout to the Monitor.Wait to allow the
                    // setter to eventually add to our buffer but that could
                    // defeat the purpose of having a buffer limit.
                    Monitor.Wait(this); // enter WaitSleepJoin state
                }

                // Set new buffer value
                buffer.Enqueue(value);

                // Indicate consumer can retrieve another value 
                // because producer has just stored a buffer value
                ++occupiedBufferCount;

                if (verbose)
                    ShowState($"{Thread.CurrentThread.Name} writes {value}");

                // Tell waiting thread (if there is one) to become ready to execute (Running state)
                Monitor.Pulse(this);

                // Release lock on this object
                Monitor.Exit(this);
            }
        }
        #endregion

        /// <summary>
        /// Displays the current operation and buffer state.
        /// </summary>
        public void ShowState(string operation)
        {
            Debug.WriteLine(new string('=',39));
            Debug.WriteLine("{0,-45} Total={1,-6}", operation, occupiedBufferCount);
            Debug.WriteLine(new string('=', 39));
        }

        protected virtual void OnEnabledChanging(PropertyChangingEventArgs<bool> e)
        {
            EnabledChanging?.Invoke(this, e);
        }

        protected virtual void OnEnabledChanged(EventArgs e)
        {
            EnabledChanged?.Invoke(this, e);
        }

    }
}

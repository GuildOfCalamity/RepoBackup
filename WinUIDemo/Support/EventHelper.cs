using System;
using System.ComponentModel;

namespace WinUIDemo
{
    public static class EventHelper
    {
        public delegate void OnPropertyChangingEventHandler<T>(PropertyChangingEventArgs<T> e);
        public delegate void OnPropertyChangedEventHandler(EventArgs e);

        /// <summary>
        /// To be used in the setter of a property.
        /// set
        /// {
        ///     EventHelper.AssignProperty{string}(ref m_backing, value, OnValueChanging, OnValueChanged);
        /// }
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="oldValue">existing property</param>
        /// <param name="proposedValue">new value</param>
        /// <param name="onChanging"><see cref="EventHandler"/></param>
        /// <param name="onChanged"><see cref="EventHandler"/></param>
        public static void AssignProperty<T>(ref T oldValue, T proposedValue, OnPropertyChangingEventHandler<T> onChanging, OnPropertyChangedEventHandler onChanged)
        {
            //  Nothing to do if the new value is the same as the old value.
            if (object.Equals(oldValue, proposedValue))
                return;

            //  Invoke the OnChangingXXXX method, exit if subscribers canceled the assignment.
            PropertyChangingEventArgs<T> e = new PropertyChangingEventArgs<T>(proposedValue);
            onChanging.DynamicInvoke(e);
            if (e.Cancel)
                return;

            //  Proceed with assignment, then invoke the OnChangedXXXX method.
            oldValue = proposedValue;
            onChanged.DynamicInvoke(EventArgs.Empty);
        }
    }

    /// <summary>
    /// Inheriting from CancelEventArgs adds support for the Cancel property.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PropertyChangingEventArgs<T> : CancelEventArgs
    {
        public PropertyChangingEventArgs(T proposedValue)
        {
            m_ProposedValue = proposedValue;
        }

        private T m_ProposedValue;

        public T ProposedValue
        {
            get { return m_ProposedValue; }
        }
    }
}

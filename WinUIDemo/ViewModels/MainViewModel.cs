#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinUIDemo.ViewModels
{
    /// <summary>
    /// This is single purpose utility app, so we're not taking advantage of MVVM 
    /// but I've included the framework so that it's ready to be used if necessary.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        private string _systemStatus = "";
        public string SystemStatus
        {
            get { return _systemStatus; }
            set { _systemStatus = value; }
        }

        private bool _showFrame = false;
        public bool ShowFrame
        {
            get { return _showFrame; }
            set { _showFrame = value; }
        }

        public MainViewModel()
        {
        }
    }
}

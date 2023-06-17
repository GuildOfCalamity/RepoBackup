#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinUIDemo.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private string _systemStatus = "";
        public string SystemStatus
        {
            get { return _systemStatus; }
            set { _systemStatus = value; }
        }

        public MainViewModel()
        {
        }
    }
}

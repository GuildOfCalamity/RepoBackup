using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinUIDemo.Controls;

public class TextInputEventArgs : EventArgs
{
    public string Data { get; }

    public TextInputEventArgs(string textData)
    {
        Data = textData;
    }

}

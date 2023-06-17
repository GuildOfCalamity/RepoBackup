using System;
using Windows.UI;

namespace WinUIDemo.Models;

public class NamedColor
{
    public string KeyName { get; set; }
    public string HexCode { get; set; }
    public Color Color { get; set; }
    public override string ToString() => $"{KeyName} => {HexCode}";
}

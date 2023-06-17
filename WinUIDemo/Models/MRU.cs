using System;

namespace WinUIDemo;

public class MRU
{
    public string Folder { get; set; }
    public DateTime Time { get; set; }
    public int Count { get; set; }
    public MRU() { }
    public override string ToString()
    {
        return $"[{Count}] {Folder}";
    }
}

using System;

namespace WinUIDemo;

public class Settings
{
    public string Theme { get; set; }
    public string HomeRepoFolder { get; set; }
    public string HomeBufferFolder { get; set; }
    public string WorkRepoFolder { get; set; }
    public string WorkBufferFolder { get; set; }
    public bool ExplorerShell { get; set; }
    public bool FullInitialBackup { get; set; }
    public bool AtWork { get; set; }
    public int ThreadIndex { get; set; }
    public int StaleIndex { get; set; }
    public Settings() { }
    public override string ToString()
    {
        return $"[AtWork:{AtWork}] [StaleIndex:{StaleIndex}] [ExplorerShell:{ExplorerShell}] [FullInitialBackup:{FullInitialBackup}] [HomeRepoFolder:{HomeRepoFolder}] [WorkRepoFolder:{WorkRepoFolder}]";
    }
}

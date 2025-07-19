using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

using Windows.Storage;

namespace WinUIDemo;

public interface ISettingsManager
{
    public Settings LoadSettings();
    public void SaveSettings(Settings settings);
}

public class SettingsManager : ISettingsManager
{
    public Settings Config { get; private set; }

    public SettingsManager()
    {
        Config = LoadSettings();
    }

    /// <summary>
    /// Loads the application settings collection.
    /// </summary>
    public Settings LoadSettings()
    {
        string baseFolder = "";
        Settings result;
        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            if (File.Exists(Path.Combine(baseFolder, @"Settings.xml")))
            {
                var data = File.ReadAllText(Path.Combine(baseFolder, @"Settings.xml"));
                var serializer = new XmlSerializer(typeof(Settings));
                if (serializer != null)
                {
                    result = serializer.Deserialize(new StringReader(data)) as Settings ?? GenerateDefaultSettings();
                }
                else
                {
                    Debug.WriteLine($"LoadSettings: XmlSerializer was null.");
                    throw new Exception($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}: XmlSerializer was null.");
                }
            }
            else
            {
                // Create a default config if not found.
                result = GenerateDefaultSettings();
                SaveSettings(result);
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadSettings: {ex.Message}");
            //Debugger.Break();
            throw new Exception($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the application settings collection.
    /// If <see cref="Settings"/> is null then the local instance <see cref="Config"/> will be used.
    /// </summary>
    public void SaveSettings(Settings settings = null)
    {
        string baseFolder = "";

        try
        {
            if (App.IsPackaged)
                baseFolder = ApplicationData.Current.LocalFolder.Path;
            else
                baseFolder = Directory.GetCurrentDirectory();

            var serializer = new XmlSerializer(typeof(Settings));
            if (serializer != null)
            {
                var stringWriter = new StringWriter();
                if (settings == null)
                    serializer.Serialize(stringWriter, Config);
                else
                    serializer.Serialize(stringWriter, settings);
                var applicationData = stringWriter.ToString();
                File.WriteAllText(Path.Combine(baseFolder, @"Settings.xml"), applicationData);
            }
            else
            {
                Debug.WriteLine($"SaveSettings: XmlSerializer was null.");
                throw new Exception($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}: XmlSerializer was null.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SaveSettings: {ex.Message}");
            //Debugger.Break();
            throw new Exception($"{MethodBase.GetCurrentMethod()?.DeclaringType?.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new <see cref="Settings"/> object.
    /// </summary>
    /// <returns><see cref="Settings"/></returns>
    Settings GenerateDefaultSettings()
    {
        return new Settings
        {
            Theme = "Dark",
            AtWork = false,
            ExplorerShell = true,
            FullInitialBackup = false,
            RandomBackdrop = false,
            StaleIndex = 6,
            ThreadIndex = 1,
            HomeRepoFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos"),
            HomeBufferFolder = @"F:\RepoBackups",
            WorkRepoFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"source\repos"),
            WorkBufferFolder = @"D:\RepoBackups",
            ExcludeList = @"\.git,\.vs,\bin,\obj",
        };
    }
}

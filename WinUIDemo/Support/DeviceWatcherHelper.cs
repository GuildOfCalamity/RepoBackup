using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.UI.Dispatching;

using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;
using Windows.Devices.SerialCommunication;
using WinUIDemo.Models;

namespace WinUIDemo;

/// <summary>
/// USB device watcher.
/// </summary>
public class DeviceWatcherHelper : IDisposable
{
	const int max = 1000;
	static int count = 0;
	static bool isEnumerationComplete = false;
	static DeviceWatcher watcher = null;
	static DeviceInformation[] interfaces = new DeviceInformation[max]; // Most systems will have around 100 devices, so create a buffer with plenty of space.
	public static string StopStatus = null;

	static DispatcherQueue Dispatcher = null;
	public List<string> DeviceInterfacesOutputList = new List<string>();
	static ObservableCollection<LogEntry> LogMessages;

	public DeviceWatcherHelper(DispatcherQueue dispatcher, ref ObservableCollection<LogEntry> logMessages)
    {
		Dispatcher = dispatcher;
		LogMessages = logMessages;
	}

	/// <summary>
	/// This would normally be tied to a button click event.
	/// </summary>
	/// <returns>true if watcher was created and started, false otherwise</returns>
	public async Task<bool> WatchDevices()
	{
		try
		{
			if (Dispatcher == null)
				throw new ArgumentNullException($"Cannot continue, {nameof(Dispatcher)} was null.");

            if (LogMessages == null)
                throw new ArgumentNullException($"Cannot continue, {nameof(LogMessages)} was null.");

            watcher = DeviceInformation.CreateWatcher();
			
			// Add DeviceWatcher event handlers
			watcher.Added += watcherOnAdded;
			watcher.Removed += watcherOnRemoved;
			watcher.Updated += watcherOnUpdated;
			watcher.EnumerationCompleted += watcherOnEnumerationCompleted;
			watcher.Stopped += watcherOnStopped;
			watcher.Start();
			AddMessage("USB enumeration started", LogLevel.Debug);
			return true;

		}
		catch (ArgumentException)
		{
			//The ArgumentException gets thrown by FindAllAsync when the GUID isn't formatted properly
			//The only reason we're catching it here is because the user is allowed to enter GUIDs without validation
			//In normal usage of the API, this exception handling probably wouldn't be necessary when using known-good GUIDs 
			AddMessage("Caught ArgumentException. Failed to create watcher.", LogLevel.Warning);
			
			// Prevent hammering
			await Task.Delay(2000);

			return false;
		}
	}


	#region [Events]
	async void watcherOnAdded(DeviceWatcher sender, DeviceInformation deviceInterface)
	{
		if (count < max)
		{
			interfaces[count] = deviceInterface;
			count++;
		}

		if (isEnumerationComplete)
		{
			Dispatcher.TryEnqueue(() => { DisplayDeviceInterfaceArray(); });
		}
	}

	async void watcherOnUpdated(DeviceWatcher sender, DeviceInformationUpdate devUpdate)
	{
		int count2 = 0;
		foreach (DeviceInformation deviceInterface in interfaces)
		{
			if (count2 < count)
			{
				if (interfaces[count2].Id == devUpdate.Id)
				{
					//Update the element.
					interfaces[count2].Update(devUpdate);
				}

			}
			count2++;
		}

		Dispatcher.TryEnqueue(() => 
		{ 
			AddMessage($"Device updated. Count is now {count2}", LogLevel.Debug); 
			DisplayDeviceInterfaceArray();
		});
	}

	async void watcherOnRemoved(DeviceWatcher sender, DeviceInformationUpdate devUpdate)
	{
		int count2 = 0;
		//Convert interfaces array to a list (IList).
		List<DeviceInformation> interfaceList = new List<DeviceInformation>(interfaces);
		foreach (DeviceInformation deviceInterface in interfaces)
		{
			if (count2 < count)
			{
				if (interfaces[count2].Id == devUpdate.Id)
				{
					//Remove the element.
					interfaceList.RemoveAt(count2);
				}

			}
			count2++;
		}
		//Convert the list back to the interfaces array.
		interfaces = interfaceList.ToArray();
		count -= 1;

		AddMessage($"Device was removed. Count is now {count2}", LogLevel.Debug);
		Dispatcher.TryEnqueue(() => { DisplayDeviceInterfaceArray(); });
	}

	async void watcherOnEnumerationCompleted(DeviceWatcher sender, object args)
	{
		isEnumerationComplete = true;
		AddMessage("USB enumeration complete", LogLevel.Debug);
		Dispatcher.TryEnqueue(() =>	{ DisplayDeviceInterfaceArray(); });
	}

	async void watcherOnStopped(DeviceWatcher sender, object args)
	{
		if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Aborted)
		{
			StopStatus = "USB enumeration stopped unexpectedly. Click Watch to restart enumeration.";
			AddMessage(StopStatus, LogLevel.Warning);
		}
		else if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Stopped)
		{
			StopStatus = "You requested to stop the USB enumeration. Click Watch to restart enumeration.";
		}
	}
    #endregion

    #region [Helpers]
	/// <summary>
	/// Thread-safe ObservableCollection helper method.
	/// </summary>
	void AddMessage(string message, LogLevel severity)
	{
		if (!App.IsClosing)
			Dispatcher.TryEnqueue(() => { LogMessages.Insert(0, new LogEntry { Message = message, Severity = severity, Time = DateTime.Now }); });
	}

    async void DisplayDeviceInterfaceArray()
	{
		DeviceInterfacesOutputList.Clear();
		int count2 = 0;
		foreach (DeviceInformation deviceInterface in interfaces)
		{
			if (count2 < count) { DisplayDeviceInterface(deviceInterface); }
			count2++;
		}
	}

	async void DisplayDeviceInterface(DeviceInformation deviceInterface)
	{
		string item = "Name:" + deviceInterface.Name + ", IsEnabled:" + deviceInterface.IsEnabled + ", Id:" + deviceInterface.Id;

		if (!string.IsNullOrEmpty(deviceInterface.Name) && deviceInterface.Name == App.MachineName)
		{   // These are typically integrated chips, so we will ignore
			// them since it is not a device you can simply add or remove.
			return;
		}

		if (deviceInterface.IsEnabled)
		{
			DeviceInterfacesOutputList.Add(item);

            #region [Example of selective filtering]
            if (deviceInterface.Name.StartsWith("Cruzer Micro"))
			{
				AddMessage("Found USB ThumbDrive", LogLevel.Notice);
				var diProps = deviceInterface.Properties;
				EnclosureLocation el = deviceInterface.EnclosureLocation;
				if (el != null)
				{
					// If not null then we have found our physical USB device
					DeviceInterfacesOutputList.Add($"> Panel: {el.Panel}");
					DeviceInterfacesOutputList.Add($"> InDock: {el.InDock}");
					DeviceInterfacesOutputList.Add($"> InLid: {el.InLid}");
				}
				foreach (KeyValuePair<string, object> kvp in diProps)
				{
					DeviceInterfacesOutputList.Add($"Cruzer Key: {kvp.Key}");
				}
			}

			if (deviceInterface.Name.StartsWith("Galaxy A"))
			{
				AddMessage("Found Samsung Smartphone", LogLevel.Notice);
				var diProps = deviceInterface.Properties;
				foreach (KeyValuePair<string, object> kvp in diProps)
				{
					DeviceInterfacesOutputList.Add($"Samsung Key: {kvp.Key}");
				}
			}
            #endregion
        }
    }

    /// <summary>
    /// Calls https://www.catalog.update.microsoft.com/Search.aspx using the supplied parameters.
    /// To search KnowledgeBase use https://www.catalog.update.microsoft.com/Search.aspx?q=KB1234567
    /// </summary>
    /// <example>
    /// LookupDevice("", "", @"\\?\USB#VID_0A12&PID_0001#6&268a6ccc&0&3#{92383b0e-f90e-4ac9-8d44-8c2d0d0ebda2}");
    /// LookupDevice("", "", @"\\?\HID#VID_046D&PID_C534&MI_00#8&2f0241a8&0&0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}");
    /// </example>
    public async void LookupDevice(string VID = "VID_0A12", string PID = "PID_0001", string fullID = "")
	{
		try
		{
            // USB Device /* \\?\USB#VID_04B8&PID_0202# */
            if (!string.IsNullOrEmpty(fullID) && fullID.ToUpper().StartsWith(@"\\?\USB"))
            {
                VID = fullID.Substring(fullID.IndexOf("#") + 1, (fullID.IndexOf("&") - fullID.IndexOf("#")) - 1);
				PID = fullID.Substring(fullID.IndexOf("&") + 1, (fullID.IndexOfNth('#', 2) - fullID.IndexOf("&")) - 1);
                var uri = new Uri($@"https://www.catalog.update.microsoft.com/Search.aspx?q=USB\{VID}&{PID}");
                var success = await Windows.System.Launcher.LaunchUriAsync(uri);
                if (!success) { AddMessage($"Failed to launch URI", LogLevel.Warning); }
            }
            // Human Interface Device /* \\?\HID#VID_046D&PID_C534& */
            else if (!string.IsNullOrEmpty(fullID) && fullID.ToUpper().StartsWith(@"\\?\HID"))
            {
                VID = fullID.Substring(fullID.IndexOf("#") + 1, (fullID.IndexOf("&") - fullID.IndexOf("#")) - 1);
                PID = fullID.Substring(fullID.IndexOf("&") + 1, (fullID.IndexOfNth('&', 2) - fullID.IndexOf("&")) - 1);
                var uri = new Uri($@"https://www.catalog.update.microsoft.com/Search.aspx?q=HID\{VID}&{PID}");
                var success = await Windows.System.Launcher.LaunchUriAsync(uri);
                if (!success) { AddMessage($"Failed to launch URI", LogLevel.Warning); }
            }
            // Bluetooth Device /* \\?\BTHENUM#{0000110e-0000-1000-8000-00805f9b34fb} */
            else if (!string.IsNullOrEmpty(fullID) && fullID.ToUpper().StartsWith(@"\\?\BTHENUM"))
            {
                // These entries concern the Bluetooth Enumerator Service (BthEnum) in Windows,
                // which typically points to a location in the registry, e.g.
                // "\\?\bthenum#{0000110b-0000-1000-8000-00805f9b34fb}_vid&000103e0_pid&300a#{6994ad04-93ef-11d0-a3cc-00a0c9223196}"
                // Would be found here: [HKCU\SOFTWARE\Microsoft\Internet Explorer\LowRegistry\Audio\PolicyConfig\PropertyStore]
                // and indicates that Zoom will use this as an audio device...
                // \Device\HarddiskVolume2\Users\Name\AppData\Roaming\Zoom\bin\Zoom.exe
                AddMessage($"Ignoring BluetoothEnumeratorService", LogLevel.Warning);
                return;
			}
            // Software Device (via Hardware) /* \\?\SWD#PRINTENUM#{2F6FCD66-B2F6-4D0D-AE8E-30653E76406E} */
            else if (!string.IsNullOrEmpty(fullID) && fullID.ToUpper().StartsWith(@"\\?\SWD"))
            {
                // These entries are related to a software device such as a shared printer and its network connection.
                // The format is typically followed by #PRINTENUM (Printer Enumerator Service).
                AddMessage($"Ignoring PrinterEnumeratorService", LogLevel.Warning);
                return;
            }
        }
        catch (Exception ex)
		{
            AddMessage($"Could not route: {ex.Message}", LogLevel.Warning);
        }
    }
    #endregion

    public void Dispose()
	{
		try
		{
			if (watcher.Status == Windows.Devices.Enumeration.DeviceWatcherStatus.Stopped)
			{
				StopStatus = "The enumeration is already stopped.";
			}
			else
			{
				watcher.Stop();
				AddMessage("Watcher stopped", LogLevel.Debug);
				
				// Remove event handlers
				watcher.Added -= watcherOnAdded;
				watcher.Removed -= watcherOnRemoved;
				watcher.Updated -= watcherOnUpdated;
				watcher.EnumerationCompleted -= watcherOnEnumerationCompleted;
				watcher.Stopped -= watcherOnStopped;
			}
		}
		catch (ArgumentException)
		{
			AddMessage("Caught ArgumentException. Failed to stop watcher.", LogLevel.Warning);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Windows.Storage.FileProperties;
using Windows.Storage.Provider;
using Windows.Storage;

namespace WinUIDemo.Support;

public enum InvalidFilenameError
{
    None = 0,
    EmptyOrAllWhitespace,
    ContainsLeadingSpaces,
    ContainsTrailingSpaces,
    ContainsInvalidCharacters,
    InvalidOrNotAllowed,
    TooLong,
}

public enum LineEnding
{
    CrLf,
    Cr,
    Lf
}

/// <summary>
/// Class that supports packaged and unpackaged file IO.
/// </summary>
public static class PackagedFileSystemUtility
{
    //static readonly Windows.ApplicationModel.Resources.ResourceLoader ResourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();
    const string WslRootPath = "\\\\wsl$\\"; // Linux pathing
	const string BrowserFilePath = @"file:///";
    const ulong SizeLimit = 1024 * 1024; // 1 MB

	#region [Public Methods]
	public static bool IsFilenameValid(string filename, out InvalidFilenameError error)
	{
		if (filename.Length > 255)
		{
			error = InvalidFilenameError.TooLong;
			return false;
		}

		if (string.IsNullOrWhiteSpace(filename))
		{
			error = InvalidFilenameError.EmptyOrAllWhitespace;
			return false;
		}

		// Although shell supports file with leading spaces, explorer and file picker does not
		// So we treat it as invalid file name as well
		if (filename.StartsWith(" "))
		{
			error = InvalidFilenameError.ContainsLeadingSpaces;
			return false;
		}

		if (filename.EndsWith(" "))
		{
			error = InvalidFilenameError.ContainsTrailingSpaces;
			return false;
		}

		var illegalChars = Path.GetInvalidFileNameChars();
		if (filename.Any(c => illegalChars.Contains(c)))
		{
			error = InvalidFilenameError.ContainsInvalidCharacters;
			return false;
		}

		if (filename.EndsWith(".") || !Extensions.ValidWindowsFileNames.IsMatch(filename))
		{
			error = InvalidFilenameError.InvalidOrNotAllowed;
			return false;
		}

		error = InvalidFilenameError.None;
		return true;
	}

	public static bool IsFullPath(string path)
	{
		return !String.IsNullOrWhiteSpace(path)
			   && path.IndexOfAny(System.IO.Path.GetInvalidPathChars().ToArray()) == -1
			   && Path.IsPathRooted(path)
			   && !Path.GetPathRoot(path).Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
	}

	public static String GetAbsolutePath(String basePath, String path)
	{
		String finalPath;
		if (!Path.IsPathRooted(path) || "\\".Equals(Path.GetPathRoot(path)))
		{
			if (path.StartsWith(Path.DirectorySeparatorChar.ToString()))
			{
				finalPath = Path.Combine(Path.GetPathRoot(basePath), path.TrimStart(Path.DirectorySeparatorChar));
			}
			else
			{
				finalPath = Path.Combine(basePath, path);
			}
		}
		else
		{
			finalPath = path;
		}

		// Resolves any internal "..\" to get the true full path.
		return Path.GetFullPath(finalPath);
	}

	public static async Task<StorageFile> OpenFileFromCommandLine(string dir, string args)
	{
		string path = null;

		try
		{
			args = ReplaceEnvironmentVariables(args);
			path = GetAbsolutePathFromCommandLine(dir, args, App.AppName);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] Failed to parse command line: {args} with Exception: {ex.Message}");
			PackagedLoggingService.LogException(ex);
		}

		if (string.IsNullOrEmpty(path))
		{
			return null;
		}

		PackagedLoggingService.Log($"[{nameof(PackagedFileSystemUtility)}] OpenFileFromCommandLine: {path}", LogLevel.Info);

		return await GetFile(path);
	}

	public static string GetAbsolutePathFromCommandLine(string dir, string args, string appName)
	{
		if (string.IsNullOrEmpty(args)) return null;

		args = args.Trim();

		args = RemoveExecutableNameOrPathFromCommandLineArgs(args, appName);

		if (string.IsNullOrEmpty(args))
		{
			return null;
		}

		string path = args;

		// Get first quoted string if any
		if (path.StartsWith("\"") && path.Length > 1)
		{
			var index = path.IndexOf('\"', 1);
			if (index == -1) return null;
			path = args.Substring(1, index - 1);
		}

		if (dir.StartsWith(WslRootPath))
		{
			if (path.StartsWith('/'))
			{
				var distroRootPath = dir.Substring(0, dir.IndexOf('\\', WslRootPath.Length) + 1);
				var fullPath = distroRootPath + path.Trim('/').Replace('/', Path.DirectorySeparatorChar);
				if (IsFullPath(fullPath)) return fullPath;
			}
		}

		// Replace all forward slash with platform supported directory separator 
		path = path.Trim('/').Replace('/', Path.DirectorySeparatorChar);

		if (IsFullPath(path))
		{
			return path;
		}

		if (path.StartsWith(".\\"))
		{
			path = dir + Path.DirectorySeparatorChar + path.Substring(2, path.Length - 2);
		}
		else if (path.StartsWith("..\\"))
		{
			path = GetAbsolutePath(dir, path);
		}
		else
		{
			path = dir + Path.DirectorySeparatorChar + path;
		}

		return path;
	}

	public static string SanitizeBrowserFilePath(string path)
	{
		if (string.IsNullOrEmpty(path)) 
			return null;

		path = path.Trim().Replace("\"", "");

		if (path.StartsWith(BrowserFilePath))
		{
			var cleanPath = path.Substring(BrowserFilePath.Length);
			path = cleanPath.Replace('/', Path.DirectorySeparatorChar);
		}
		else
		{
			// Replace all forward slash with platform supported directory separator 
			path = path.Trim('/').Replace('/', Path.DirectorySeparatorChar);
		}

		if (IsFullPath(path))
		{
			return path;
		}

		if (path.StartsWith(".\\"))
		{
			path = path.Substring(2, path.Length - 2);
		}
		else if (path.StartsWith("..\\"))
		{
			path = path.Substring(3, path.Length - 3);
		}

		return path;
	}

	public static async Task<BasicProperties> GetFileProperties(StorageFile file)
	{
		return await file.GetBasicPropertiesAsync();
	}

	public static async Task<long> GetDateModified(StorageFile file)
	{
		var properties = await GetFileProperties(file);
		var dateModified = properties.DateModified;
		return dateModified.ToFileTime();
	}

	/// <summary>
	/// Determines if a file is currently read-only.
	/// </summary>
	/// <param name="file"><see cref="StorageFile"/></param>
	/// <returns>true if read-only, false otherwise</returns>
	public static bool IsFileReadOnly(StorageFile file)
	{
		return (file.Attributes & Windows.Storage.FileAttributes.ReadOnly) != 0;
	}

	/// <summary>
	/// Determines if a file is currently locked by another process.
	/// </summary>
	/// <param name="file"><see cref="StorageFile"/></param>
	/// <returns>true if available, false otherwise</returns>
	public static async Task<bool> IsFileWritable(StorageFile file)
	{
		try
		{
			using (var stream = await file.OpenStreamForWriteAsync()) { }
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns the supplied filePath as a <see cref="StorageFile"/>.
	/// </summary>
	public static async Task<StorageFile> GetFile(string filePath)
	{
		try
		{
			return await StorageFile.GetFileFromPathAsync(filePath);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Reads the filePath and returns it as a <see cref="TextFile"/>.
	/// </summary>
	public static async Task<TextFile> ReadFile(string filePath, bool ignoreFileSizeLimit, Encoding encoding)
	{
		StorageFile file = await GetFile(filePath);
		return file == null ? null : await ReadFile(file, ignoreFileSizeLimit, encoding);
	}

	/// <summary>
	/// Reads the filePath and returns it as a <see cref="TextFile"/>.
	/// </summary>
	public static async Task<TextFile> ReadFile(StorageFile file, bool ignoreFileSizeLimit, Encoding encoding = null)
	{
		var fileProperties = await file.GetBasicPropertiesAsync();

		if (!ignoreFileSizeLimit && fileProperties.Size > SizeLimit)
		{
			throw new Exception($"FileSizeLimit exceeded. Current limit is {SizeLimit.ToFileSize()}");
		}

		Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		string text;
		var bom = new byte[4];

		using (var inputStream = await file.OpenReadAsync())
		using (var stream = inputStream.AsStreamForRead())
		{
			stream.Read(bom, 0, 4); // Read BOM values
			stream.Position = 0; // Reset stream position

			var reader = CreateStreamReader(stream, bom, encoding);

			string PeekAndRead()
			{
				if (encoding == null)
				{
					reader.Peek();
					encoding = reader.CurrentEncoding;
				}
				var str = reader.ReadToEnd();
				reader.Close();
				return str;
			}

			try
			{
				text = PeekAndRead();
			}
			catch (DecoderFallbackException)
			{
				stream.Position = 0; // Reset stream position
				encoding = GetFallBackEncoding();
				reader = new StreamReader(stream, encoding);
				text = PeekAndRead();
			}
		}

		encoding = FixUtf8Bom(encoding, bom);
		return new TextFile(text, encoding, GetLineEndingTypeFromText(text), fileProperties.DateModified.ToFileTime());
	}

	/// <summary>
	/// If an error occurs or the stream is empty the <see cref="Encoding"/> object will be set to null.
	/// </summary>
	/// <returns>true if file encoding determined, false otherwise</returns>
	public static bool TryGuessEncoding(Stream stream, out Encoding encoding)
	{
		encoding = null;

		try
		{
			if (stream.Length > 0) // We do not care about empty file
			{
				Debug.WriteLine("Unable to detect encoding: empty stream");
			}
			else
			{
				encoding = DetermineFileEncoding(stream);
				return true;
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"TryGuessEncoding: {ex.Message}");
		}

		return false;
	}

	/// <summary>
	/// Save text to a file with requested encoding
	/// Exception will be thrown if not succeeded
	/// Exception should be caught and handled by caller
	/// </summary>
	/// <param name="text"></param>
	/// <param name="encoding"></param>
	/// <param name="file"></param>
	/// <returns></returns>
	public static async Task WriteToFile(string text, Encoding encoding, StorageFile file)
	{
		bool usedDeferUpdates = true;

		try
		{
			// Prevent updates to the remote version of the file until we
			// finish making changes and call CompleteUpdatesAsync.
			CachedFileManager.DeferUpdates(file);
		}
		catch (Exception)
		{
			// If DeferUpdates fails, just ignore it and try to save the file anyway
			usedDeferUpdates = false;
		}

		// Write to file
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		try
		{
			if (IsFileReadOnly(file) || !await IsFileWritable(file))
			{
				// For file(s) dragged into Notepads, they are read-only
				// StorageFile API won't work on read-only files but can be written by Win32 PathIO API (exploit?)
				// In case the file is actually read-only, WriteBytesAsync will throw UnauthorizedAccessException
				var content = encoding.GetBytes(text);
				var result = encoding.GetPreamble().Concat(content).ToArray();
				await PathIO.WriteBytesAsync(file.Path, result);
			}
			else // Use StorageFile API to save 
			{
				using (var stream = await file.OpenStreamForWriteAsync())
				using (var writer = new StreamWriter(stream, encoding))
				{
					stream.Position = 0;
					await writer.WriteAsync(text);
					await writer.FlushAsync();
					stream.SetLength(stream.Position); // Truncate
				}
			}
		}
		finally
		{
			if (usedDeferUpdates)
			{
				// Let Windows know that we're finished changing the file so the
				// other app can update the remote version of the file.
				FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
				if (status != FileUpdateStatus.Complete)
				{
					// Track FileUpdateStatus here to better understand the failed scenarios
					// File name, path and content are not included to respect/protect user privacy
					//Analytics.TrackEvent("CachedFileManager_CompleteUpdatesAsync_Failed", new Dictionary<string, string>()
					//{
					//    { "FileUpdateStatus", nameof(status) }
					//});
				}
			}
		}
	}

	public static async Task<StorageFolder> GetOrCreateAppFolder(string folderName)
	{
		if (App.IsPackaged)
		{
			StorageFolder localFolder = ApplicationData.Current.LocalFolder;
			return await localFolder.CreateFolderAsync(folderName, CreationCollisionOption.OpenIfExists);
		}
		else
		{
			return await StorageFolder.GetFolderFromPathAsync(Path.Combine(System.AppContext.BaseDirectory, folderName));
		}
	}

	public static async Task<StorageFile> CreateFile(StorageFolder folder, string fileName, CreationCollisionOption option = CreationCollisionOption.ReplaceExisting)
	{
		return await folder.CreateFileAsync(fileName, option);
	}

	public static async Task<bool> FileExists(StorageFile file)
	{
		try
		{
			using (var stream = await file.OpenStreamForReadAsync()) { }
			return true;
		}
		catch (FileNotFoundException)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] File was not found: '{file.Path}'");
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] Failed to check if file '{file.Path}' exists: {ex.Message}");
			PackagedLoggingService.LogException(ex);
			return true; // Probably a permissions issue, so we'll return true since the file would exist.
		}
	}

	/// <summary>
	/// Utilizes the <see cref="System.IO.StreamReader"/> to determine the file's <see cref="System.Text.Encoding"/>.
	/// </summary>
	/// <param name="path">path to the file</param>
	/// <returns><see cref="System.Text.Encoding"/></returns>
	public static System.Text.Encoding DetermineFileEncoding(string path)
	{
		try
		{
			System.IO.FileStream fs = new System.IO.FileStream(path, System.IO.FileMode.Open);
			System.IO.StreamReader sr = new System.IO.StreamReader(fs);
			System.Text.Encoding coding = sr.CurrentEncoding;
			fs.Close(); fs.Dispose();
			sr.Close(); sr.Dispose();
			return coding;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] Failed to determine file encoding: {ex.Message}");
			PackagedLoggingService.LogException(ex);
			return System.Text.Encoding.Default;
		}
	}

	/// <summary>
	/// Utilizes the <see cref="System.IO.StreamReader"/> to determine the file's <see cref="System.Text.Encoding"/>.
	/// </summary>
	/// <param name="fs"><see cref="System.IO.Stream"/></param>
	/// <returns><see cref="System.Text.Encoding"/></returns>
	public static System.Text.Encoding DetermineFileEncoding(Stream fs)
	{
		try
		{
			System.IO.StreamReader sr = new System.IO.StreamReader(fs);
			System.Text.Encoding coding = sr.CurrentEncoding;
			sr.Close(); sr.Dispose();
			return coding;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] Failed to determine file encoding: {ex.Message}");
			PackagedLoggingService.LogException(ex);
			return System.Text.Encoding.Default;
		}
	}
	#endregion

	#region [Private Methods]
	static string ReplaceEnvironmentVariables(string args)
	{
		if (args.Contains("%homepath%", StringComparison.OrdinalIgnoreCase))
		{
			args = args.Replace("%homepath%",
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				StringComparison.OrdinalIgnoreCase);
		}

		if (args.Contains("%localappdata%", StringComparison.OrdinalIgnoreCase))
		{
			args = args.Replace("%localappdata%",
				UserDataPaths.GetDefault().LocalAppData,
				StringComparison.OrdinalIgnoreCase);
		}

		if (args.Contains("%temp%", StringComparison.OrdinalIgnoreCase))
		{
			args = args.Replace("%temp%",
				(string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Environment",
				"TEMP",
				Environment.GetEnvironmentVariable("temp")),
				StringComparison.OrdinalIgnoreCase);
		}

		if (args.Contains("%tmp%", StringComparison.OrdinalIgnoreCase))
		{
			args = args.Replace("%tmp%",
				(string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Environment",
				"TEMP",
				Environment.GetEnvironmentVariable("tmp")),
				StringComparison.OrdinalIgnoreCase);
		}

		return Environment.ExpandEnvironmentVariables(args);
	}
	static string RemoveExecutableNameOrPathFromCommandLineArgs(string args, string appName)
	{
		if (!args.StartsWith('\"'))
		{
			// From Windows Command Line
			// notepads <file> ...
			// notepads.exe <file>

			if (args.StartsWith($"{appName}-Dev.exe",
				StringComparison.OrdinalIgnoreCase))
			{
				args = args.Substring($"{appName}-Dev.exe".Length);
			}

			if (args.StartsWith($"{appName}.exe",
				StringComparison.OrdinalIgnoreCase))
			{
				args = args.Substring($"{appName}.exe".Length);
			}

			if (args.StartsWith($"{appName}-Dev",
				StringComparison.OrdinalIgnoreCase))
			{
				args = args.Substring($"{appName}-Dev".Length);
			}

			if (args.StartsWith(appName,
				StringComparison.OrdinalIgnoreCase))
			{
				args = args.Substring(appName.Length);
			}
		}
		else if (args.StartsWith('\"') && args.Length > 1)
		{
			// From PowerShell or run
			// "notepads" <file>
			// "notepads.exe" <file>
			// "<app-install-path><app-name>.exe"  <file> ...
			var index = args.IndexOf('\"', 1);
			if (index == -1) return null;
			if (args.Length == index + 1) return null;
			args = args.Substring(index + 1);
		}
		else
		{
			return null;
		}

		args = args.Trim();
		return args;
	}

	/// <summary>
	/// Analyze the Byte Order Mark
	/// </summary>
	static bool HasBom(byte[] bom)
	{
		if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76)
			return true; // Encoding.UTF7
		if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
			return true; // Encoding.UTF8
		if (bom[0] == 0xff && bom[1] == 0xfe)
			return true; // Encoding.Unicode
		if (bom[0] == 0xfe && bom[1] == 0xff)
			return true; // Encoding.BigEndianUnicode
		if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff)
			return true; // Encoding.UTF32

		return false;
	}

	static Encoding FixUtf8Bom(Encoding encoding, byte[] bom)
	{
		if (encoding is UTF8Encoding)
		{
			// UTF8 with BOM - UTF-8-BOM
			// UTF8 byte order mark is: 0xEF,0xBB,0xBF
			if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
			{
				encoding = new UTF8Encoding(true);
			}
			// UTF8 no BOM
			else
			{
				encoding = new UTF8Encoding(false);
			}
		}

		return encoding;
	}

	static Encoding GetFallBackEncoding()
	{
		return new UTF8Encoding(false);
	}

	static StreamReader CreateStreamReader(Stream stream, byte[] bom, Encoding encoding = null)
	{
		StreamReader reader;
		if (encoding != null)
		{
			reader = new StreamReader(stream, encoding);
		}
		else
		{
			if (HasBom(bom))
			{
				reader = new StreamReader(stream);
			}
			else // No BOM, need to guess or use default decoding set by user
			{
				reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
			}
		}
		return reader;
	}

	internal static async Task DeleteFile(string filePath, StorageDeleteOption deleteOption = StorageDeleteOption.PermanentDelete)
	{
		try
		{
			var file = await GetFile(filePath);
			if (file != null)
			{
				await file.DeleteAsync(deleteOption);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[{nameof(PackagedFileSystemUtility)}] Failed to delete file: {filePath}, Exception: {ex.Message}");
			PackagedLoggingService.LogException(ex);
		}
	}
	#endregion

    #region [Line Ending Helpers]
    public static LineEnding GetLineEndingTypeFromText(string text)
    {
        if (text.Contains("\r\n"))
        {
            return LineEnding.CrLf;
        }
        else if (text.Contains("\r"))
        {
            return LineEnding.Cr;
        }
        else if (text.Contains("\n"))
        {
            return LineEnding.Lf;
        }
        else
        {
            return LineEnding.CrLf;
        }
    }

    public static string GetLineEndingDisplayText(LineEnding lineEnding)
    {
        switch (lineEnding)
        {
            case LineEnding.CrLf:
                return "Windows (CRLF)";
            case LineEnding.Cr:
                return "Macintosh (CR)";
            case LineEnding.Lf:
                return "Unix (LF)";
            default:
                return "Windows (CRLF)";
        }
    }

    public static string GetLineEndingName(LineEnding lineEnding)
    {
        string lineEndingName = "CRLF";

        switch (lineEnding)
        {
            case LineEnding.CrLf:
                lineEndingName = "CRLF";
                break;
            case LineEnding.Cr:
                lineEndingName = "CR";
                break;
            case LineEnding.Lf:
                lineEndingName = "LF";
                break;
        }

        return lineEndingName;
    }

    public static LineEnding GetLineEndingByName(string name)
    {
        LineEnding lineEnding = LineEnding.CrLf;

        switch (name.ToUpper())
        {
            case "CRLF":
                lineEnding = LineEnding.CrLf;
                break;
            case "CR":
                lineEnding = LineEnding.Cr;
                break;
            case "LF":
                lineEnding = LineEnding.Lf;
                break;
        }

        return lineEnding;
    }

    public static string ApplyLineEnding(string text, LineEnding lineEnding)
    {
        if (lineEnding == LineEnding.Cr)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r");
        }
        else if (lineEnding == LineEnding.CrLf)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }
        else // LF
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        return text;
    }
    #endregion
}

/// <summary>
/// A class that represents a text file, its contents as UTF-16 code units, and its encoding.
/// </summary>
public class TextFile
{
    public string Content { get; set; }
    public Encoding Encoding { get; set; }
    public LineEnding LineEnding { get; set; }
    public long DateModifiedFileTime { get; set; }

    public TextFile(string content, Encoding encoding, LineEnding lineEnding, long dateModifiedFileTime = -1)
    {
        Content = content;
        Encoding = encoding;
        LineEnding = lineEnding;
        DateModifiedFileTime = dateModifiedFileTime;
    }
}

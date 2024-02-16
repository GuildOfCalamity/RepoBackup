#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using Path = System.IO.Path;

namespace WinUIDemo;

/// <summary>
/// A static class of common helper methods.
/// </summary>
public static class Extensions
{
    #region [Miscellaneous]
    /// <summary>
    /// I created this to show what controls are members of <see cref="Microsoft.UI.Xaml.FrameworkElement"/>.
    /// </summary>
    public static void FindControlsInheritingFromFrameworkElement()
    {
        var controlAssembly = typeof(Microsoft.UI.Xaml.Controls.Control).GetTypeInfo().Assembly;
        var controlTypes = controlAssembly.GetTypes()
            .Where(type => type.Namespace == "Microsoft.UI.Xaml.Controls" &&
            typeof(Microsoft.UI.Xaml.FrameworkElement).IsAssignableFrom(type));

        foreach (var controlType in controlTypes)
        {
            Debug.WriteLine($"[FrameworkElement] {controlType.FullName}");
        }
    }

    /// <summary>
    /// Over-engineered version of Thread.Sleep().
    /// During the WaitOne call we do not care about receiving a signal.
    /// </summary>
    /// <param name="msTime"></param>
    public static void ThreadSleep(int msTime)
    {
        if (msTime <= 0) { return; }
        new System.Threading.AutoResetEvent(false).WaitOne(msTime);
        // -Or-
        //new System.Threading.ManualResetEvent(false).WaitOne(msTime);
    }

    /// <summary>
    /// Provides a JSON object for HttpProgress using our <see cref="WinUIDemo.JsonBuilder"/> wrapper.
    /// </summary>
    public static Windows.Data.Json.JsonObject ToJsonObject(this Windows.Web.Http.HttpProgress p)
    {
        var builder = new JsonBuilder("HttpProgress");
        builder.AddString(nameof(p.Stage), p.Stage);
        builder.AddNumber(nameof(p.Retries), p.Retries);
        builder.AddNumber(nameof(p.BytesSent), p.BytesSent);
        builder.AddNumber(nameof(p.TotalBytesToSend), p.TotalBytesToSend);
        builder.AddNumber(nameof(p.BytesReceived), p.BytesReceived);
        builder.AddNumber(nameof(p.TotalBytesToReceive), p.TotalBytesToReceive);
        return builder.GetJsonObject();
    }

    /// <summary>
    /// Provides a JSON object for DateTime using our <see cref="WinUIDemo.JsonBuilder"/> wrapper.
    /// Windows.Data.Json.JsonObject? example = DateTime.Now.ToJsonObject();
    /// </summary>
    public static Windows.Data.Json.JsonObject ToJsonObject(this DateTime dt)
    {
        var builder = new JsonBuilder("DateTime");
        builder.AddString(nameof(dt.Day), dt.Day);
        builder.AddString(nameof(dt.DayOfWeek), dt.DayOfWeek);
        builder.AddNumber(nameof(dt.DayOfYear), dt.DayOfYear);
        builder.AddNumber(nameof(dt.Year), dt.Year);
        builder.AddNumber(nameof(dt.Month), dt.Month);
        builder.AddNumber(nameof(dt.Hour), dt.Hour);
        builder.AddNumber(nameof(dt.Minute), dt.Minute);
        builder.AddNumber(nameof(dt.Second), dt.Second);
        builder.AddNumber(nameof(dt.Millisecond), dt.Millisecond);
        return builder.GetJsonObject();
    }

    /// <summary>
    /// Provides a JSON object for <see cref="WinUIDemo.Settings"/> using our <see cref="WinUIDemo.JsonBuilder"/> wrapper.
    /// Windows.Data.Json.JsonObject? example = Config.ToJsonObject();
    /// </summary>
    public static Windows.Data.Json.JsonObject ToJsonObject(this WinUIDemo.Settings s)
    {
        var builder = new JsonBuilder("Settings");
        builder.AddString(nameof(s.Theme), s.Theme);
        builder.AddString(nameof(s.HomeRepoFolder), s.HomeRepoFolder);
        builder.AddString(nameof(s.HomeBufferFolder), s.HomeBufferFolder);
        builder.AddString(nameof(s.WorkRepoFolder), s.WorkRepoFolder);
        builder.AddString(nameof(s.WorkBufferFolder), s.WorkBufferFolder);
        builder.AddBoolean(nameof(s.ExplorerShell), s.ExplorerShell);
        builder.AddBoolean(nameof(s.FullInitialBackup), s.FullInitialBackup);
        builder.AddBoolean(nameof(s.AtWork), s.AtWork);
        builder.AddNumber(nameof(s.ThreadIndex), s.ThreadIndex);
        builder.AddNumber(nameof(s.StaleIndex), s.StaleIndex);
        return builder.GetJsonObject();
    }

    /// <summary>
    /// Returns a <see cref="string"/> representation of a <see cref="Guid"/> only made of uppercase letters
    /// </summary>
    /// <param name="guid">The input <see cref="Guid"/> to process</param>
    /// <returns>A <see cref="string"/> representation of <paramref name="guid"/> only made up of letters in the [A-Z] range</returns>
    [Pure]
    public static string ToUpper(this Guid guid)
    {
        return new string((
            from c in guid.ToString("N")
            let l = char.IsDigit(c) ? (char)('G' + c - '0') : c
            select l).ToArray());
    }

    /// <summary>
    /// Determines if the specified exception is un-recoverable.
    /// </summary>
    /// <returns>true if the process cannot be recovered from, false otherwise</returns>
    public static bool IsCritical(this Exception exception)
    {
        return (exception is OutOfMemoryException) ||
               (exception is StackOverflowException) ||
               (exception is AccessViolationException) ||
               (exception is ThreadAbortException);
    }

    /// <summary>
    /// Tries to get a boxed <typeparamref name="T"/> value from an input <see cref="object"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of value to try to unbox.</typeparam>
    /// <param name="obj">The input <see cref="object"/> instance to check.</param>
    /// <param name="value">The resulting <typeparamref name="T"/> value, if <paramref name="obj"/> was in fact a boxed <typeparamref name="T"/> value.</param>
    /// <returns><see langword="true"/> if a <typeparamref name="T"/> value was retrieved correctly, <see langword="false"/> otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnbox<T>(this object obj, out T value) where T : struct
    {
        if (obj.GetType() == typeof(T))
        {
            value = Unsafe.Unbox<T>(obj);
            return true;
        }
        value = default;
        return false;
    }

    #endregion

    #region [Maths]
    internal const double EpsilonDouble = 2.2204460492503131E-15;
    internal const float EpsilonFloat = 1.175494351E-38F;

    /// <summary>
    /// Clamping function for any value of type <see cref="IComparable{T}"/>.
    /// </summary>
    /// <param name="val">initial value</param>
    /// <param name="min">lowest range</param>
    /// <param name="max">highest range</param>
    /// <returns>clamped value</returns>
    public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
    {
        return val.CompareTo(min) < 0 ? min : (val.CompareTo(max) > 0 ? max : val);
    }

    /// <summary>
    /// Determine if two <see cref="System.Numerics.Vector2"/> are near each other based on <paramref name="tolerance"/>.
    /// </summary>
    public static bool AreVectorsClose(System.Numerics.Vector2 v1, System.Numerics.Vector2 v2, float tolerance)
    {
        float gap = System.Numerics.Vector2.Distance(v1, v2);
        return gap <= tolerance;
    }

    /// <summary>
    /// Converts a <see cref="System.Numerics.Vector2"/> structure (x,y) 
    /// to <see cref="System.Numerics.Vector3"/> structure (x, y, 0).
    /// </summary>
    /// <param name="v"><see cref="System.Numerics.Vector2"/></param>
    /// <returns><see cref="System.Numerics.Vector3"/></returns>
    public static System.Numerics.Vector3 ToVector3(this System.Numerics.Vector2 v)
    {
        return new System.Numerics.Vector3(v, 0);
    }

    /// <summary>
    /// Scale a range of numbers. [baseMin to baseMax] will become [limitMin to limitMax]
    /// </summary>
    public static double Scale(this double valueIn, double baseMin, double baseMax, double limitMin, double limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
    public static float Scale(this float valueIn, float baseMin, float baseMax, float limitMin, float limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
    public static int Scale(this int valueIn, int baseMin, int baseMax, int limitMin, int limitMax) => ((limitMax - limitMin) * (valueIn - baseMin) / (baseMax - baseMin)) + limitMin;
    
    /// <summary>
    /// LERP a range of numbers.
    /// </summary>
    public static double Lerp(this double start, double end, double amount = 0.5D) => start + (end - start) * amount;
    public static float Lerp(this float start, float end, float amount = 0.5F) => start + (end - start) * amount;


    /// <summary>
    /// Check if a double is zero.
    /// </summary>
    /// <param name="value">The number to check.</param>
    /// <returns>true if the number is zero, false otherwise</returns>
    /// <remarks>we consider anything within an order of magnitude of epsilon to be zero</remarks>
    public static bool IsZero(this double value)
    {
        // 
        return Math.Abs(value) < EpsilonDouble;
    }

    /// <summary>
    /// Check if a float is zero.
    /// </summary>
    /// <param name="value">The number to check.</param>
    /// <returns>true if the number is zero, false otherwise</returns>
    /// <remarks>we consider anything within an order of magnitude of epsilon to be zero</remarks>
    public static bool IsZero(this float value)
    {
        return Math.Abs(value) < EpsilonFloat;
    }

    /// <summary>
    /// Returns whether or not the double is "close" to 1.
    /// </summary>
    /// <param name="value">The double to compare to 1</param>
    /// <returns>the result of the <see cref="Extensions.AreClose(double, double)"/> comparison</returns>
    public static bool IsOne(this double value)
    {
        return AreClose(value, 1.0d);
    }

    /// <summary>
    /// Returns whether or not the float is "close" to 1.
    /// </summary>
    /// <param name="value">The double to compare to 1</param>
    /// <returns>the result of the <see cref="Extensions.AreClose(float, float)"/> comparison</returns>
    public static bool IsOne(this float value)
    {
        return AreClose(value, 1.0d);
    }

    /// <summary>
    /// Determine if two doubles are close in value.
    /// </summary>
    /// <param name="left">First number.</param>
    /// <param name="right">Second number.</param>
    /// <returns>
    /// True if the first number is close in value to the second, false otherwise.
    /// </returns>
    public static bool AreClose(this double left, double right)
    {
        if (left == right)
            return true;

        double a = (Math.Abs(left) + Math.Abs(right) + 10.0d) * EpsilonDouble;
        double b = left - right;
        return (-a < b) && (a > b);
    }

    /// <summary>
    /// Determine if two floats are close in value.
    /// </summary>
    /// <param name="left">First number.</param>
    /// <param name="right">Second number.</param>
    /// <returns>
    /// True if the first number is close in value to the second, false otherwise.
    /// </returns>
    public static bool AreClose(this float left, float right)
    {
        if (left == right)
            return true;

        float a = (Math.Abs(left) + Math.Abs(right) + 10.0f) * EpsilonFloat;
        float b = left - right;
        return (-a < b) && (a > b);
    }

    /// <summary>
    /// Returns the biggest value from an <see cref="IComparable"/> set.
    /// Similar to Math.Max(), but this allows more than two input values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <returns>biggest comparable value</returns>
    /// <example>
    /// int biggest = Max(2, 41, 28, -11);
    /// </example>
    public static T Max<T>(params T[] values) where T : IComparable
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (result.CompareTo(values[i]) < 0)
                result = values[i];
        }
        return result;
    }

    /// <summary>
    /// Returns the smallest value from an <see cref="IComparable"/> set.
    /// Similar to Math.Min(), but this allows more than two input values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <returns>smallest comparable value</returns>
    /// <example>
    /// int smallest = Min(2, 41, 28, -11);
    /// </example>
    public static T Min<T>(params T[] values) where T : IComparable
    {
        T result = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (result.CompareTo(values[i]) > 0)
                result = values[i];
        }
        return result;
    }

    /// <summary>
    /// Evaluate the median value of a list from an <see cref="IComparable{T}"/> set.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <example>
    /// List{int} list = new List{int}(new int[] { 2, 4, 9, 22, 3, 5, 9, 11, 0 });
    /// Console.WriteLine("Median: {0}", list.MedianValue());
    /// </example>
    public static T MedianValue<T>(this List<T> list) where T : IComparable<T>
    {
        return MedianValue<T>(list, -1);
    }

    /// <summary>
    /// Evaluate the median value of a list from an <see cref="IComparable{T}"/> set.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <param name="position"></param>
    /// <example>
    /// List{int} list = new List{int}(new int[] { 2, 4, 9, 22, 3, 5, 9, 11, 0 });
    /// Console.WriteLine("Median: {0}", list.MedianValue(5));
    /// </example>
    public static T MedianValue<T>(this List<T> list, int position) where T : IComparable<T>
    {
        if (position < 0)
            position = list.Count / 2;

        T guess = list[0];

        if (list.Count == 1)
            return guess;

        List<T> lowList = new List<T>(); 
        List<T> highList = new List<T>();

        for (int i = 1; i < list.Count; i++)
        {
            T value = list[i];
            if (guess.CompareTo(value) <= 0) // Value is higher than or equal to the current guess.
                highList.Add(value);
            else // Value is lower than the current guess.
                lowList.Add(value);
        }

        if (lowList.Count > position) // Median value must be in the lower-than list.
            return MedianValue(lowList, position);
        else if (lowList.Count < position) // Median value must be in the higher-than list.
            return MedianValue(highList, position - lowList.Count - 1);
        else // Guess is correct.
            return guess;
    }

    /// <summary>
    /// 64-bit hashing method.
    /// Modulus and shift each byte across the string length.
    /// </summary>
    /// <param name="input">string to hash</param>
    public static ulong BasicHash(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        ulong value = (ulong)utf8.Length;
        for (int n = 0; n < utf8.Length; n++)
        {
            value += (ulong)utf8[n] << ((n * 5) % 56);
        }
        return value;
    }

    /// <summary>
    /// 64-bit hashing method using the Fowler-Noll-Vo (FNV) algorithm.
    /// </summary>
    /// <param name="input">string to hash</param>
    public static ulong FNV1Hash(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        const ulong FnvOffsetBasis = 14695981039346656037;
        const ulong FnvPrime = 1099511628211;

        ulong hash = FnvOffsetBasis;
        byte[] data = Encoding.UTF8.GetBytes(input);

        for (int i = 0; i < data.Length; ++i)
        {
            hash ^= data[i];
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>
    /// 64-bit hashing method using the Fowler-Noll-Vo (FNV) algorithm.
    /// </summary>
    /// <param name="input">string to hash</param>
    public static ulong FNV2Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        ulong hash = 5381; // Initial hash value

        foreach (char c in input)
        {
            hash = ((hash << 5) + hash) ^ c; // Update the hash
        }

        return hash;
    }

    /// <summary>
    /// This only shaved off 1ms during testing (which is within the margin of error).
    /// Since we are not working with a ton of low-level operations or manipulating 
    /// memory directly it's unlikely that this would have much of a performance impact.
    /// </summary>
    /// <param name="input">string to hash</param>
    //public static unsafe ulong FNV2HashUnsafe(string input)
    //{
    //    if (string.IsNullOrEmpty(input))
    //        return 0;
    //
    //    fixed (char* str = input)
    //    {
    //        char* ptr = str;
    //        int length = input.Length;
    //
    //        ulong hash = 5381;
    //
    //        while (length > 0)
    //        {
    //            hash = ((hash << 5) + hash) ^ *ptr;
    //            ptr++;
    //            length--;
    //        }
    //
    //        return hash;
    //    }
    //}

    /// <summary>
    /// An extremely simplified version of the SpookyHash.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="seed"></param>
    public static ulong SpookyHash64(byte[] data, ulong seed = 0)
    {
        if (data.Length == 0)
            return 0;

        const ulong m = 0x880355f21e6d1965;
        const int k1 = 50;
        const int k2 = 52;
        const int k3 = 30;
        const int k4 = 54;

        ulong h = seed;

        for (int i = 0; i < data.Length; i++)
        {
            h ^= data[i];
            h *= m;
            h ^= h >> k1;
            h *= m;
            h ^= h >> k2;
        }

        h ^= h >> k3;
        h *= m;
        h ^= h >> k4;

        return h;
    }
    public static ulong SpookyHash64(string data, ulong seed = 0)
    {
        if (string.IsNullOrEmpty(data))
            return 0;
            
        return SpookyHash64(Encoding.UTF8.GetBytes(data), seed);
    }

    public static ulong RotateLeft(ulong value, int shiftBits)
    {
        return (value << shiftBits) | (value >> (64 - shiftBits));
    }

    /// <summary>
    /// Generate an MD5 hash based on input string.
    /// </summary>
    /// <param name="input">string data to hash against</param>
    /// <returns>32 byte hash string</returns>
    public static string GetMD5(this string input)
    {
        try
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetMD5: {ex.Message}", nameof(Extensions));
            return new string('0', 32);
        }
    }

    /// <summary>
    /// Basic key generator for unique IDs.
    /// This employs the standard MS key table which accounts
    /// for the 36 Latin letters and Arabic numerals used in
    /// most Western European languages...
    /// 24 chars are favored: 2346789 BCDFGHJKMPQRTVWXY
    /// 12 chars are avoided: 015 AEIOU LNSZ
    /// Of the 24 favored chars, only two are occasionally
    /// mistaken: 8 & B, which depends mostly on the font.
    /// The base of possible codes is large, about 3.2 * 10^34.
    /// </summary>
    public static string KeyGen(int kLength = 6, long pSeed = 0)
    {
        const string pwChars = "2346789BCDFGHJKMPQRTVWXY";
        if (kLength < 6)
            kLength = 6; // minimum of 6 characters

        char[] charArray = pwChars.Distinct().ToArray();

        if (pSeed == 0)
        {
            pSeed = DateTime.Now.Ticks;
            //Thread.Sleep(1); // allow a tick to go by (if hammering)
        }

        var result = new char[kLength];
        var rng = new Random((int)pSeed);

        for (int x = 0; x < kLength; x++)
            result[x] = pwChars[rng.Next() % pwChars.Length];

        return (new string(result));
    }

    public static string NumberToWord(int number)
    {
        if (number == 0) { return "zero"; }
        if (number < 0) { return "minus " + NumberToWord(Math.Abs(number)); }

        string words = "";

        if ((number / 1000000) > 0)
        {
            words += NumberToWord(number / 1000000) + " million ";
            number %= 1000000;
        }

        if ((number / 1000) > 0)
        {
            words += NumberToWord(number / 1000) + " thousand ";
            number %= 1000;
        }

        if ((number / 100) > 0)
        {
            words += NumberToWord(number / 100) + " hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != "")
                words += "and ";

            var unitsMap = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
            var tensMap = new[] { "zero", "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += "-" + unitsMap[number % 10];
            }
        }

        return words;
    }
    #endregion

    #region [OS Version]
    /// <summary>
    /// If using the Windows version helper methods on older versions of dotnet (below .NET 5)
    /// it may not provide the correct major version. To make sure you get the right version 
    /// using Environment.OSVersion you should add an "app.manifest" using Visual Studio and 
    /// then un-comment the relevant "supportedOS" XML tag.
    /// <see cref="Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion"/>
    /// can be used in newer dotnet versions.
    /// </summary>
    internal static bool IsWindowsNT { get; } = Environment.OSVersion.Platform == PlatformID.Win32NT;

    public static int GetWindowsProductType()
    {
        NativeMethods.GetProductInfo(Environment.OSVersion.Version.Major, Environment.OSVersion.Version.Minor, 0, 0, out var productNum);
        return productNum;
    }

    /// <summary>
    /// Minimum supported client: Windows 2000 Professional
    /// </summary>
    /// <returns>true if OS is Windows XP or greater</returns>
    public static bool IsWindowsXPOrLater()
    {
        OperatingSystem osVersion = Environment.OSVersion;
        NativeMethods.OSVERSIONINFOEX osvi = new NativeMethods.OSVERSIONINFOEX();
        osvi.dwOSVersionInfoSize = Marshal.SizeOf(typeof(NativeMethods.OSVERSIONINFOEX));
        if (NativeMethods.GetVersionEx(ref osvi))
        {
            int majorVersion = osVersion.Version.Major;
            int minorVersion = osVersion.Version.Minor;
            int platformId = osvi.dwPlatformId;
            byte productType = osvi.wProductType;
            short suiteMask = osvi.wSuiteMask;
            string servicePack = osvi.szCSDVersion;
        }
        return ((osvi.dwMajorVersion > 5) || ((osvi.dwMajorVersion == 5) && (osvi.dwMinorVersion >= 1)));
    }

    /// <summary>
    /// Minimum supported client: Windows 2000 Professional
    /// </summary>
    /// <returns>true if OS is Windows 8 or greater</returns>
    public static bool IsWindows8OrLater()
    {
        Debug.WriteLine($"Major={Environment.OSVersion.Version.Major} Minor={Environment.OSVersion.Version.Minor} Revision={Environment.OSVersion.Version.Revision} Build={Environment.OSVersion.Version.Build}");
        return IsWindowsNT && Environment.OSVersion.Version >= new Version(6, 2);
    }

    /// <summary>
    /// Minimum supported client: Windows 2000 Professional
    /// </summary>
    /// <returns>true if OS is Windows 10 or greater</returns>
    public static bool IsWindows10OrLater()
    {
        Debug.WriteLine($"Major={Environment.OSVersion.Version.Major} Minor={Environment.OSVersion.Version.Minor} Revision={Environment.OSVersion.Version.Revision} Build={Environment.OSVersion.Version.Build}");
        return IsWindowsNT && Environment.OSVersion.Version >= new Version(10, 0);
    }

    /// <summary>
    /// Minimum supported client: Windows 2000 Professional
    /// </summary>
    /// <returns>true if OS is Windows 11 or greater</returns>
    public static bool IsWindows11OrLater()
    {
        Debug.WriteLine($"Major={Environment.OSVersion.Version.Major} Minor={Environment.OSVersion.Version.Minor} Revision={Environment.OSVersion.Version.Revision} Build={Environment.OSVersion.Version.Build}");
        return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
    }

    /// <summary>
    /// Get OS version by way of <see cref="Windows.System.Profile.AnalyticsInfo"/>.
    /// </summary>
    /// <returns><see cref="Version"/></returns>
    public static Version GetWindowsVersionUsingAnalyticsInfo()
    {
        try
        {
            ulong version = ulong.Parse(Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
            var Major = (ushort)((version & 0xFFFF000000000000L) >> 48);
            var Minor = (ushort)((version & 0x0000FFFF00000000L) >> 32);
            var Build = (ushort)((version & 0x00000000FFFF0000L) >> 16);
            var Revision = (ushort)(version & 0x000000000000FFFFL);

            return new Version(Major, Minor, Build, Revision);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetWindowsVersionUsingAnalyticsInfo: {ex.Message}", $"{nameof(Extensions)}");
            return new Version(); // 0.0
        }
    }
    #endregion

    #region [WinUI]
    public static bool IsMonospacedFont(FontFamily font)
    {
        var tb1 = new TextBlock { Text = "(!aiZ%#BIm,. ~`", FontFamily = font };
        tb1.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        var tb2 = new TextBlock { Text = "...............", FontFamily = font };
        tb2.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        var off = Math.Abs(tb1.DesiredSize.Width - tb2.DesiredSize.Width);
        return off < 0.01;
    }

    public static Windows.Foundation.Size GetTextSize(FontFamily font, double fontSize, string text)
    {
        var tb = new TextBlock { Text = text, FontFamily = font, FontSize = fontSize };
        tb.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity));
        return tb.DesiredSize;
    }

    /// <summary>
    /// Returns the <see cref="Microsoft.UI.Xaml.PropertyPath"/> based on the provided <see cref="Microsoft.UI.Xaml.Data.Binding"/>.
    /// </summary>
    public static string? GetBindingPropertyName(this Microsoft.UI.Xaml.Data.Binding binding)
    {
        return binding?.Path?.Path?.Split('.')?.LastOrDefault();
    }

    /// <summary>
    /// Gets a list of the specified FrameworkElement's DependencyProperties. This method will return all
    /// DependencyProperties of the element unless 'useBlockList' is true, in which case all bindings on elements
    /// that are typically not used as input controls will be ignored.
    /// </summary>
    /// <param name="element">FrameworkElement of interest</param>
    /// <param name="useBlockList">If true, ignores elements not typically used for input</param>
    /// <returns>List of DependencyProperties</returns>
    public static List<DependencyProperty> GetDependencyProperties(this FrameworkElement element, bool useBlockList)
    {
        List<DependencyProperty> dependencyProperties = new List<DependencyProperty>();

        bool isBlocklisted = useBlockList &&
            (element is Panel || element is Button || element is Image || element is ScrollViewer || 
             element is TextBlock || element is Border || element is Shape || element is ContentPresenter);

        if (!isBlocklisted)
        {
            Type type = element.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(DependencyProperty))
                {
                    var dp = (DependencyProperty)field.GetValue(null);
                    if (dp != null)
                        dependencyProperties.Add(dp);
                }
            }
        }

        return dependencyProperties;
    }


    public static bool IsXamlRootAvailable(bool UWP = false)
    {
        if (UWP)
            return Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "XamlRoot");
        else
            return Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.UIElement", "XamlRoot");
    }

    /// <summary>
    /// Helper function to calculate an element's rectangle in root-relative coordinates.
    /// </summary>
    public static Windows.Foundation.Rect GetElementRect(this Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            Microsoft.UI.Xaml.Media.GeneralTransform transform = element.TransformToVisual(null);
            Windows.Foundation.Point point = transform.TransformPoint(new Windows.Foundation.Point());
            return new Windows.Foundation.Rect(point, new Windows.Foundation.Size(element.ActualWidth, element.ActualHeight));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetElementRect: {ex.Message}", nameof(Extensions));
            return new Windows.Foundation.Rect(0,0,0,0);
        }
    }

    public static IconElement? GetIcon(string imagePath, string imageExt = ".png")
    {
        IconElement? result = null;

        try
        {
            result = imagePath.ToLowerInvariant().EndsWith(imageExt) ?
                        (IconElement)new BitmapIcon() { UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute), ShowAsMonochrome = false } :
                        (IconElement)new FontIcon() { Glyph = imagePath };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}", $"{nameof(Extensions)}");
        }

        return result;
    }

    public static FontIcon GenerateFontIcon(Windows.UI.Color brush, string glyph = "\uF127", int width = 10, int height = 10)
    {
        return new FontIcon()
        {
            Glyph = glyph,
            FontSize = 1.5,
            Width = (double)width,
            Height = (double)height,
            Foreground = new SolidColorBrush(brush),
        };
    }

    public static async Task<byte[]> AsPng(this UIElement control)
    {
        // Get XAML Visual in BGRA8 format
        var rtb = new RenderTargetBitmap();
        await rtb.RenderAsync(control, (int)control.ActualSize.X, (int)control.ActualSize.Y);

        // Encode as PNG
        var pixelBuffer = (await rtb.GetPixelsAsync()).ToArray();
        IRandomAccessStream mraStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, mraStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)rtb.PixelWidth,
            (uint)rtb.PixelHeight,
            184,
            184,
            pixelBuffer);
        await encoder.FlushAsync();

        // Transform to byte array
        var bytes = new byte[mraStream.Size];
        await mraStream.ReadAsync(bytes.AsBuffer(), (uint)mraStream.Size, InputStreamOptions.None);

        return bytes;
    }

    /// <summary>
    /// This is a redundant call from App.xaml.cs, but is here if you need it.
    /// </summary>
    /// <param name="window"><see cref="Microsoft.UI.Xaml.Window"/></param>
    /// <returns><see cref="Microsoft.UI.Windowing.AppWindow"/></returns>
    public static Microsoft.UI.Windowing.AppWindow GetAppWindow(this Microsoft.UI.Xaml.Window window)
    {
        System.IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        Microsoft.UI.WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wndId);
    }

    /// <summary>
    /// Creates a <see cref="LinearGradientBrush"/> from 3 input colors.
    /// </summary>
    /// <param name="c1">offset 0.0 color</param>
    /// <param name="c2">offset 0.5 color</param>
    /// <param name="c3">offset 1.0 color</param>
    /// <returns><see cref="LinearGradientBrush"/></returns>
    public static LinearGradientBrush CreateLinearGradientBrush(Windows.UI.Color c1, Windows.UI.Color c2, Windows.UI.Color c3)
    {
        var gs1 = new GradientStop(); gs1.Color = c1; gs1.Offset = 0.0;
        var gs2 = new GradientStop(); gs2.Color = c2; gs2.Offset = 0.5;
        var gs3 = new GradientStop(); gs3.Color = c3; gs3.Offset = 1.0;
        var gsc = new GradientStopCollection();
        gsc.Add(gs1); gsc.Add(gs2); gsc.Add(gs3);
        var lgb = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops = gsc
        };
        return lgb;
    }
    /// <summary>
    /// Creates a Color object from the hex color code and returns the result.
    /// </summary>
    /// <param name="hexColorCode">text representation of the color</param>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color? GetColorFromHexString(string hexColorCode)
    {
        if (string.IsNullOrEmpty(hexColorCode))
            return null;

        try
        {
            byte a = 255; byte r = 0; byte g = 0; byte b = 0;

            if (hexColorCode.Length == 9)
            {
                hexColorCode = hexColorCode.Substring(1, 8);
            }
            if (hexColorCode.Length == 8)
            {
                a = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                hexColorCode = hexColorCode.Substring(2, 6);
            }
            if (hexColorCode.Length == 6)
            {
                r = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                g = Convert.ToByte(hexColorCode.Substring(2, 2), 16);
                b = Convert.ToByte(hexColorCode.Substring(4, 2), 16);
            }

            return Windows.UI.Color.FromArgb(a, r, g, b);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Calculates the linear interpolated Color based on the given Color values.
    /// </summary>
    /// <param name="colorFrom">Source Color.</param>
    /// <param name="colorTo">Target Color.</param>
    /// <param name="amount">Weightage given to the target color.</param>
    /// <returns>Linear Interpolated Color.</returns>
    public static Windows.UI.Color Lerp(this Windows.UI.Color colorFrom, Windows.UI.Color colorTo, float amount)
    {
        // Convert colorFrom components to lerp-able floats
        float sa = colorFrom.A,
            sr = colorFrom.R,
            sg = colorFrom.G,
            sb = colorFrom.B;

        // Convert colorTo components to lerp-able floats
        float ea = colorTo.A,
            er = colorTo.R,
            eg = colorTo.G,
            eb = colorTo.B;

        // lerp the colors to get the difference
        byte a = (byte)Math.Max(0, Math.Min(255, sa.Lerp(ea, amount))),
            r = (byte)Math.Max(0, Math.Min(255, sr.Lerp(er, amount))),
            g = (byte)Math.Max(0, Math.Min(255, sg.Lerp(eg, amount))),
            b = (byte)Math.Max(0, Math.Min(255, sb.Lerp(eb, amount)));

        // return the new color
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Darkens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to darken. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color DarkerBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.Black, amount);
    }

    /// <summary>
    /// Lightens the color by the given percentage using lerp.
    /// </summary>
    /// <param name="color">Source color.</param>
    /// <param name="amount">Percentage to lighten. Value should be between 0 and 1.</param>
    /// <returns>Color</returns>
    public static Windows.UI.Color LighterBy(this Windows.UI.Color color, float amount)
    {
        return color.Lerp(Colors.White, amount);
    }

    /// <summary>
    /// Multiply color bytes by <paramref name="factor"/>, default value is 1.5
    /// </summary>
    public static Windows.UI.Color LightenColor(this Windows.UI.Color source, float factor = 1.5F)
    {
        var red = (int)((float)source.R * factor);
        var green = (int)((float)source.G * factor);
        var blue = (int)((float)source.B * factor);

        if (red == 0) { red = 0x1F; }
        else if (red > 255) { red = 0xFF; }
        if (green == 0) { green = 0x1F; }
        else if (green > 255) { green = 0xFF; }
        if (blue == 0) { blue = 0x1F; }
        else if (blue > 255) { blue = 0xFF; }

        return Windows.UI.Color.FromArgb((byte)255, (byte)red, (byte)green, (byte)blue);
    }

    /// <summary>
    /// Divide color bytes by <paramref name="factor"/>, default value is 1.5
    /// </summary>
    public static Windows.UI.Color DarkenColor(this Windows.UI.Color source, float factor = 1.5F)
    {
        if (source.R == 0) { source.R = 2; }
        if (source.G == 0) { source.G = 2; }
        if (source.B == 0) { source.B = 2; }

        var red = (int)((float)source.R / factor);
        var green = (int)((float)source.G / factor);
        var blue = (int)((float)source.B / factor);

        return Windows.UI.Color.FromArgb((byte)255, (byte)red, (byte)green, (byte)blue);
    }

    /// <summary>
    /// Generates a completely random <see cref="Windows.UI.Color"/>.
    /// </summary>
    /// <returns><see cref="Windows.UI.Color"/></returns>
    public static Windows.UI.Color GetRandomWinUIColor()
    {
        byte[] buffer = new byte[3];
        Random.Shared.NextBytes(buffer);
        return Windows.UI.Color.FromArgb(255, buffer[0], buffer[1], buffer[2]);
    }

    /// <summary>
    /// Returns a random selection from <see cref="Microsoft.UI.Colors"/>.
    /// </summary>
    /// <returns><see cref="Windows.UI.Color"/></returns>
	public static Windows.UI.Color GetRandomMicrosoftUIColor()
	{
		try
		{
			var colorType = typeof(Microsoft.UI.Colors);
			var colors = colorType.GetProperties()
				.Where(p => p.PropertyType == typeof(Windows.UI.Color) && p.GetMethod.IsStatic && p.GetMethod.IsPublic)
				.Select(p => (Windows.UI.Color)p.GetValue(null))
				.ToList();

		    if (colors.Count > 0)
            {
                var randomIndex = Random.Shared.Next(colors.Count);
                var randomColor = colors[randomIndex];
                return randomColor;
            }
            else
            {
                return Microsoft.UI.Colors.Gray;
            }
	    }
		catch (Exception ex)
		{
			Debug.WriteLine($"GetRandomColor: {ex.Message}");
			return Microsoft.UI.Colors.Red;
		}
	}


	/// <summary>
	/// Creates a Color from the hex color code and returns the result 
	/// as a <see cref="Microsoft.UI.Xaml.Media.SolidColorBrush"/>.
	/// </summary>
	/// <param name="hexColorCode">text representation of the color</param>
	/// <returns><see cref="Microsoft.UI.Xaml.Media.SolidColorBrush"/></returns>
	public static Microsoft.UI.Xaml.Media.SolidColorBrush? GetBrushFromHexString(string hexColorCode)
    {
        if (string.IsNullOrEmpty(hexColorCode))
            return null;

        try
        {
            byte a = 255; byte r = 0; byte g = 0; byte b = 0;

            if (hexColorCode.Length == 9)
            {
                hexColorCode = hexColorCode.Substring(1, 8);
            }
            if (hexColorCode.Length == 8)
            {
                a = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                hexColorCode = hexColorCode.Substring(2, 6);
            }
            if (hexColorCode.Length == 6)
            {
                r = Convert.ToByte(hexColorCode.Substring(0, 2), 16);
                g = Convert.ToByte(hexColorCode.Substring(2, 2), 16);
                b = Convert.ToByte(hexColorCode.Substring(4, 2), 16);
            }

            return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Verifies if the given brush is a SolidColorBrush and its color does not include transparency.
    /// </summary>
    /// <param name="brush">Brush</param>
    /// <returns>true if yes, otherwise false</returns>
    public static bool IsOpaqueSolidColorBrush(this Microsoft.UI.Xaml.Media.Brush brush)
    {
        return (brush as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color.A == 0xff;
    }

    /// <summary>
    /// Verifies if the given brush is the same as the otherBrush.
    /// </summary>
    /// <param name="brush">Given <see cref="Brush"/></param>
    /// <param name="otherBrush">The <see cref="Brush"/> to match it with</param>
    /// <returns>true if yes, otherwise false</returns>
    public static bool IsEqualTo(this Brush brush, Brush otherBrush)
    {
        if (brush.GetType() != otherBrush.GetType())
            return false;

        if (ReferenceEquals(brush, otherBrush))
            return true;

        // Are both instances of SolidColorBrush
        if ((brush is SolidColorBrush solidBrushA) && (otherBrush is SolidColorBrush solidBrushB))
            return (solidBrushA.Color == solidBrushB.Color) && solidBrushA.Opacity.AreClose(solidBrushB.Opacity);

        // Are both instances of LinearGradientBrush
        if ((brush is LinearGradientBrush linGradBrushA) && (otherBrush is LinearGradientBrush linGradBrushB))
        {
            var result = (linGradBrushA.ColorInterpolationMode == linGradBrushB.ColorInterpolationMode) &&
                             (linGradBrushA.EndPoint == linGradBrushB.EndPoint) &&
                             (linGradBrushA.MappingMode == linGradBrushB.MappingMode) &&
                              linGradBrushA.Opacity.AreClose(linGradBrushB.Opacity) &&
                             (linGradBrushA.StartPoint == linGradBrushB.StartPoint) &&
                             (linGradBrushA.SpreadMethod == linGradBrushB.SpreadMethod) &&
                             (linGradBrushA.GradientStops.Count == linGradBrushB.GradientStops.Count);
            if (!result)
            {
                return false;
            }

            for (var i = 0; i < linGradBrushA.GradientStops.Count; i++)
            {
                result = (linGradBrushA.GradientStops[i].Color == linGradBrushB.GradientStops[i].Color) &&
                          linGradBrushA.GradientStops[i].Offset.AreClose(linGradBrushB.GradientStops[i].Offset);

                if (!result)
                {
                    break;
                }
            }

            return result;
        }

        // Are both instances of ImageBrush
        if ((brush is ImageBrush imgBrushA) && (otherBrush is ImageBrush imgBrushB))
        {
            var result = (imgBrushA.AlignmentX == imgBrushB.AlignmentX) &&
                             (imgBrushA.AlignmentY == imgBrushB.AlignmentY) &&
                              imgBrushA.Opacity.AreClose(imgBrushB.Opacity) &&
                             (imgBrushA.Stretch == imgBrushB.Stretch) &&
                             (imgBrushA.ImageSource == imgBrushB.ImageSource);

            return result;
        }

        return false;
    }

    /// <summary>
    /// Finds the contrast ratio.
    /// This is helpful for determining if one control's foreground and another control's background will be hard to distinguish.
    /// https://www.w3.org/WAI/GL/wiki/Contrast_ratio
    /// (L1 + 0.05) / (L2 + 0.05), where
    /// L1 is the relative luminance of the lighter of the colors, and
    /// L2 is the relative luminance of the darker of the colors.
    /// </summary>
    /// <param name="first"><see cref="Windows.UI.Color"/></param>
    /// <param name="second"><see cref="Windows.UI.Color"/></param>
    /// <returns>ratio between relative luminance</returns>
    public static double CalculateContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        double relLuminanceOne = GetRelativeLuminance(first);
        double relLuminanceTwo = GetRelativeLuminance(second);
        return (Math.Max(relLuminanceOne, relLuminanceTwo) + 0.05) / (Math.Min(relLuminanceOne, relLuminanceTwo) + 0.05);
    }

    /// <summary>
    /// Gets the relative luminance.
    /// https://www.w3.org/WAI/GL/wiki/Relative_luminance
    /// For the sRGB colorspace, the relative luminance of a color is defined as L = 0.2126 * R + 0.7152 * G + 0.0722 * B
    /// </summary>
    /// <param name="c"><see cref="Windows.UI.Color"/></param>
    public static double GetRelativeLuminance(Windows.UI.Color c)
    {
        double rSRGB = c.R / 255.0;
        double gSRGB = c.G / 255.0;
        double bSRGB = c.B / 255.0;

        // WebContentAccessibilityGuideline 2.x definition was 0.03928 (incorrect)
        // WebContentAccessibilityGuideline 3.x definition is 0.04045 (correct)
        double r = rSRGB <= 0.04045 ? rSRGB / 12.92 : Math.Pow(((rSRGB + 0.055) / 1.055), 2.4);
        double g = gSRGB <= 0.04045 ? gSRGB / 12.92 : Math.Pow(((gSRGB + 0.055) / 1.055), 2.4);
        double b = bSRGB <= 0.04045 ? bSRGB / 12.92 : Math.Pow(((bSRGB + 0.055) / 1.055), 2.4);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// Returns a new <see cref="Windows.Foundation.Rect(double, double, double, double)"/> representing the size of the <see cref="Vector2"/>.
    /// </summary>
    /// <param name="vector"><see cref="System.Numerics.Vector2"/> vector representing object size for Rectangle.</param>
    /// <returns><see cref="Windows.Foundation.Rect(double, double, double, double)"/> value.</returns>
    public static Windows.Foundation.Rect ToRect(this System.Numerics.Vector2 vector)
    {
        return new Windows.Foundation.Rect(0, 0, vector.X, vector.Y);
    }

    /// <summary>
    /// Returns a new <see cref="System.Numerics.Vector2"/> representing the <see cref="Windows.Foundation.Size(double, double)"/>.
    /// </summary>
    /// <param name="size"><see cref="Windows.Foundation.Size(double, double)"/> value.</param>
    /// <returns><see cref="System.Numerics.Vector2"/> value.</returns>
    public static System.Numerics.Vector2 ToVector2(this Windows.Foundation.Size size)
    {
        return new System.Numerics.Vector2((float)size.Width, (float)size.Height);
    }

    /// <summary>
    /// Deflates rectangle by given thickness.
    /// </summary>
    /// <param name="rect">Rectangle</param>
    /// <param name="thick">Thickness</param>
    /// <returns>Deflated Rectangle</returns>
    public static Windows.Foundation.Rect Deflate(this Windows.Foundation.Rect rect, Microsoft.UI.Xaml.Thickness thick)
    {
        return new Windows.Foundation.Rect(
            rect.Left + thick.Left,
            rect.Top + thick.Top,
            Math.Max(0.0, rect.Width - thick.Left - thick.Right),
            Math.Max(0.0, rect.Height - thick.Top - thick.Bottom));
    }

    /// <summary>
    /// Inflates rectangle by given thickness.
    /// </summary>
    /// <param name="rect">Rectangle</param>
    /// <param name="thick">Thickness</param>
    /// <returns>Inflated Rectangle</returns>
    public static Windows.Foundation.Rect Inflate(this Windows.Foundation.Rect rect, Microsoft.UI.Xaml.Thickness thick)
    {
        return new Windows.Foundation.Rect(
            rect.Left - thick.Left,
            rect.Top - thick.Top,
            Math.Max(0.0, rect.Width + thick.Left + thick.Right),
            Math.Max(0.0, rect.Height + thick.Top + thick.Bottom));
    }

    /// <summary>
    /// Starts an <see cref="Microsoft.UI.Composition.ExpressionAnimation"/> to keep the size of the source <see cref="Microsoft.UI.Composition.CompositionObject"/> in sync with the target <see cref="UIElement"/>
    /// </summary>
    /// <param name="source">The <see cref="Microsoft.UI.Composition.CompositionObject"/> to start the animation on</param>
    /// <param name="target">The target <see cref="UIElement"/> to read the size updates from</param>
    public static void BindSize(this Microsoft.UI.Composition.CompositionObject source, UIElement target)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var bindSizeAnimation = source.Compositor.CreateExpressionAnimation($"{nameof(visual)}.Size");
        bindSizeAnimation.SetReferenceParameter(nameof(visual), visual);
        // Start the animation
        source.StartAnimation("Size", bindSizeAnimation);
    }

    /// <summary>
    /// Starts an animation on the given property of a <see cref="Microsoft.UI.Composition.CompositionObject"/>
    /// </summary>
    /// <typeparam name="T">The type of the property to animate</typeparam>
    /// <param name="target">The target <see cref="Microsoft.UI.Composition.CompositionObject"/></param>
    /// <param name="property">The name of the property to animate</param>
    /// <param name="value">The final value of the property</param>
    /// <param name="duration">The animation duration</param>
    /// <returns>A <see cref="Task"/> that completes when the created animation completes</returns>
    public static Task StartAnimationAsync<T>(this Microsoft.UI.Composition.CompositionObject target, string property, T value, TimeSpan duration) where T : unmanaged
    {
        // Stop previous animations
        target.StopAnimation(property);

        // Setup the animation to run
        Microsoft.UI.Composition.KeyFrameAnimation animation;

        // Switch on the value to determine the necessary KeyFrameAnimation type
        switch (value)
        {
            case float f:
                var scalarAnimation = target.Compositor.CreateScalarKeyFrameAnimation();
                scalarAnimation.InsertKeyFrame(1f, f);
                animation = scalarAnimation;
                break;
            case Windows.UI.Color c:
                var colorAnimation = target.Compositor.CreateColorKeyFrameAnimation();
                colorAnimation.InsertKeyFrame(1f, c);
                animation = colorAnimation;
                break;
            case System.Numerics.Vector4 v4:
                var vector4Animation = target.Compositor.CreateVector4KeyFrameAnimation();
                vector4Animation.InsertKeyFrame(1f, v4);
                animation = vector4Animation;
                break;
            default: throw new ArgumentException($"Invalid animation type: {typeof(T)}", nameof(value));
        }

        animation.Duration = duration;

        // Get the batch and start the animations
        var batch = target.Compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

        // Create a TCS for the result
        var tcs = new TaskCompletionSource<object>();

        batch.Completed += (s, e) => tcs.SetResult(null);

        target.StartAnimation(property, animation);

        batch.End();

        return tcs.Task;
    }

    /// <summary>
    /// Creates a <see cref="Microsoft.UI.Composition.CompositionGeometricClip"/> from the specified <see cref="Windows.Graphics.IGeometrySource2D"/>.
    /// </summary>
    /// <param name="compositor"><see cref="Microsoft.UI.Composition.Compositor"/></param>
    /// <param name="geometry"><see cref="Windows.Graphics.IGeometrySource2D"/></param>
    /// <returns>CompositionGeometricClip</returns>
    public static Microsoft.UI.Composition.CompositionGeometricClip CreateGeometricClip(this Microsoft.UI.Composition.Compositor compositor, Windows.Graphics.IGeometrySource2D geometry)
    {
        // Create the CompositionPath
        var path = new Microsoft.UI.Composition.CompositionPath(geometry);
        // Create the CompositionPathGeometry
        var pathGeometry = compositor.CreatePathGeometry(path);
        // Create the CompositionGeometricClip
        return compositor.CreateGeometricClip(pathGeometry);
    }
    #endregion

    #region [Bit-Twiddlers]
    /// <summary>
    /// Extracts the high nibble of a byte.
    /// </summary>
    /// <returns>return byte representing the high nibble of a byte</returns>
    public static byte HiNibble(this byte value)
    {
        return (byte)(value >> 4);
    }

    /// <summary>
    /// Extracts the low nibble of a byte.
    /// </summary>
    /// <returns>return byte representing the low nibble of a byte</returns>
    public static byte LowNibble(this byte value)
    {
        return (byte)(value & 0x0F);
    }

    /// <summary>
    /// 0xFF => (15,15)
    /// </summary>
    /// <param name="val">byte to evaluate</param>
    /// <returns>tuple</returns>
    public static (int low, int high) GetLowAndHighBytes(this byte val)
    {
        int low = val & 0x0F;
        int high = (val & 0xF0) >> 4;
        return (low, high);
    }

    /// <summary>
    /// Returns whether the bit at the specified position is set.
    /// </summary>
    /// <typeparam name="T">Any integer type.</typeparam>
    /// <param name="t">The value to check.</param>
    /// <param name="pos">The position of the bit to check, 0 refers to the least significant bit.</param>
    /// <returns>true if the specified bit is on, otherwise false.</returns>
    public static bool IsBitSet<T>(this T t, int pos) where T : struct, IConvertible
    {
        var value = t.ToInt64(CultureInfo.CurrentCulture);
        return (value & (1 << pos)) != 0;
    }
    #endregion

    #region [Types]
    /// <summary>
    /// Returns the field names and their types for a specific class.
    /// </summary>
    /// <param name="myType"></param>
    /// <example>
    /// var dict = ReflectFieldInfo(typeof(FileBackupView));
    /// </example>
    public static Dictionary<string, Type> ReflectFieldInfo(Type myType)
    {
        Dictionary<string, Type> results = new();
        FieldInfo[] myFieldInfo;
        myFieldInfo = myType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        for (int i = 0; i < myFieldInfo.Length; i++) { results[myFieldInfo[i].Name] = myFieldInfo[i].FieldType; }
        return results;
    }

    /// <summary>
    /// Gets the default member name that is used for an indexer (e.g. "Item").
    /// </summary>
    /// <param name="type">Type to check.</param>
    /// <returns>Default member name.</returns>
    public static string? GetDefaultMemberName(this Type type)
    {
        DefaultMemberAttribute? defaultMemberAttribute = type.GetTypeInfo().GetCustomAttributes().OfType<DefaultMemberAttribute>().FirstOrDefault();
        return defaultMemberAttribute == null ? null : defaultMemberAttribute.MemberName;
    }

    public static bool IsDisposable(this Type type)
    {
        if (!typeof(IDisposable).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool IsClonable(this Type type)
    {
        if (!typeof(ICloneable).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool IsComparable(this Type type)
    {
        if (!typeof(IComparable).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool IsConvertible(this Type type)
    {
        if (!typeof(IConvertible).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool IsFormattable(this Type type)
    {
        if (!typeof(IFormattable).IsAssignableFrom(type))
            return false;

        return true;
    }

    public static bool IsEnumerableType(this Type enumerableType)
    {
        return FindGenericType(typeof(IEnumerable<>), enumerableType) != null;
    }

    public static bool IsNullableType(this Type type)
    {
        return type != null && type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// Returns whether or not a given type is generic.
    /// </summary>
    /// <param name="type">The input type.</param>
    /// <returns>Whether or not the input type is generic.</returns>
    [Pure]
    private static bool IsGenericType(this Type type)
    {
#if NETSTANDARD1_4
        return type.GetTypeInfo().IsGenericType;
#else
        return type.IsGenericType;
#endif
    }

    /// <summary>
    /// Determines if a base type implements an interface type.
    /// </summary>
    /// <param name="baseType"></param>
    /// <param name="interfaceType"></param>
    /// <returns>true if base type implements interface, false otherwise</returns>
    public static bool ImplementsInterface(this Type baseType, Type interfaceType)
    {
        return baseType.GetInterfaces().Any(interfaceType.Equals);
    }

    /// <summary>
    /// Returns an array of types representing the generic arguments.
    /// </summary>
    /// <param name="type">The input type.</param>
    /// <returns>An array of types representing the generic arguments.</returns>
    [Pure]
    public static Type[] GetGenericArguments(this Type type)
    {
        return type.GetTypeInfo().GenericTypeParameters;
    }

    /// <summary>
    /// Returns whether <paramref name="type"/> is an instance of <paramref name="value"/>.
    /// </summary>
    /// <param name="type">The input type.</param>
    /// <param name="value">The type to check against.</param>
    /// <returns><see langword="true"/> if <paramref name="type"/> is an instance of <paramref name="value"/>, <see langword="false"/> otherwise.</returns>
    [Pure]
    public static bool IsInstanceOfType(this Type type, object value)
    {
        return type.GetTypeInfo().IsAssignableFrom(value.GetType().GetTypeInfo());
    }

    public static Type? FindGenericType(Type definition, Type type)
    {
        TypeInfo definitionTypeInfo = definition.GetTypeInfo();

        while (type != null && type != typeof(object))
        {
            TypeInfo typeTypeInfo = type.GetTypeInfo();

            if (typeTypeInfo.IsGenericType && type.GetGenericTypeDefinition() == definition)
                return type;

            if (definitionTypeInfo.IsInterface)
            {
                foreach (Type type2 in typeTypeInfo.ImplementedInterfaces)
                {
                    Type? type3 = FindGenericType(definition, type2);
                    if (type3 != null)
                        return type3;
                }
            }

            type = typeTypeInfo.BaseType;
        }

        return null;
    }

    /// <summary>
    /// A thread-safe mapping of precomputed string representation of types.
    /// </summary>
    static readonly ConditionalWeakTable<Type, string> DisplayNames = new ConditionalWeakTable<Type, string>();

    /// <summary>
    /// The mapping of built-in types to their simple representation.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, string> BuiltInTypesMap = new Dictionary<Type, string>
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(char)] = "char",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(float)] = "float",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(object)] = "object",
        [typeof(string)] = "string",
        [typeof(void)] = "void"
    };

    /// <summary>
    /// Returns a simple string representation of a type.
    /// </summary>
    /// <param name="type">The input type.</param>
    /// <returns>The string representation of <paramref name="type"/>.</returns>
    [Pure]
    public static string ToTypeString(this Type type)
    {
        // Local function to create the formatted string for a given type
        static string FormatDisplayString(Type type, int genericTypeOffset, ReadOnlySpan<Type> typeArguments)
        {
            // Primitive types use the keyword name
            if (BuiltInTypesMap.TryGetValue(type, out string? typeName))
            {
                return typeName!;
            }

            // Array types are displayed as Foo[]
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                var rank = type.GetArrayRank();

                return $"{FormatDisplayString(elementType, 0, elementType.GetGenericArguments())}[{new string(',', rank - 1)}]";
            }

            // By checking generic types here we are only interested in specific cases,
            // ie. nullable value types or value types. We have a separate path for custom
            // generic types, as we can't rely on this API in that case, as it doesn't show
            // a difference between nested types that are themselves generic, or nested simple
            // types from a generic declaring type. To deal with that, we need to manually track
            // the offset within the array of generic arguments for the whole constructed type.
            if (type.IsGenericType())
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();

                // Nullable<T> types are displayed as T?
                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    var nullableArguments = type.GetGenericArguments();

                    return $"{FormatDisplayString(nullableArguments[0], 0, nullableArguments)}?";
                }

                // ValueTuple<T1, T2> types are displayed as (T1, T2)
                if (genericTypeDefinition == typeof(ValueTuple<>) ||
                    genericTypeDefinition == typeof(ValueTuple<,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,,,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,,,,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,,,,,>) ||
                    genericTypeDefinition == typeof(ValueTuple<,,,,,,,>))
                {
                    var formattedTypes = type.GetGenericArguments().Select(t => FormatDisplayString(t, 0, t.GetGenericArguments()));

                    return $"({string.Join(", ", formattedTypes)})";
                }
            }

            string displayName;

            // Generic types
            if (type.Name.Contains('`'))
            {
                // Retrieve the current generic arguments for the current type (leaf or not)
                var tokens = type.Name.Split('`');
                var genericArgumentsCount = int.Parse(tokens[1]);
                var typeArgumentsOffset = typeArguments.Length - genericTypeOffset - genericArgumentsCount;
                var currentTypeArguments = typeArguments.Slice(typeArgumentsOffset, genericArgumentsCount).ToArray();
                var formattedTypes = currentTypeArguments.Select(t => FormatDisplayString(t, 0, t.GetGenericArguments()));

                // Standard generic types are displayed as Foo<T>
                displayName = $"{tokens[0]}<{string.Join(", ", formattedTypes)}>";

                // Track the current offset for the shared generic arguments list
                genericTypeOffset += genericArgumentsCount;
            }
            else
            {
                // Simple custom types
                displayName = type.Name;
            }

            // If the type is nested, recursively format the hierarchy as well
            if (type.IsNested)
            {
                var openDeclaringType = type.DeclaringType!;
                var rootGenericArguments = typeArguments.Slice(0, typeArguments.Length - genericTypeOffset).ToArray();

                // If the declaring type is generic, we need to reconstruct the closed type
                // manually, as the declaring type instance doesn't retain type information.
                if (rootGenericArguments.Length > 0)
                {
                    var closedDeclaringType = openDeclaringType.GetGenericTypeDefinition().MakeGenericType(rootGenericArguments);

                    return $"{FormatDisplayString(closedDeclaringType, genericTypeOffset, typeArguments)}.{displayName}";
                }

                return $"{FormatDisplayString(openDeclaringType, genericTypeOffset, typeArguments)}.{displayName}";
            }

            return $"{type.Namespace}.{displayName}";
        }

        // Atomically get or build the display string for the current type.
        return DisplayNames.GetValue(type, t =>
        {
            // By-ref types are displayed as T&
            if (t.IsByRef)
            {
                t = t.GetElementType()!;

                return $"{FormatDisplayString(t, 0, t.GetGenericArguments())}&";
            }

            // Pointer types are displayed as T*
            if (t.IsPointer)
            {
                int depth = 0;

                // Calculate the pointer indirection level
                while (t.IsPointer)
                {
                    depth++;
                    t = t.GetElementType()!;
                }

                return $"{FormatDisplayString(t, 0, t.GetGenericArguments())}{new string('*', depth)}";
            }

            // Standard path for concrete types
            return FormatDisplayString(t, 0, t.GetGenericArguments());
        });
    }
    #endregion

    #region [Process]
    /// <summary>
    /// Helper method for thread state.
    /// </summary>
    /// <param name="tstate"><see cref="System.Threading.ThreadState"/></param>
    /// <returns>reduced <see cref="System.Threading.ThreadState"/></returns>
    public static System.Threading.ThreadState SimplifyState(this System.Threading.ThreadState tstate)
    {
        return tstate & (System.Threading.ThreadState.Unstarted |
                     System.Threading.ThreadState.WaitSleepJoin |
                     System.Threading.ThreadState.Stopped);
    }

    /// <summary>
    /// Macro for in-line code testing.
    /// </summary>
    /// <param name="action">code to execute</param>
    /// <param name="logger"><see cref="ILogger"/></param>
    public static void Try(Action action, ILogger? logger)
    {
        try { action(); }
        catch (Exception ex)
        {
            string[] lines = {
                    $"A {ex.GetType()} was thrown.",
                    ex.Message,
                    (ex.InnerException != null) ? ex.InnerException.Message : "",
                };
            string errMsg = string.Join("-->", lines);

            if (logger != null)
                logger.WriteLine($"{errMsg}", LogLevel.Error);
        }
    }

    public static T Retry<T>(this Func<T> operation, int attempts)
    {
        while (true)
        {
            try
            {
                attempts--;
                return operation();
            }
            catch (Exception ex) when (attempts > 0)
            {
                Debug.WriteLine($"{MethodBase.GetCurrentMethod()?.Name}: {ex.Message}", $"{nameof(Extensions)}");
                Thread.Sleep(2000);
            }
        }
    }

    /// <summary>
    /// Employs Process.WaitForExitAsync to block until the <paramref name="processId"/> has closed.
    /// </summary>
    /// <param name="processId">the PID to monitor</param>
    /// <param name="token"><see cref="CancellationToken"/></param>
    /// <returns>integer exit code, -1 if error</returns>
    public static async Task<int> WatchProcessUntilExit(int processId, CancellationToken token)
    {
        try
        {
            Process handle = Process.GetProcessById(processId);
            if (handle == null) { return -1; }
            try { await handle.WaitForExitAsync(token); }
            catch (TaskCanceledException) { /* Nothing to do, normal operation */ }
            if (handle.HasExited) { return handle.ExitCode; }
            return -1;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Couldn't add a waiter to wait for process to exit (PID {processId}): {ex.Message}");
            return -1;
        }
    }
    #endregion

    #region [Resources and Controls]
    /// <summary>
    /// Returns the selected item's content.
    /// </summary>
    /// <param name="comboBox"><see cref="ComboBox"/></param>
    /// <returns>the selected item's content</returns>
    public static string? GetSelectedText(this ComboBox comboBox)
    {
        var item = comboBox.SelectedItem as ComboBoxItem;
        if (item != null) { return (string)item.Content; }
        return null;
    }

    /// <summary>
    /// Enables or disables the Header on the <see cref="Expander"/>.
    /// </summary>
    public static void IsLocked(this Expander expander, bool locked)
    {
        var ctrl = (expander.Header as FrameworkElement)?.Parent as Control;
        if (ctrl != null)
            ctrl.IsEnabled = locked;
    }

    /// <summary>
    /// Sets the desired Height for content when expanded.
    /// </summary>
    public static void SetContentHeight(this Expander expander, double contentHeight)
    {
        var ctrl = expander.Content as FrameworkElement;
        if (ctrl != null)
            ctrl.Height = contentHeight;
    }

    public static void SetOrientation(this VirtualizingLayout layout, Orientation orientation)
    {
        // The public properties of UniformGridLayout and FlowLayout interpret
        // orientation the opposite to how FlowLayoutAlgorithm interprets it. 
        // For simplicity, our validation code is written in terms that match
        // the implementation. For this reason, we need to switch the orientation
        // whenever we set UniformGridLayout.Orientation or StackLayout.Orientation.
        if (layout is StackLayout)
        {
            ((StackLayout)layout).Orientation = orientation;
        }
        else if (layout is UniformGridLayout)
        {
            ((UniformGridLayout)layout).Orientation = orientation;
        }
        else
        {
            throw new InvalidOperationException("Unknown Layout");
        }
    }

    /// <summary>
    /// Randomize an <see cref="ObservableCollection{T}"/>.
    /// </summary>
    public static ObservableCollection<T> RandomizeCollection<T>(this ObservableCollection<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Shared.Next(i + 1);

            // Tuple technique...
            //(list[swapIndex], list[i]) = (list[i], list[swapIndex]);

            // Traditional technique...
            T tmp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = tmp;
        }

        return list;
    }

    /// <summary>
    /// Use this if you only have a root resource dictionary.
    /// var rdBrush = Extensions.GetResource{SolidColorBrush}("PrimaryBrush");
    /// </summary>
    public static T? GetResource<T>(string resourceName) where T : class
    {
        try
        {
            if (Application.Current.Resources.TryGetValue($"{resourceName}", out object value))
                return (T)value;
            else
                return default(T);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetResource: {ex.Message}", $"{nameof(Extensions)}");
            return null;
        }
    }

    /// <summary>
    /// Use this if you have merged theme resource dictionaries.
    /// var darkBrush = Extensions.GetThemeResource{SolidColorBrush}("PrimaryBrush", ElementTheme.Dark);
    /// var lightBrush = Extensions.GetThemeResource{SolidColorBrush}("PrimaryBrush", ElementTheme.Light);
    /// </summary>
    public static T? GetThemeResource<T>(string resourceName, ElementTheme? theme) where T : class
    {
        try
        {
            if (theme == null) { theme = ElementTheme.Default; }

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var item in dictionaries)
            {
                // Do we have any themes in this resource dictionary?
                if (item.ThemeDictionaries.Count > 0)
                {
                    if (theme == ElementTheme.Dark)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Dark", out var drd))
                        {
                            ResourceDictionary? dark = drd as ResourceDictionary;
                            if (dark != null)
                            {
                                Debug.WriteLine($"Found dark theme resource dictionary");
                                if (dark.TryGetValue($"{resourceName}", out var tmp))
                                {
                                    return (T)tmp;
                                }
                                else
                                {
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                                }
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Dark)} theme was not found"); }
                    }
                    else if (theme == ElementTheme.Light)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Light", out var lrd))
                        {
                            ResourceDictionary? light = lrd as ResourceDictionary;
                            if (light != null)
                            {
                                Debug.WriteLine($"Found light theme resource dictionary");
                                if (light.TryGetValue($"{resourceName}", out var tmp))
                                {
                                    return (T)tmp;
                                }
                                else
                                {
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                                }
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Light)} theme was not found"); }
                    }
                    else if (theme == ElementTheme.Default)
                    {
                        if (item.ThemeDictionaries.TryGetValue("Default", out var drd))
                        {
                            ResourceDictionary? dflt = drd as ResourceDictionary;
                            if (dflt != null)
                            {
                                Debug.WriteLine($"Found default theme resource dictionary");
                                if (dflt.TryGetValue($"{resourceName}", out var tmp))
                                {
                                    return (T)tmp;
                                }
                                else
                                {
                                    Debug.WriteLine($"Could not find '{resourceName}'");
                                }
                            }
                        }
                        else { Debug.WriteLine($"{nameof(ElementTheme.Default)} theme was not found"); }
                    }
                    else
                    {
                        Debug.WriteLine($"No theme to match");
                    }
                }
                else
                {
                    Debug.WriteLine($"No theme dictionaries found");
                }
            }

            return default(T);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetThemeResource: {ex.Message}", $"{nameof(Extensions)}");
            return null;
        }
    }
    #endregion

    #region [IEnumerable]
    public static IEnumerable<T> Replace<T>(this IEnumerable<T> source, T oldValue, T newValue)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        return source.Select(x => EqualityComparer<T>.Default.Equals(x, oldValue) ? newValue : x);
    }

    public static IEnumerable<(T value, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((value, index) => (value, index));
    }

    public static (int min, int max) MinMax(this IEnumerable<int> source)
    {
        using (var iterator = source.GetEnumerator())
        {
            if (!iterator.MoveNext())
                throw new InvalidOperationException("Cannot find min/max of an empty sequence");

            int min = iterator.Current;
            int max = iterator.Current;
            while (iterator.MoveNext())
            {
                min = Math.Min(min, iterator.Current);
                max = Math.Max(max, iterator.Current);
            }
            return (min, max);
        }
    }

    /// <summary>
    /// Uses an operator for the current and previous item.
    /// Needs only a single iteration to process pairs and produce an output.
    /// </summary>
    /// <example>
    /// var avg = collection.Pairwise((a, b) => (b.DateTime - a.DateTime)).Average(ts => ts.TotalMinutes);
    /// </example>
    public static IEnumerable<TResult> Pairwise<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> resultSelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (resultSelector == null)
            throw new ArgumentNullException(nameof(resultSelector));

        return _(); IEnumerable<TResult> _()
        {
            using var e = source.GetEnumerator();

            if (!e.MoveNext())
                yield break;

            var previous = e.Current;
            while (e.MoveNext())
            {
                yield return resultSelector(previous, e.Current);
                previous = e.Current;
            }
        }
    }

    /// <summary>
    /// Chunks a large list into smaller n-sized list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="locations">List to be chunked</param>
    /// <param name="nSize">Size of chunks</param>
    /// <returns>IEnumerable list broken up into chunks</returns>
    public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 30)
    {
        for (int i = 0; i < locations.Count; i += nSize)
        {
            yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
        }
    }

    /// <summary>
    /// Weave two <see cref="IEnumerable{T}"/>s together while alternating elements.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns>Woven <see cref="IEnumerable{T}"/></returns>
    public static IEnumerable<T> InterleaveSequenceWith<T>(this IEnumerable<T> first, IEnumerable<T> second)
    {
        var firstIter = first.GetEnumerator();
        var secondIter = second.GetEnumerator();

        while (firstIter.MoveNext() && secondIter.MoveNext())
        {
            yield return firstIter.Current;
            yield return secondIter.Current;
        }
    }

    /// <summary>
    /// IEnumerable file reader.
    /// </summary>
    public static IEnumerable<string> ReadFileLines(string path)
    {
        string line = string.Empty;

        if (!File.Exists(path))
            yield return line;
        else
        {
            using (TextReader reader = File.OpenText(path))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// IAsyncEnumerable file reader.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadFileLinesAsync(string path)
    {
        string line = string.Empty;

        if (!File.Exists(path))
            yield return line;
        else
        {
            using (TextReader reader = File.OpenText(path))
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    yield return line;
                }
            }
        }
    }

    /// <summary>
    /// File writer for <see cref="IEnumerable{string}"/> parameters.
    /// </summary>
    public static bool WriteFileLines(string path, IEnumerable<string> lines)
    {
        using (TextWriter writer = File.CreateText(path))
        {
            foreach (var line in lines)
            {
                writer.WriteLine(line);
            }
        }

        return true;
    }

    /// <summary>
    /// De-dupe file reader using a <see cref="HashSet{string}"/>.
    /// </summary>
    public static HashSet<string> ReadLines(string path)
    {
        if (!File.Exists(path))
            return new();

        return new HashSet<string>(File.ReadAllLines(path), StringComparer.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// De-dupe file writer using a <see cref="HashSet{string}"/>.
    /// </summary>
    public static bool WriteLines(string path, IEnumerable<string> lines)
    {
        var output = new HashSet<string>(lines, StringComparer.InvariantCultureIgnoreCase);

        using (TextWriter writer = File.CreateText(path))
        {
            foreach (var line in output)
            {
                writer.WriteLine(line);
            }
        }
        return true;
    }

    /// <summary>
    /// var collection = new[] { 10, 20, 30 };
    /// collection.ForEach(Console.WriteLine);
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
    {
        foreach (var i in ie)
            action(i);
    }

    /// <summary>
    /// var collection = new[] { 10, 20, 30 };
    /// collection.ForEachUsingIterator(Console.WriteLine);
    /// </summary>
    public static void ForEachUsingIterator<T>(this IEnumerable<T> ie, Action<T> action)
    {
        var iterator = ie.GetEnumerator();
        using (iterator)
        {
            while (iterator.MoveNext())
            {
                action(iterator.Current);
            }
        }
    }
    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
    {
        var joined = new[] { list1, list2 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }
    public static IEnumerable<T> JoinLists<T>(this IEnumerable<T> list1, IEnumerable<T> list2, IEnumerable<T> list3)
    {
        var joined = new[] { list1, list2, list3 }.Where(x => x != null).SelectMany(x => x);
        return joined ?? Enumerable.Empty<T>();
    }
    public static IEnumerable<T> JoinMany<T>(params IEnumerable<T>[] array)
    {
        var final = array.Where(x => x != null).SelectMany(x => x);
        return final ?? Enumerable.Empty<T>();
    }
    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
    {
        if (target == null) { throw new ArgumentNullException(nameof(target)); }
        if (source == null) { throw new ArgumentNullException(nameof(source)); }
        foreach (var element in source) { target.Add(element); }
    }

    /// <summary>
    /// Catch the exception and then omit the value if exception thrown.
    /// </summary>
    public static IEnumerable<T> Catch<T>(this IEnumerable<T> source, Action<Exception>? action = null)
    {
        return Catch<T, Exception>(source, action);
    }


    /// <summary>
    /// Catch the exception and then omit the value if exception thrown.
    /// </summary>
    public static IEnumerable<T> Catch<T, TException>(this IEnumerable<T> source, Action<TException>? action = null) where TException : Exception
    {
        using var enumerator = source.GetEnumerator();
        while (true)
        {
            T item;
            try
            {
                if (!enumerator.MoveNext())
                    break;
                item = enumerator.Current;
            }
            catch (TException e)
            {
                action?.Invoke(e);
                continue;
            }
            yield return item;
        }
    }
    #endregion

    #region [Arrays]
    public static byte[] TruncateArray(this byte[] byteArray, int len, bool useLINQ = true)
    {
        if (len >= byteArray.Length)
            return byteArray;

        if (useLINQ)
        {
            byte[] tmp = byteArray.Take(len).ToArray();
            return tmp;
        }
        else // Conventional
        {
            byte[] tmp = new byte[len];
            Array.Copy(byteArray, tmp, len);
            return tmp;
        }
    }

    /// <summary>
    /// Yields a column from a jagged array.
    /// An exception will be thrown if the column is out of bounds, and return default in places where there are no elements from inner arrays.
    /// Note: There is no equivalent GetRow method, as you can use array[row] to retrieve.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="rectarray">The source array.</param>
    /// <param name="column">Column record to retrieve, 0-based index.</param>
    /// <returns>Yielded enumerable of column elements for given column, and default values for smaller inner arrays.</returns>
    public static IEnumerable<T?> GetColumn<T>(this T?[][] rectarray, int column)
    {
        if (column < 0 || column >= rectarray.Max(array => array.Length))
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        for (int r = 0; r < rectarray.GetLength(0); r++)
        {
            if (column >= rectarray[r].Length)
            {
                yield return default;

                continue;
            }

            yield return rectarray[r][column];
        }
    }

    /// <summary>
    /// Returns a simple string representation of an array.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The <see cref="string"/> representation of the array.</returns>
    public static string ToArrayString<T>(this T?[] array)
    {
        // The returned string will be in the following format:
        // [1, 2, 3]
        StringBuilder builder = new StringBuilder();

        builder.Append('[');

        for (int i = 0; i < array.Length; i++)
        {
            if (i != 0)
                builder.Append(",\t");

            builder.Append(array[i]?.ToString());
        }

        builder.Append(']');

        return builder.ToString();
    }

    /// <summary>
    /// Returns a simple string representation of a jagged array.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="mdarray">The source array.</param>
    /// <returns>String representation of the array.</returns>
    public static string ToArrayString<T>(this T?[][] mdarray)
    {
        // The returned string uses the same format as the overload for 2D arrays
        StringBuilder builder = new StringBuilder();

        builder.Append('[');

        for (int i = 0; i < mdarray.Length; i++)
        {
            if (i != 0)
            {
                builder.Append(',');
                builder.Append(Environment.NewLine);
                builder.Append(' ');
            }

            builder.Append('[');

            T?[] row = mdarray[i];

            for (int j = 0; j < row.Length; j++)
            {
                if (j != 0)
                    builder.Append(",\t");

                builder.Append(row[j]?.ToString());
            }

            builder.Append(']');
        }

        builder.Append(']');

        return builder.ToString();
    }

    /// <summary>
    /// Returns a simple string representation of a 2D array.
    /// </summary>
    /// <typeparam name="T">The element type of the array.</typeparam>
    /// <param name="array">The source array.</param>
    /// <returns>The <see cref="string"/> representation of the array.</returns>
    public static string ToArrayString<T>(this T?[,] array)
    {
        // The returned string will be in the following format:
        // [[1, 2,  3],
        //  [4, 5,  6],
        //  [7, 8,  9]]
        StringBuilder builder = new StringBuilder();

        builder.Append('[');

        int height = array.GetLength(0);
        int width = array.GetLength(1);

        for (int i = 0; i < height; i++)
        {
            if (i != 0)
            {
                builder.Append(',');
                builder.Append(Environment.NewLine);
                builder.Append(' ');
            }

            builder.Append('[');

            for (int j = 0; j < width; j++)
            {
                if (j != 0)
                    builder.Append(",\t");

                builder.Append(array[i, j]?.ToString());
            }

            builder.Append(']');
        }

        builder.Append(']');

        return builder.ToString();
    }
    #endregion

    #region [Collections]
    /// <summary>
    /// Splits a <see cref="Dictionary{TKey, TValue}"/> into two equal halves.
    /// </summary>
    /// <param name="dictionary"><see cref="Dictionary{TKey, TValue}"/></param>
    /// <returns>tuple</returns>
    public static (Dictionary<string, string> firstHalf, Dictionary<string, string> secondHalf) SplitDictionary(this Dictionary<string, string> dictionary)
    {
        int count = dictionary.Count;

        if (count <= 1)
        {
            // Return the entire dictionary as the first half and an empty dictionary as the second half.
            return (dictionary, new Dictionary<string, string>());
        }

        int halfCount = count / 2;
        var firstHalf = dictionary.Take(halfCount).ToDictionary(kv => kv.Key, kv => kv.Value);
        var secondHalf = dictionary.Skip(halfCount).ToDictionary(kv => kv.Key, kv => kv.Value);

        // Adjust the second half if the count is odd.
        if (count % 2 != 0)
            secondHalf = dictionary.Skip(halfCount + 1).ToDictionary(kv => kv.Key, kv => kv.Value);

        return (firstHalf, secondHalf);
    }

#pragma warning disable 8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
    /// <summary>
    /// Helper for <see cref="System.Collections.Generic.SortedList{TKey, TValue}"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="sortedList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.Generic.SortedList<TKey, TValue> sortedList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (KeyValuePair<TKey, TValue> pair in sortedList) { dictionary.Add(pair.Key, pair.Value); }
        return dictionary;
    }

    /// <summary>
    /// Helper for <see cref="System.Collections.SortedList"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="sortedList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.SortedList sortedList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (DictionaryEntry pair in sortedList) { dictionary.Add((TKey)pair.Key, (TValue)pair.Value); }
        return dictionary;
    }

    /// <summary>
    /// Helper for <see cref="System.Collections.Hashtable"/>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="hashList"></param>
    /// <returns><see cref="Dictionary{TKey, TValue}"/></returns>
    public static Dictionary<TKey, TValue> ConvertToDictionary<TKey, TValue>(this System.Collections.Hashtable hashList)
    {
        Dictionary<TKey, TValue> dictionary = new Dictionary<TKey, TValue>();
        foreach (DictionaryEntry pair in hashList) { dictionary.Add((TKey)pair.Key, (TValue)pair.Value); }
        return dictionary;
    }
#pragma warning restore 8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.

    public static string ToJson<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, string rootName = "")
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("{ ");

        if (!String.IsNullOrEmpty(rootName))
            sb.Append(rootName).Append(": { ");

        foreach (var pair in dictionary)
            sb.Append($"\"{pair.Key}\": \"{pair.Value}\", ");

        sb.Length -= 2; // Get rid of the last ','

        if (!String.IsNullOrEmpty(rootName))
            sb.Append(" }");

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// <see cref="KeyValuePair"/> helper.
    /// </summary>
    /// <param name="valueSet"></param>
    /// <returns>string representation of the <see cref="KeyValuePair"/>s</returns>
    public static string? ToString<K, V>(this IEnumerable<KeyValuePair<K, V>> valueSet)
    {
        if (valueSet == null)
            return null;

        StringBuilder builder = new StringBuilder();

        foreach (KeyValuePair<K, V> value in valueSet)
        {
            builder.Append(value.Key);
            builder.Append(": ");
            builder.Append(value.Value);
            builder.Append('\n');
        }

        // Remove last CRLF
        if (builder.Length > 0)
            builder.Length--;

        return builder.ToString();
    }



    /// <summary>
    /// Removes duplicate entries in a <see cref="List{T}"/>.
    /// </summary>
    /// <param name="list"><see cref="List{T}"/></param>
    public static void RemoveDuplicates<T>(this List<T> list)
    {
        list.Sort();
        var index = 0;
        while (index < list.Count - 1)
        {
            if (Equals(list[index], list[index + 1]))
                list.RemoveAt(index);
            else
                index++;
        }
    }

    /// <summary>
    /// Debugging helper method.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>type name then base type for the object</returns>
    public static string NameOf(this object obj)
    {
        return $"{obj.GetType().Name} => {obj.GetType().BaseType?.Name}";
    }

    /// <summary>
    /// Determine if value is currently the default.
    /// </summary>
    /// <example>
    /// bool result = IsDefaultValue{int}(0);
    /// </example>
    public static bool IsDefaultValue<T>(this T value)
    {
        return object.Equals(default(T), value);
    }

    public static int ToInt(this string stringValue)
    {
        try { return Convert.ToInt32(stringValue, CultureInfo.InvariantCulture); }
        catch (FormatException) { return -1; }
    }

    /// <summary>
    /// Most <see cref="Enum"/>s are represented as int or uint.
    /// </summary>
    public static int ToInt(this Enum enumValue)
    {
        try { return Convert.ToInt32(enumValue, CultureInfo.InvariantCulture); }
        catch (FormatException) { return -1; }
    }

    /// <summary>
    /// This performs no conversion, it reboxes the same value in another type.
    /// </summary>
    /// <example>
    /// object? enumTest = LogLevel.Notice.GetBoxedEnumValue();
    /// Debug.WriteLine($"{enumTest} ({enumTest.GetType()})");
    /// Output: "5 (System.Int32)"
    /// </example>
    public static object GetBoxedEnumValue(this Enum anyEnum)
    {
        Type intType = Enum.GetUnderlyingType(anyEnum.GetType());
        return Convert.ChangeType(anyEnum, intType);
    }

    public static TEnum GetEnum<TEnum>(this string text) where TEnum : struct
    {
        if (!typeof(TEnum).GetTypeInfo().IsEnum)
            throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");

        return (TEnum)Enum.Parse(typeof(TEnum), text);
    }

    public static T? ParseEnum<T>(this string value)
    {
        try { return (T)Enum.Parse(typeof(T), value, true); }
        catch (Exception) { return default(T); }
    }

    /// <summary>
    /// Auto-formatter for time stamps.
    /// "20230516-074401943"
    /// </summary>
    /// <returns>formatted time string</returns>
    public static string GetTimeStamp()
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                             "{0:D4}{1:D2}{2:D2}-{3:D2}{4:D2}{5:D2}{6:D3}",
                             DateTime.Now.Year,
                             DateTime.Now.Month,
                             DateTime.Now.Day,
                             DateTime.Now.Hour,
                             DateTime.Now.Minute,
                             DateTime.Now.Second,
                             DateTime.Now.Millisecond);
    }

    /// <summary>
    /// Helper method for evaluating passed arguments.
    /// </summary>
    public static void PopulateArgDictionary(this string[]? argArray, ref Dictionary<string, string> dict)
    {
        if (argArray != null)
        {
            for (int i = 0; i < argArray.Length; i++)
            {
                var item = argArray[i].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (item.Length % 2 == 0)
                    dict[item[0]] = item[1];
                else
                    Debug.WriteLine($"[WARNING] Index {i} has an odd number of segments.", $"{nameof(Extensions)}");
            }
        }
        else { Debug.WriteLine($"[WARNING] {nameof(argArray)} was null.", $"{nameof(Extensions)}"); }

        // To populate parameters with a typical URI assigning format...
        //string sampleString = "mode=1,state=2,theme=dark";
        //var parameters = Extensions.ParseAssignedValues(sampleString);
        //var code = parameters["mode"];
        //var state = parameters["state"];
        //var theme = parameters["theme"];
    }

    /// <summary>
    /// To populate parameters with a typical URI assigning format.
    /// This method assumes the format is like "mode=1,state=2,theme=dark"
    /// </summary>
    public static Dictionary<string, string> ParseAssignedValues(string inputString, string delimiter = ",")
    {
        Dictionary<string, string> parameters = new();

        try
        {
            var parts = inputString.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
            parameters = parts.Select(x => x.Split("=")).ToDictionary(x => x.First(), x => x.Last());
        }
        catch (Exception ex) { Debug.WriteLine($"ParseAssignedValues: {ex.Message}", $"{nameof(Extensions)}"); }

        return parameters;
    }

    /// <summary>
    /// Creates a new <see cref="Span{T}"/> over an input <see cref="List{T}"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the input <see cref="List{T}"/> instance.</typeparam>
    /// <param name="list">The input <see cref="List{T}"/> instance.</param>
    /// <returns>A <see cref="Span{T}"/> instance with the values of <paramref name="list"/>.</returns>
    /// <remarks>
    /// Note that the returned <see cref="Span{T}"/> is only guaranteed to be valid as long as the items within
    /// <paramref name="list"/> are not modified. Doing so might cause the <see cref="List{T}"/> to swap its
    /// internal buffer, causing the returned <see cref="Span{T}"/> to become out of date. That means that in this
    /// scenario, the <see cref="Span{T}"/> would end up wrapping an array no longer in use. Always make sure to use
    /// the returned <see cref="Span{T}"/> while the target <see cref="List{T}"/> is not modified.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this List<T>? list)
    {
        return CollectionsMarshal.AsSpan(list);
    }

    /// <summary>
    /// Merges the two input <see cref="IReadOnlyDictionary{TKey,TValue}"/> instances and makes sure no duplicate keys are present
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the input dictionaries</typeparam>
    /// <typeparam name="TValue">The type of values in the input dictionaries</typeparam>
    /// <param name="a">The first <see cref="IReadOnlyDictionary{TKey,TValue}"/> to merge</param>
    /// <param name="b">The second <see cref="IReadOnlyDictionary{TKey,TValue}"/> to merge</param>
    /// <returns>An <see cref="IReadOnlyDictionary{TKey,TValue}"/> instance with elements from both <paramref name="a"/> and <paramref name="b"/></returns>
    [Pure]
    public static IReadOnlyDictionary<TKey, TValue> Merge<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> a, IReadOnlyDictionary<TKey, TValue> b)
    {
        if (a.Keys.FirstOrDefault(b.ContainsKey) is TKey key)
            throw new InvalidOperationException($"The key {key} already exists in the current pipeline");

        return new Dictionary<TKey, TValue>(a.Concat(b));
    }

    /// <summary>
    /// Merges the two input <see cref="IReadOnlyCollection{T}"/> instances and makes sure no duplicate items are present
    /// </summary>
    /// <typeparam name="T">The type of elements in the input collections</typeparam>
    /// <param name="a">The first <see cref="IReadOnlyCollection{T}"/> to merge</param>
    /// <param name="b">The second <see cref="IReadOnlyCollection{T}"/> to merge</param>
    /// <returns>An <see cref="IReadOnlyCollection{T}"/> instance with elements from both <paramref name="a"/> and <paramref name="b"/></returns>
    [Pure]
    public static IReadOnlyCollection<T> Merge<T>(this IReadOnlyCollection<T> a, IReadOnlyCollection<T> b)
    {
        if (a.Any(b.Contains))
            throw new InvalidOperationException("The input collection has at least an item already present in the second collection");

        return a.Concat(b).ToArray();
    }
    #endregion

    #region [Strings]
    const string CharactersRegex = "^[A-Za-z]+$";
    const string RemoveHtmlTagsRegex = @"(?></?\w+)(?>(?:[^>'""]+|'[^']*'|""[^""]*"")*)>";
    const string PhoneNumberRegex = @"^[+]?(\d{1,3})?[\s.-]?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}$";
    const string EmailRegex = "(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[\\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21\\x23-\\x5b\\x5d-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])*\")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\\[(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?|[a-z0-9-]*[a-z0-9]:(?:[\\x01-\\x08\\x0b\\x0c\\x0e-\\x1f\\x21-\\x5a\\x53-\\x7f]|\\\\[\\x01-\\x09\\x0b\\x0c\\x0e-\\x7f])+)\\])";
    static readonly Regex RemoveHtmlCommentsRegex = new("<!--.*?-->", RegexOptions.Singleline);
    static readonly Regex RemoveHtmlScriptsRegex = new(@"(?s)<script.*?(/>|</script>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    static readonly Regex RemoveHtmlStylesRegex = new(@"(?s)<style.*?(/>|</style>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Determines whether a string is a valid email address.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <returns><c>true</c> for a valid email address; otherwise, <c>false</c>.</returns>
    public static bool IsEmail(this string str) => Regex.IsMatch(str, EmailRegex);

    /// <summary>
    /// Determines whether a string is a valid decimal number.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <returns><c>true</c> for a valid decimal number; otherwise, <c>false</c>.</returns>
    public static bool IsDecimal([NotNullWhen(true)] this string? str)
    {
        return decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Determines whether a string is a valid integer.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <returns><c>true</c> for a valid integer; otherwise, <c>false</c>.</returns>
    public static bool IsNumeric([NotNullWhen(true)] this string? str)
    {
        return int.TryParse(str, out _);
    }

    /// <summary>
    /// Determines if a string is fully numeric utilizing IFormatProvider and considering currency symbols.
    /// </summary>
    public static bool IsNumericIgnoreCurrency(this string value)
    {
        bool retVal = false;
        if (value != null)
        {
            value = value.Trim();
            if (value != "")
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.AllowCurrencySymbol | System.Globalization.NumberStyles.Number, Thread.CurrentThread.CurrentUICulture, out double result))
                    retVal = true;
            }
        }
        return retVal;
    }

    /// <summary>
    /// Determines whether a string is a valid phone number.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <returns><c>true</c> for a valid phone number; otherwise, <c>false</c>.</returns>
    public static bool IsPhoneNumber(this string str) => Regex.IsMatch(str, PhoneNumberRegex);

    /// <summary>
    /// Determines whether a string contains only letters.
    /// </summary>
    /// <param name="str">The string to test.</param>
    /// <returns><c>true</c> if the string contains only letters; otherwise, <c>false</c>.</returns>
    public static bool IsCharacterString(this string str) => Regex.IsMatch(str, CharactersRegex);

    /// <summary>
    /// Returns a string with HTML comments, scripts, styles, and tags removed.
    /// </summary>
    /// <param name="htmlText">HTML string.</param>
    /// <returns>Decoded HTML string.</returns>
    [return: NotNullIfNotNull("htmlText")]
    public static string? DecodeHtml(this string? htmlText)
    {
        if (htmlText is null)
        {
            return null;
        }

        var ret = htmlText.SanitizeHtml();

        // Remove html tags
        ret = new Regex(RemoveHtmlTagsRegex).Replace(ret, string.Empty);

        return WebUtility.HtmlDecode(ret);
    }

    /// <summary>
    /// Returns a string with HTML comments, scripts, and styles removed.
    /// </summary>
    /// <param name="html">HTML string to clean</param>
    /// <returns>cleaned HTML string</returns>
    public static string SanitizeHtml(this string html)
    {
        // Remove comments
        var withoutComments = RemoveHtmlCommentsRegex.Replace(html, string.Empty);

        // Remove scripts
        var withoutScripts = RemoveHtmlScriptsRegex.Replace(withoutComments, string.Empty);

        // Remove styles
        var withoutStyles = RemoveHtmlStylesRegex.Replace(withoutScripts, string.Empty);

        return withoutStyles;
    }

    /// <summary>
    /// Completely removes HTML formatting from an input string.
    /// </summary>
    public static string StripHtml(this string input)
    {
        var tagsExpression = new System.Text.RegularExpressions.Regex(@"</?.+?>");
        return tagsExpression.Replace(input, " ");
    }


    /// <summary>
    /// Truncates a string to the specified length.
    /// </summary>
    /// <param name="value">The string to be truncated.</param>
    /// <param name="length">The maximum length.</param>
    /// <returns>Truncated string.</returns>
    public static string Truncate(this string? value, int length) => Truncate(value, length, false);

    /// <summary>
    /// Provide better linking for resourced strings.
    /// </summary>
    /// <param name="format">The format of the string being linked.</param>
    /// <param name="args">The object which will receive the linked String.</param>
    /// <returns>Truncated string.</returns>
    public static string AsFormat(this string format, params object[] args)
    {
        // Note: this extension was originally added to help developers using {x:Bind} in XAML, but
        // due to a known limitation in the UWP/WinUI XAML compiler, using either this method or the
        // standard string.Format method from the BCL directly doesn't always work. Since this method
        // doesn't actually provide any benefit over the built-in one, it has been marked as obsolete.
        // For more details, see the WinUI issue on the XAML compiler limitation here:
        // https://github.com/microsoft/microsoft-ui-xaml/issues/2654.
        return string.Format(format, args);
    }

    /// <summary>
    /// Truncates a string to the specified length.
    /// </summary>
    /// <param name="value">The string to be truncated.</param>
    /// <param name="length">The maximum length.</param>
    /// <param name="ellipsis"><c>true</c> to add ellipsis to the truncated text; otherwise, <c>false</c>.</param>
    /// <returns>Truncated string.</returns>
    public static string Truncate(this string? value, int length, bool ellipsis)
    {
        if (!string.IsNullOrEmpty(value))
        {
            value = value!.Trim();

            if (value.Length > length)
            {
                if (ellipsis)
                {
                    return value.Substring(0, length) + "...";
                }

                return value.Substring(0, length);
            }
        }

        return value ?? string.Empty;
    }

    public static bool HasAlpha(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[+a-zA-Z]+");
    }

    public static bool HasNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x));
    }
    public static bool HasNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[0-9]+"); // [^\D+]
    }

    public static bool HasSpace(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsSeparator(x));
    }
    public static bool HasSpaceRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", @"[\s]+");
    }

    public static bool HasPunctuation(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsPunctuation(x));
    }

    public static bool HasAlphaNumeric(this string str)
    {
        if (string.IsNullOrEmpty(str)) { return false; }
        return str.Any(x => char.IsNumber(x)) && str.Any(x => char.IsLetter(x));
    }
    public static bool HasAlphaNumericRegex(this string str)
    {
        return Regex.IsMatch(str ?? "", "[a-zA-Z0-9]+");
    }

    public static string RemoveAlphas(this string str)
    {
        return string.Concat(str?.Where(c => char.IsNumber(c) || c == '.') ?? string.Empty);
    }

    public static string RemoveNumerics(this string str)
    {
        return string.Concat(str?.Where(c => char.IsLetter(c)) ?? string.Empty);
    }

    public static string RemoveExtraSpaces(this string strText)
    {
        if (!string.IsNullOrEmpty(strText))
            strText = Regex.Replace(strText, @"\s+", " ");

        return strText;
    }

    /// <summary>
    /// String normalize helper.
    /// </summary>
    /// <param name="strThis"></param>
    /// <returns>sanitized string</returns>
    public static string? RemoveDiacritics(this string strThis)
    {
        if (strThis == null)
            return null;

        var sb = new StringBuilder();

        foreach (char c in strThis.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// ExampleTextSample => Example Text Sample
    /// </summary>
    /// <param name="input"></param>
    /// <returns>space delimited string</returns>
    public static string SeparateCamelCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        StringBuilder result = new StringBuilder();
        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
                result.Append(' ');

            result.Append(input[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Helper for parsing command line arguments.
    /// </summary>
    /// <param name="inputArray"></param>
    /// <returns>string array of args excluding the 1st arg</returns>
    public static string[] IgnoreFirstTakeRest(this string[] inputArray)
    {
        if (inputArray.Length > 1)
            return inputArray.Skip(1).ToArray();
        else
            return new string[0];
    }

    /// <summary>
    /// Returns the first element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractFirst("{tag}", '{', '}');
    /// </example>
    public static string ExtractFirst(this string text, char start, char end)
    {
        string pattern = @"\" + start + "(.*?)" + @"\" + end; //pattern = @"\{(.*?)\}"
        Match match = Regex.Match(text, pattern);
        if (match.Success)
            return match.Groups[1].Value;
        else
            return "";
    }

    /// <summary>
    /// Returns the last element from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    /// <example>
    /// var clean = ExtractLast("{tag}", '{', '}');
    /// </example>
    public static string ExtractLast(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        if (matches.Count > 0)
        {
            Match lastMatch = matches[matches.Count - 1];
            return lastMatch.Groups[1].Value;
        }
        else
            return "";
    }

    /// <summary>
    /// Returns all the elements from a tokenized string, e.g.
    /// Input:"{tag}"  Output:"tag"
    /// </summary>
    public static string[] ExtractAll(this string text, char start, char end)
    {
        string pattern = @"\" + start + @"(.*?)\" + end; //pattern = @"\{(.*?)\}"
        MatchCollection matches = Regex.Matches(text, pattern);
        string[] results = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++)
            results[i] = matches[i].Groups[1].Value;

        return results;
    }

    /// <summary>
    /// Returns the specified occurrence of a character in a string.
    /// </summary>
    /// <returns>
    /// Index of requested occurrence if successful, -1 otherwise.
    /// </returns>
    /// <example>
    /// If you wanted to find the second index of the percent character in a string:
    /// int index = "blah%blah%blah".IndexOfNth('%', 2);
    /// </example>
    public static int IndexOfNth(this string input, char character, int position)
    {
        int index = -1;
        
        if (string.IsNullOrEmpty(input))
            return index;

        for (int i = 0; i < position; i++)
        {
            index = input.IndexOf(character, index + 1);
            if (index == -1) { break; }
        }

        return index;
    }

    /// <summary>
    /// Attempts to convert a string into a decimal utilizing IFormatProvider and considering currency symbols.
    /// </summary>
    public static decimal ToDecimal(this string value, decimal defaultValue)
    {
        decimal retVal = 0;
        if (value != null)
        {
            value = value.Trim();
            if (IsNumericIgnoreCurrency(value))
            {
                if (decimal.TryParse(value, System.Globalization.NumberStyles.AllowCurrencySymbol | System.Globalization.NumberStyles.Number, Thread.CurrentThread.CurrentUICulture, out decimal result))
                    retVal = result;
            }
        }
        if (retVal == 0)
            retVal = defaultValue;

        return retVal;
    }

    /// <summary>
    /// Attempts to convert a string into an integer utilizing IFormatProvider and considering currency symbols.
    /// </summary>
    public static int ToInteger(this string value, int defaultValue)
    {
        int retVal = 0;
        if (value != null)
        {
            value = value.Trim();
            if (IsNumericIgnoreCurrency(value))
            {
                if (int.TryParse(value, System.Globalization.NumberStyles.AllowCurrencySymbol | System.Globalization.NumberStyles.Number, Thread.CurrentThread.CurrentUICulture, out int result))
                    retVal = result;
            }
        }
        if (retVal == 0)
            retVal = defaultValue;

        return retVal;
    }

    /// <summary>
    /// This function uses the substring routine to take the first <paramref name="length"/> characters 
    /// in a string. If the string is larger than <paramref name="length"/>, the <paramref name="message"/> 
    /// text will be appended to the end of the string instead.
    /// </summary>
    public static string Truncate(this string s, int length, string message)
    {
        if (s.Length > length)
            return String.Format("{0}…{1}", s.Substring(0, length).Trim(), message);

        return s;
    }

    public static string GetLast(this string source, int numChars)
    {
        if (numChars >= source.Length)
            return source;

        return source.Substring(source.Length - numChars);
    }

    /// <summary>
    /// Returns the input <paramref name="str"/> as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="encoding"></param>
    public static ReadOnlySpan<byte> AsSpan(this string str, Encoding encoding)
    {
        int byteCount = encoding.GetByteCount(str);
        byte[] bytes = new byte[byteCount];
        encoding.GetBytes(str, 0, str.Length, bytes, 0);
        return new ReadOnlySpan<byte>(bytes);
    }

    /// <summary>
    /// Tokenizes the values in the input <see cref="string"/> instance using a specified separator.
    /// This extension should be used directly within a <see langword="foreach"/> loop:
    /// <code>
    /// string text = "Hello, world!";
    /// foreach (var token in text.Tokenize(','))
    /// {
    ///     /* Access the tokens here */
    /// }
    /// </code>
    /// The compiler will take care of properly setting up the <see langword="foreach"/> loop with the type returned from this method.
    /// </summary>
    /// <param name="text">The source <see cref="string"/> to tokenize.</param>
    /// <param name="separator">The separator character to use.</param>
    /// <returns>A wrapper type that will handle the tokenization for <paramref name="text"/>.</returns>
    /// <remarks>The returned <see cref="ReadOnlySpanTokenizer{T}"/> value shouldn't be used directly: use this extension in a <see langword="foreach"/> loop.</remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpanTokenizer<char> Tokenize(this string text, char separator)
    {
        return new(text.AsSpan(), separator);
    }

    /// <summary>
    /// <see cref="System.Xml.XmlWriter"/> offers a StringBuilder as an output.
    /// </summary>
    /// <param name="xmlString"></param>
    /// <returns>human-friendly format</returns>
    public static string FormatXml(string xmlString)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            var element = System.Xml.Linq.XElement.Parse(xmlString);
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = settings.Indent = settings.NewLineOnAttributes = true;
            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                element.Save(xmlWriter);
            }
            return stringBuilder.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FormatXml: {ex.Message}");
            return string.Empty;
        }
    }

    public static string BeautifyXml(this string xml)
    {
        try
        {
            var stringBuilder = new StringBuilder();
            var element = System.Xml.Linq.XElement.Parse(xml);
            var settings = new XmlWriterSettings();
            settings.OmitXmlDeclaration = settings.Indent = settings.NewLineOnAttributes = true;
            // XmlWriter offers a StringBuilder as an output.
            using (var xmlWriter = XmlWriter.Create(stringBuilder, settings))
            {
                element.Save(xmlWriter);
            }
            return stringBuilder.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PrettyXml(ERROR): {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// This method takes in an array of string URLs as an argument using the params 
    /// keyword, which allows you to pass any number of arguments to the method.
    /// </summary>
    public static string? CombineUrls(params string[] urls)
    {
        if (urls == null)
            return null;

        if (urls.Length == 0)
            return "";

        string result = urls[0].TrimEnd('/');
        for (int i = 1; i < urls.Length; i++)
        {
            string url = urls[i];

            if (string.IsNullOrEmpty(url))
                continue;

            if (!result.Contains(url))
            {
                url = url.TrimStart('/');
                result += "/" + url;
            }
        }

        return result;
    }

    /// <summary>
    /// Formats a string for AppX syntax.
    /// </summary>
    /// <example>
    /// TextureUri = "/Assets/SomeImage.png".ToAppxUri();
    /// </example>
    /// <param name="path">the relative asset path</param>
    /// <returns><see cref="Uri"/></returns>
    [Pure]
    public static Uri ToAppxUri(this string path)
    {
        string prefix = $"ms-appx://{(path.StartsWith('/') ? string.Empty : "/")}";
        return new Uri($"{prefix}{path}");
    }

    /// <summary>
    /// Returns an <see cref="Uri"/> that starts with the ms-appx:// prefix
    /// </summary>
    /// <param name="uri">The input <see cref="Uri"/> to process</param>
    /// <returns>A <see cref="Uri"/> equivalent to the first but relative to ms-appx://</returns>
    /// <remarks>This is needed because the XAML converter doesn't use the ms-appx:// prefix</remarks>
    [Pure]
    public static Uri ToAppxUri(this Uri uri)
    {
        if (uri.Scheme.Equals("ms-resource"))
        {
            string path = uri.AbsolutePath.StartsWith("/Files") ? uri.AbsolutePath.Replace("/Files", string.Empty) : uri.AbsolutePath;
            return new Uri($"ms-appx://{path}");
        }
        return uri;
    }

    /// <summary>
    /// Compares one URI with another URI.
    /// </summary>
    /// <param name="uri">URI to compare with</param>
    /// <param name="otherUri">URI to compare</param>
    /// <returns>true if yes, false otherwise</returns>
    public static bool IsEqualTo(this Uri uri, Uri otherUri)
    {
        return Uri.Compare(uri, otherUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }


    /// <summary>
    /// We'll add <see cref="MethodImplOptions.AggressiveInlining"/> to prevent an unnecessary stack push.
    /// </summary>
    /// <param name="str">string to convert to <see cref="Stream"/></param>
    /// <returns><see cref="Stream"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Stream ToStream(this string str)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(str);
        return new MemoryStream(byteArray);
    }

    /// <summary>
    /// Returns a <see cref="Stream"/> wrapping the contents of the given <see cref="Memory{T}"/> of <see cref="byte"/> instance.
    /// </summary>
    /// <param name="memory">The input <see cref="Memory{T}"/> of <see cref="byte"/> instance.</param>
    /// <returns>A <see cref="Stream"/> wrapping the data within <paramref name="memory"/>.</returns>
    /// <remarks>
    /// Since this method only receives a <see cref="Memory{T}"/> instance, which does not track
    /// the lifetime of its underlying buffer, it is responsibility of the caller to manage that.
    /// In particular, the caller must ensure that the target buffer is not disposed as long
    /// as the returned <see cref="Stream"/> is in use, to avoid unexpected issues.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="memory"/> has an invalid data store.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Stream AsStream(this Memory<byte> memory)
    {
        return new MemoryStream(memory.ToArray(), false);
    }

    /// <summary>
    /// Asynchronously reads a sequence of bytes from a given <see cref="Stream"/> instance.
    /// </summary>
    /// <param name="stream">The source <see cref="Stream"/> to read data from.</param>
    /// <param name="buffer">The destination <see cref="Memory{T}"/> to write data to.</param>
    /// <param name="cancellationToken">The optional <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation being performed.</returns>
    public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return new ValueTask<int>(Task.FromCanceled<int>(cancellationToken));

        // If the memory wraps an array, extract it and use it directly
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));

        // Local function used as the fallback path. This happens when the input memory
        // doesn't wrap an array instance we can use. We use a local function as we need
        // the body to be asynchronous, in order to execute the finally block after the
        // write operation has been completed. By separating the logic, we can keep the
        // main method as a synchronous, value-task returning function. This fallback
        // path should hopefully be pretty rare, as memory instances are typically just
        // created around arrays, often being rented from a memory pool in particular.
        static async Task<int> ReadAsyncFallback(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] rent = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                int bytesRead = await stream.ReadAsync(rent, 0, buffer.Length, cancellationToken);

                if (bytesRead > 0)
                    rent.AsSpan(0, bytesRead).CopyTo(buffer.Span);

                return bytesRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rent);
            }
        }

        return new ValueTask<int>(ReadAsyncFallback(stream, buffer, cancellationToken));
    }

    /// <summary>
    /// Asynchronously writes a sequence of bytes to a given <see cref="Stream"/> instance.
    /// </summary>
    /// <param name="stream">The destination <see cref="Stream"/> to write data to.</param>
    /// <param name="buffer">The source <see cref="ReadOnlyMemory{T}"/> to read data from.</param>
    /// <param name="cancellationToken">The optional <see cref="CancellationToken"/> for the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the operation being performed.</returns>
    public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return new ValueTask(Task.FromCanceled(cancellationToken));

        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
            return new ValueTask(stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));

        // Local function, same idea as above
        static async Task WriteAsyncFallback(Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] rent = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                buffer.Span.CopyTo(rent);
                await stream.WriteAsync(rent, 0, buffer.Length, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rent);
            }
        }

        return new ValueTask(WriteAsyncFallback(stream, buffer, cancellationToken));
    }

    /// <summary>
    /// Reads a sequence of bytes from a given <see cref="Stream"/> instance.
    /// </summary>
    /// <param name="stream">The source <see cref="Stream"/> to read data from.</param>
    /// <param name="buffer">The target <see cref="Span{T}"/> to write data to.</param>
    /// <returns>The number of bytes that have been read.</returns>
    public static int Read(this Stream stream, Span<byte> buffer)
    {
        byte[] rent = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            int bytesRead = stream.Read(rent, 0, buffer.Length);

            if (bytesRead > 0)
                rent.AsSpan(0, bytesRead).CopyTo(buffer);

            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }

    /// <summary>
    /// Writes a sequence of bytes to a given <see cref="Stream"/> instance.
    /// </summary>
    /// <param name="stream">The destination <see cref="Stream"/> to write data to.</param>
    /// <param name="buffer">The source <see cref="Span{T}"/> to read data from.</param>
    public static void Write(this Stream stream, ReadOnlySpan<byte> buffer)
    {
        byte[] rent = ArrayPool<byte>.Shared.Rent(buffer.Length);

        try
        {
            buffer.CopyTo(rent);
            stream.Write(rent, 0, buffer.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rent);
        }
    }
    #endregion

    #region [Settings]
    /// <summary>
    /// To be used with a config setting group, typically a <see cref="List{T}"/>.
    /// </summary>
    /// <example>
    /// var result = settings.Update("[lastrun]", $"{DateTime.Now}");
    /// </example>
    public static bool Update(this List<string> source, string? tag, string? newValue, bool addIfNotFound = true)
    {
        if (source == null)
            return false;

        tag ??= "[empty]";
        newValue ??= "";

        int index = source.FindIndex(s => s.Contains(tag));

        if (index != -1)
            source[index] = $"{tag}{newValue}";
        else if (addIfNotFound)
            source.Add($"{tag}{newValue}");
        else
            return false;

        return true;
    }

    /// <summary>
    /// To be used with a config setting group, typically a <see cref="List{T}"/>.
    /// </summary>
    /// <example>
    /// var result = settings.Fetch("[lastrun]");
    /// </example>
    public static string Fetch(this List<string> source, string? tag)
    {
        if (source == null)
            return string.Empty;

        tag ??= "[empty]";

        int index = source.FindIndex(s => s.Contains(tag));

        if (index != -1)
            return source[index].Replace(tag, "");
        else
            return string.Empty;
    }
    #endregion

    #region [Tasks]
    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeoutInMilliseconds">Timeout duration in Milliseconds.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public async static Task<T> WithTimeout<T>(this Task<T> task, int timeoutInMilliseconds)
    {
        var retTask = await Task.WhenAny(task, Task.Delay(timeoutInMilliseconds))
            .ConfigureAwait(false);

        #pragma warning disable CS8603 // Possible null reference return.
        return retTask is Task<T> ? task.Result : default;
        #pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Task extension to add a timeout.
    /// </summary>
    /// <returns>The task with timeout.</returns>
    /// <param name="task">Task.</param>
    /// <param name="timeout">Timeout Duration.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout) => WithTimeout(task, (int)timeout.TotalMilliseconds);

     #pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
    /// <summary>
    /// Attempts to await on the task and catches exception
    /// </summary>
    /// <param name="task">Task to execute</param>
    /// <param name="onException">What to do when method has an exception</param>
    /// <param name="continueOnCapturedContext">If the context should be captured.</param>
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onException = null, bool continueOnCapturedContext = false)
    #pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void
    {
        try
        {
            await task.ConfigureAwait(continueOnCapturedContext);
        }
        catch (Exception ex) when (onException != null)
        {
            onException.Invoke(ex);
        }
    }

    /// <summary>
    /// Chainable task helper.
    /// var result = await SomeLongAsyncFunction().WithCancellation(cts.Token);
    /// </summary>
    /// <typeparam name="TResult">the type of task result</typeparam>
    /// <returns><see cref="Task"/>TResult</returns>
    public static Task<TResult> WithCancellation<TResult>(this Task<TResult> task, CancellationToken cancelToken)
    {
        TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
        CancellationTokenRegistration reg = cancelToken.Register(() => tcs.TrySetCanceled());
        task.ContinueWith(ant =>
        {
            reg.Dispose(); // NOTE: it's important to dispose of CancellationTokenRegistrations or they will hand around in memory until the application closes
            if (ant.IsCanceled)
                tcs.TrySetCanceled();
            else if (ant.IsFaulted)
                tcs.TrySetException(ant.Exception.InnerException);
            else
                tcs.TrySetResult(ant.Result);
        });
        return tcs.Task;  // Return the TaskCompletionSource result
    }

    public static Task<T> WithAllExceptions<T>(this Task<T> task)
    {
        TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        task.ContinueWith(ignored =>
        {
            switch (task.Status)
            {
                case TaskStatus.Canceled:
                    Debug.WriteLine($"[TaskStatus.Canceled]");
                    tcs.SetCanceled();
                    break;
                case TaskStatus.RanToCompletion:
                    tcs.SetResult(task.Result);
                    Debug.WriteLine($"[TaskStatus.RanToCompletion]: {task.Result}");
                    break;
                case TaskStatus.Faulted:
                    // SetException will automatically wrap the original AggregateException in another
                    // one. The new wrapper will be removed in TaskAwaiter, leaving the original intact.
                    Debug.WriteLine($"[TaskStatus.Faulted]: {task.Exception?.Message}");
                    tcs.SetException(task.Exception ?? new Exception("Exception object was null"));
                    break;
                default:
                    Debug.WriteLine($"[TaskStatus.Invalid]: Continuation called illegally.");
                    tcs.SetException(new InvalidOperationException("Continuation called illegally."));
                    break;
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Task.Factory.StartNew (() => { throw null; }).IgnoreExceptions();
    /// </summary>
    public static void IgnoreExceptions(this Task task, bool logEx = false)
    {
        task.ContinueWith(t =>
        {
            AggregateException? ignore = t.Exception;

            ignore?.Flatten().Handle(ex =>
            {
                if (logEx)
                    Debug.WriteLine("Exception type: {0}\r\nException Message: {1}", ex.GetType(), ex.Message);
                return true; // don't re-throw
            });

        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public static Task ContinueWithState<TState>(this Task task, Action<Task, TState> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken,
            continuationOptions,
            TaskScheduler.Default);
    }

    public static Task ContinueWithState<TResult, TState>(this Task<TResult> task, Action<Task<TResult>, TState> continuationAction, TState state, CancellationToken cancellationToken)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task<TResult>, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken);
    }

    public static Task ContinueWithState<TResult, TState>(this Task<TResult> task, Action<Task<TResult>, TState> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions)
    {
        return task.ContinueWith(
            (t, tupleObject) =>
            {
                var (closureAction, closureState) = ((Action<Task<TResult>, TState>, TState))tupleObject!;

                closureAction(t, closureState);
            },
            (continuationAction, state),
            cancellationToken,
            continuationOptions,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Gets the result of a <see cref="Task"/> if available, or <see langword="null"/> otherwise.
    /// </summary>
    /// <param name="task">The input <see cref="Task"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>
    /// This method does not block if <paramref name="task"/> has not completed yet. Furthermore, it is not generic
    /// and uses reflection to access the <see cref="Task{TResult}.Result"/> property and boxes the result if it's
    /// a value type, which adds overhead. It should only be used when using generics is not possible.
    /// </remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object? GetResultOrDefault(this Task task)
    {
        // Check if the instance is a completed Task
        if (
#if NETSTANDARD2_1
            task.IsCompletedSuccessfully
#else
            task.Status == TaskStatus.RanToCompletion
#endif
        ){
            // Try to get the Task<T>.Result property. This method would've
            // been called anyway after the type checks, but using that to
            // validate the input type saves some additional reflection calls.
            // Furthermore, doing this also makes the method flexible enough to
            // cases whether the input Task<T> is actually an instance of some
            // runtime-specific type that inherits from Task<T>.
            PropertyInfo? propertyInfo =
#if NETSTANDARD1_4
                task.GetType().GetRuntimeProperty(nameof(Task<object>.Result));
#else
                task.GetType().GetProperty(nameof(Task<object>.Result));
#endif
            // Return the result, if possible
            return propertyInfo?.GetValue(task);
        }

        return null;
    }

    /// <summary>
    /// Gets the result of a <see cref="Task{TResult}"/> if available, or <see langword="default"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="Task{TResult}"/> to get the result for.</typeparam>
    /// <param name="task">The input <see cref="Task{TResult}"/> instance to get the result for.</param>
    /// <returns>The result of <paramref name="task"/> if completed successfully, or <see langword="default"/> otherwise.</returns>
    /// <remarks>This method does not block if <paramref name="task"/> has not completed yet.</remarks>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? GetResultOrDefault<T>(this Task<T?> task)
    {
#if NETSTANDARD2_1
        return task.IsCompletedSuccessfully ? task.Result : default;
#else
        return task.Status == TaskStatus.RanToCompletion ? task.Result : default;
#endif
    }
    #endregion

    #region [Files and Folders]
    // https://stackoverflow.com/questions/62771/how-do-i-check-if-a-given-string-is-a-legal-valid-file-name-under-windows
    public static readonly Regex ValidWindowsFileNames = new Regex(@"^(?!(?:PRN|AUX|CLOCK\$|NUL|CON|COM\d|LPT\d)(?:\..+)?$)[^\x00-\x1F\xA5\\?*:\"";|\/<>]+(?<![\s.])$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns the AppData path including the submodule.
    /// e.g. "C:\Users\UserName\AppData\Local\RepoBackup\Settings"
    /// </summary>
    public static string LocalApplicationDataFolder(string moduleName = "Settings")
    {
        var result = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}\\{moduleName}");
        return result;
    }

    public static async Task<SoftwareBitmap> LoadFromFile(StorageFile file)
    {
        SoftwareBitmap softwareBitmap;
        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
        {
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
        }
        return softwareBitmap;
    }

    public static async Task<string> LoadText(string relativeFilePath)
    {
#if IS_UNPACKAGED
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), relativeFilePath));
        var file = await StorageFile.GetFileFromPathAsync(sourcePath);
#else
        Uri sourceUri = new Uri("ms-appx:///" + relativeFilePath);
        var file = await StorageFile.GetFileFromApplicationUriAsync(sourceUri);
#endif
        return await FileIO.ReadTextAsync(file);
    }

    public static async Task<IList<string>> LoadLines(string relativeFilePath)
    {
        string fileContents = await LoadText(relativeFilePath);
        return fileContents.Split(Environment.NewLine).ToList();
    }

    public static async Task<DateTimeOffset> GetModifiedDate(this IStorageItem file)
    {
        return (await file.GetBasicPropertiesAsync()).DateModified;
    }

    public static async Task<ulong> GetSize(this IStorageItem file)
    {
        return (await file.GetBasicPropertiesAsync()).Size;
    }

    /// <summary>
    /// <see cref="StorageFile"/> helper to check whether a file should be re-cached.
    /// </summary>
    /// <param name="file">storage file</param>
    /// <param name="duration">cache duration</param>
    /// <returns>true if file has expired, false otherwise</returns>
    public static async Task<bool> IsCacheFileOldAsync(this StorageFile? file, TimeSpan duration)
    {
        if (file == null)
            return false;

        // Setup some extended properties to examine, if needed.
        List<string> _extendedProperties = new List<string>();
        _extendedProperties.Add("System.DateAccessed");
        _extendedProperties.Add("System.ZoneIdentifier"); // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-fscc/6e3f7352-d11c-4d76-8c39-2516a9df36e8
        // ZoneId=0: Local machine
        // ZoneId=1: Local intranet
        // ZoneId=2: Trusted sites
        // ZoneId=3: Internet
        // ZoneId=4: Restricted sites

        #region [Other Property Names]
        /*
         System.CachedFileUpdaterContentIdForStream
         System.Capacity
         System.Category
         System.Comment
         System.Company
         System.ComputerName
         System.ContainedItems
         System.ContentStatus
         System.ContentType
         System.Copyright
         System.CreatorAppId
         System.CreatorOpenWithUIOptions
         System.DataObjectFormat
         System.DateAccessed
         System.DateAcquired
         System.DateArchived
         System.DateCompleted
         System.DateCreated
         System.DateImported
         System.DateModified
         System.DefaultSaveLocationDisplay
         System.DueDate
         System.EndDate
         System.ExpandoProperties
         System.FileAllocationSize
         System.FileAttributes
         System.FileCount
         System.FileDescription
         System.FileExtension
         System.FileFRN
         System.FileName
         System.FileOfflineAvailabilityStatus
         System.FileOwner
         System.FilePlaceholderStatus
         System.FileVersion
         System.FindData
         System.FlagColor
         System.FlagColorText
         System.FlagStatus
         System.FlagStatusText
         System.FolderKind
         System.FolderNameDisplay
         System.FreeSpace
         System.FullText
         System.HighKeywords
         System.ImageParsingName
         System.Importance
         System.ImportanceText
         System.IsAttachment
         System.IsDefaultNonOwnerSaveLocation
         System.IsDefaultSaveLocation
         System.IsDeleted
         System.IsEncrypted
         System.IsFlagged
         System.IsFlaggedComplete
         System.IsIncomplete
         System.IsLocationSupported
         System.IsPinnedToNameSpaceTree
         System.IsRead
         System.IsSearchOnlyItem
         System.IsSendToTarget
         System.IsShared
         System.ItemAuthors
         System.ItemClassType
         System.ItemDate
         System.ItemFolderNameDisplay
         System.ItemFolderPathDisplay
         System.ItemFolderPathDisplayNarrow
         System.ItemName
         System.ItemNameDisplay
         System.ItemNameDisplayWithoutExtension
         System.ItemNamePrefix
         System.ItemNameSortOverride
         System.ItemParticipants
         System.ItemPathDisplay
         System.ItemPathDisplayNarrow
         System.ItemSubType
         System.ItemType
         System.ItemTypeText
         System.ItemUrl
         System.Keywords
         System.Kind
         System.KindText
         System.Language
         System.LastSyncError
         System.LastWriterPackageFamilyName
         System.LowKeywords
         System.MediumKeywords
         System.MileageInformation
         System.MIMEType
         System.Null
         System.OfflineAvailability
         System.OfflineStatus
         System.OriginalFileName
         System.OwnerSID
         System.ParentalRating
         System.ParentalRatingReason
         System.ParentalRatingsOrganization
         System.ParsingBindContext
         System.ParsingName
         System.ParsingPath
         System.PerceivedType
         System.PercentFull
         System.Priority
         System.PriorityText
         System.Project
         System.ProviderItemID
         System.Rating
         System.RatingText
         System.RemoteConflictingFile
         System.Sensitivity
         System.SensitivityText
         System.SFGAOFlags
         System.SharedWith
         System.ShareUserRating
         System.SharingStatus
         System.Shell.OmitFromView
         System.SimpleRating
         System.Size
         System.SoftwareUsed
         System.SourceItem
         System.SourcePackageFamilyName
         System.StartDate
         System.Status
         System.StorageProviderCallerVersionInformation
         System.StorageProviderError
         System.StorageProviderFileChecksum
         System.StorageProviderFileIdentifier
         System.StorageProviderFileRemoteUri
         System.StorageProviderFileVersion
         System.StorageProviderFileVersionWaterline
         System.StorageProviderId
         System.StorageProviderShareStatuses
         System.StorageProviderSharingStatus
         System.StorageProviderStatus
         System.Subject
         System.SyncTransferStatus
         System.Thumbnail
         System.ThumbnailCacheId
         System.ThumbnailStream
         System.Title
         System.TitleSortOverride
         System.TotalFileSize
         System.Trademarks
         System.TransferOrder
         System.TransferPosition
         System.TransferSize
         System.VolumeId
         System.ZoneIdentifier
        */
        #endregion

        // Get extended properties.
        IDictionary<string, object> extraProperties = await file.Properties.RetrievePropertiesAsync(_extendedProperties).AsTask().ConfigureAwait(false);

        // We are only interested in the date-accessed property.
        var propValue = extraProperties["System.DateAccessed"];

        if (propValue != null)
        {
            var lastAccess = propValue as DateTimeOffset?;
            if (lastAccess.HasValue)
            {
                return DateTime.Now.Subtract(lastAccess.Value.DateTime) > duration;
            }
        }

        // If extended property retrieval fails, just examine basic date modified file property.
        var properties = await file.GetBasicPropertiesAsync().AsTask().ConfigureAwait(false);
        return properties.Size == 0 || DateTime.Now.Subtract(properties.DateModified.DateTime) > duration;
    }

    /// <summary>
    /// If false is returned you can call Marshal.GetLastWin32Error() to get the context specific error code.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns>true if successful, false otherwise</returns>
    public static bool UnblockFile(string fileName)
    {
        return NativeMethods.DeleteFile(fileName + ":Zone.Identifier");
    }
    public static void UnblockPath(string path)
    {
        try
        {
            string[] files = System.IO.Directory.GetFiles(path);
            string[] dirs = System.IO.Directory.GetDirectories(path);

            foreach (string file in files)
            {
                if (!UnblockFile(file))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err == 0)
                        Debug.WriteLine($"> No error detected.");
                    else if (err == 1)
                        Debug.WriteLine($"> Code {err}: The file does not exist. ({file})");
                    else if (err == 2)
                        Debug.WriteLine($"> Code {err}: The alternate stream is not present. ({file})");
                    else if (err == 3)
                        Debug.WriteLine($"> Code {err}: The process does not have sufficient rights to delete the alternate stream. ({file})");
                    else
                        Debug.WriteLine($"> Unknown error during unblock: {err}. ({file})");
                }
                else
                {
                    Debug.WriteLine($"> Successfully unblocked '{file}'");
                }
            }

            foreach (string dir in dirs)
            {
                UnblockPath(dir);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UnblockPath: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a List of type FileInfo based on the target folder and file extension requested.
    /// </summary>
    public static List<System.IO.FileInfo> AddRangeOfFiles(string targetDirectory, string fileExt)
    {
        List<System.IO.FileInfo> files = new List<System.IO.FileInfo>();
        var targetDir = new System.IO.DirectoryInfo(targetDirectory);
        files.AddRange(targetDir.GetFiles($"*.{fileExt}", System.IO.SearchOption.AllDirectories));
        return files;
    }

    /// <summary>
    /// Fetch all <see cref="ProcessModule"/>s in the current running process.
    /// </summary>
    /// <param name="excludeWinSys">if true any file path starting with %windir% will be excluded from the results</param>
    public static string GatherLoadedModules(bool excludeWinSys)
    {
        var modules = new StringBuilder();
        // Setup some common library paths if exclude option is desired.
        var winSys = Environment.GetFolderPath(Environment.SpecialFolder.Windows) ?? "N/A";
        var winProg = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) ?? "N/A";
        try
        {
            var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                var fn = module.FileName ?? "Empty";
                if (excludeWinSys && !fn.StartsWith(winSys, StringComparison.OrdinalIgnoreCase) && !fn.StartsWith(winProg, StringComparison.OrdinalIgnoreCase))
                    modules.AppendLine($"{System.IO.Path.GetFileName(fn)} (v{GetFileVersion(fn)})");
                else if (!excludeWinSys)
                    modules.AppendLine($"{System.IO.Path.GetFileName(fn)} (v{GetFileVersion(fn)})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GatherReferencedAssemblies: {ex.Message}", nameof(Extensions));
        }
        return modules.ToString();
    }

    /// <summary>
    /// Fetch all referenced <see cref="System.Reflection.AssemblyName"/> used by the current process.
    /// </summary>
    /// <returns><see cref="List{T}"/></returns>
    public static List<string> ListAllAssemblies()
    {
        List<string> results = new();
        try
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Reflection.AssemblyName main = assembly.GetName();
            results.Add($"Main Assembly: {main.Name}, Version: {main.Version}");
            IOrderedEnumerable<System.Reflection.AssemblyName> names = assembly.GetReferencedAssemblies().OrderBy(o => o.Name);
            foreach (var sas in names)
                results.Add($"Sub Assembly: {sas.Name}, Version: {sas.Version}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ListAllAssemblies: {ex.Message}");
        }
        return results;
    }

    public static Dictionary<string, string?>? GetFieldValues(this object obj)
    {
        Dictionary<string, string?>? results = new();

        try
        {
            results = obj.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .ToDictionary(f => f.Name, f => (string)f.GetValue(null));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetFieldValues: {ex.Message}");
        }

        return results;
    }

    public static string GetFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !fileName.Contains("."))
            return string.Empty;

        return fileName.Split(".").Last();
    }

    /// <summary>
    /// Brute force alpha removal of <see cref="Version"/> text
    /// is not always the best approach, e.g. the following:
    /// "3.0.0-zmain.2211 (DCPP(199ff10ec000000)(cloudtest).160101.0800)"
    /// ...converts to:
    /// "3.0.0.221119910000000.160101.0800"
    /// ...which is not accurate.
    /// </summary>
    /// <param name="fullPath">the entire path to the file</param>
    /// <returns>sanitized <see cref="Version"/></returns>
    public static Version GetFileVersion(string fullPath)
    {
        try
        {
            var ver = FileVersionInfo.GetVersionInfo(fullPath).FileVersion;
            if (string.IsNullOrEmpty(ver)) { return new Version(); }
            if (ver.HasSpace())
            {   // Some assemblies contain versions such as "10.0.22622.1030 (WinBuild.160101.0800)"
                // This will cause the Version constructor to throw an exception, so just take the first piece.
                var chunk = ver.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var firstPiece = Regex.Replace(chunk[0].Replace(',','.'), "[^.0-9]", "");
                return new Version(firstPiece);
            }
            string cleanVersion = Regex.Replace(ver, "[^.0-9]", "");
            return new Version(cleanVersion);
        }
        catch (Exception)
        {
            return new Version(); // 0.0
        }
    }

    /// <summary>
    /// Iterate through all files in the path and return a collection of <see cref="FileInfo"/>.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ext"></param>
    /// <param name="searchOption"></param>
    /// <returns>collection of <see cref="FileInfo"/></returns>
    public static IEnumerable<FileInfo> GetDirectoryFilesInfo(string path, string ext = "*.dll", System.IO.SearchOption searchOption = SearchOption.AllDirectories)
    {
        foreach (var f in Directory.GetFiles(path, ext, searchOption))
        {
            yield return new FileInfo(f);
        }
    }

    /// <summary>
    /// IEnumerable file list using recursion.
    /// </summary>
    /// <param name="basePath">root folder to search</param>
    /// <returns><see cref="IEnumerable{T}"/></returns>
    public static IEnumerable<string> GetAllFilesUnder(string basePath)
    {
        foreach (var file in Directory.GetFiles(basePath))
            yield return file;

        foreach (var x in Directory.GetDirectories(basePath).Select(GetAllFilesUnder).SelectMany(files => files))
            yield return x;
    }

    /// <summary>
    /// Returns the number of lines in a file, excluding the empty lines.
    /// </summary>
    /// <param name="filePath">full path to file</param>
    /// <returns>line count</returns>
    public static int CountFileLines(string filePath)
    {
        return File.ReadLines(filePath).Count(LocalPredicate);

        // We can add more logic here to customize the filtering.
        bool LocalPredicate(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Copy from one stream to another.
    /// Example:
    /// using(var stream = response.GetResponseStream())
    /// using(var ms = new MemoryStream())
    /// {
    ///     stream.CopyTo(ms);
    ///      // Do something with copied data
    /// }
    /// </summary>
    /// <param name="fromStream">From stream.</param>
    /// <param name="toStream">To stream.</param>
    public static void CopyTo(this Stream fromStream, Stream toStream)
    {
        if (fromStream == null)
            throw new ArgumentNullException("fromStream is null");

        if (toStream == null)
            throw new ArgumentNullException("toStream is null");

        byte[] bytes = new byte[8092];
        int dataRead;
        while ((dataRead = fromStream.Read(bytes, 0, bytes.Length)) > 0)
        {
            toStream.Write(bytes, 0, dataRead);
        }
    }

    /// <summary>
    /// Generic file copier with exception handling.
    /// </summary>
    /// <param name="source">the path to copy from</param>
    /// <param name="destination">the path to copy to</param>
    public static void CopyFiles(string source, string destination)
    {
        try
        {
            if (!source.EndsWith("\\")) { source += "\\"; }

            foreach (var p in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(System.IO.Path.Combine(destination, p.Substring(source.Length)));

            foreach (var f in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(f, System.IO.Path.Combine(destination, f.Substring(source.Length)), true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyFiles: {ex.Message}");
        }
    }

    /// <summary>
    /// Asynchronous file copier using <see cref="System.IO.FileStream"/>.
    /// </summary>
    /// <param name="source">the complete file path to copy from</param>
    /// <param name="destination">the complete file path to copy to</param>
    public static async Task<bool> CopyFileStreamAsync(string source, string destination)
    {
        try
        {
            using (FileStream SourceStream = File.Open(source, FileMode.Open))
            {
                using (FileStream DestinationStream = File.Create(destination))
                {
                    await SourceStream.CopyToAsync(DestinationStream);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CopyFileStreamAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Returns the <see cref="System.Security.Cryptography.MD5"/> for the specified file path.
    /// </summary>
    public static string GetFileMD5(this string fileName)
    {
        string checksum = string.Empty;

        if (File.Exists(fileName))
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                try
                {
                    using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        checksum = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"GetFileMD5: {ex.Message}", nameof(Extensions));
                }
            }
            return checksum;
        }

        return checksum;
    }

    /// <summary>
    /// Helper method.
    /// </summary>
    /// <param name="file"><see cref="FileInfo"/></param>
    /// <returns>true if file is in use, false otherwise</returns>
    public static bool IsFileLocked(FileInfo file)
    {
        FileStream? stream = null;

        try
        {
            stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            // The file is unavailable because it is:
            // - still being written to
            // - or being accessed by another thread/application
            // - or does not exist anymore
            return true;
        }
        catch (Exception)
        {
            // Probably a permissions error, so we shouldn't assume the file is locked.
            return false;
        }
        finally
        {
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
        }
        // File is not locked.
        return false;
    }

    /// <summary>
    /// Check if a file can be created in the directory.
    /// </summary>
    /// <param name="directoryPath">the directory path to evaluate</param>
    /// <returns>true if the directory is writeable, false otherwise</returns>
    public static bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            using (FileStream fs = File.Create(System.IO.Path.Combine(directoryPath, "test.txt"), 1, FileOptions.DeleteOnClose)) { /* no-op */ }
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// This method uses the <see cref="System.IO.StreamReader"/>'s
    /// CurrentEncoding property to determine the file's encoding.
    /// </summary>
    /// <param name="path">full path to file</param>
    /// <returns><see cref="System.Text.Encoding"/></returns>
    public static System.Text.Encoding DetermineFileEncoding(this string path)
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
            Debug.WriteLine($"DetermineFileEncoding: {ex.Message}", nameof(Extensions));
            return System.Text.Encoding.Default;
        }
    }

    /// <summary>
    /// In this method, the first step is to check for the presence of a BOM (Byte Order Mark) in the byte
    /// array. A BOM is a sequence of bytes at the beginning of a file that indicates the encoding of the 
    /// data. If a BOM is present, the method returns the appropriate encoding.
    /// If a BOM is not present, the method takes a sample of the data and tries to determine the encoding 
    /// by counting the number of characters with values greater than 127 in the sample.The assumption is 
    /// that encodings such as UTF-8 and UTF-7 will have fewer such characters than encodings like UTF-32 
    /// and Unicode.
    /// This method is not foolproof, and there may be cases where it fails to correctly detect the encoding, 
    /// especially if the data contains a mixture of characters from multiple encodings. Nevertheless, it can 
    /// be a useful starting point for determining the encoding of a byte array.
    /// </summary>
    /// <param name="byteArray">the array to analyze</param>
    /// <returns><see cref="System.Text.Encoding"/></returns>
    public static Encoding IdentifyEncoding(this byte[] byteArray)
    {
        // Nothing to do.
        if (byteArray.Length == 0)
            return Encoding.Default;

        // Try to detect the encoding using the ByteOrderMark.
        if (byteArray.Length >= 4)
        {
            if (byteArray[0] == 0x2b && byteArray[1] == 0x2f && byteArray[2] == 0x76) return Encoding.UTF7;
            if (byteArray[0] == 0xef && byteArray[1] == 0xbb && byteArray[2] == 0xbf) return Encoding.UTF8;
            if (byteArray[0] == 0xff && byteArray[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (byteArray[0] == 0xfe && byteArray[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (byteArray[0] == 0 && byteArray[1] == 0 && byteArray[2] == 0xfe && byteArray[3] == 0xff) return Encoding.UTF32;
        }
        else if (byteArray.Length >= 2)
        {
            if (byteArray[0] == 0xFE && byteArray[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            else if (byteArray[0] == 0xFF && byteArray[1] == 0xFE)
                return Encoding.Unicode;
            else if (byteArray.Length >= 3 && byteArray[0] == 0xEF && byteArray[1] == 0xBB && byteArray[2] == 0xBF)
                return Encoding.UTF8;
        }

        // If the BOM is not present, try to detect the encoding using a sample of the data.
        Encoding[] encodingsToTry = { Encoding.UTF8, Encoding.UTF7, Encoding.UTF32, Encoding.Unicode, Encoding.BigEndianUnicode };
        // Encoding.ASCII should not be used since it does not preserve the 8th bit 
        // and will result in all chars being lower than 127/0x7F. 01111111 = 127
        int sampleSize = Math.Min(byteArray.Length, 1024);
        foreach (Encoding encoding in encodingsToTry)
        {
            string sample = encoding.GetString(byteArray, 0, sampleSize);

            int count = 0;
            foreach (char c in sample)
            {
                if (c > 127) // 0x7F (DEL)
                    count++;
            }

            double ratio = (double)count / sampleSize;
            Debug.WriteLine($"{encoding.EncodingName} => {ratio:N3}", nameof(Extensions));
            if (ratio <= 0.1)
                return encoding;
        }

        // If the encoding could not be determined, return default encoding.
        return Encoding.Default;
    }

    /// <summary>
    /// Read the BOM to determine the file's encoding.
    /// Simplified version of <see cref="Extensions.IdentifyEncoding"/>
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns><see cref="Encoding"/></returns>
    public static Encoding GetEncoding(string fileName)
    {
        try
        {
            // Read the BOM
            var bom = new byte[4];
            using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }
            // Analyze the BOM
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.Default;
        }
        catch (Exception)
        {
            return Encoding.Default;
        }
    }

    public static bool IsValidPath(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) == System.IO.FileAttributes.ReparsePoint)
            {
                Debug.WriteLine("Reparse Point: '" + path + "'");
                return false;
            }
            if (!IsReadable(path))
            {
                Debug.WriteLine("Access Denied: '" + path + "'");
                return false;
            }
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        return true;
    }
    public static bool IsReadable(string path)
    {
        try
        {
            var dn = System.IO.Path.GetDirectoryName(path);
            if (dn != null)
                _ = Directory.GetDirectories(dn, "*.*", SearchOption.TopDirectoryOnly);
            else
                return false;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (PathTooLongException) { return false; }
        catch (IOException) { return false; }
        return true;
    }
    public static bool IsPathTooLong(string path)
    {
        try
        {
            _ = System.IO.Path.GetFullPath(path);
            return false;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (PathTooLongException) { return true; }
    }

    public static bool PathHasInvalidChars(this string path) => (!string.IsNullOrEmpty(path) && path.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0);

    public static string RemoveInvalidPathChars(this string path) => System.IO.Path.GetInvalidFileNameChars().Aggregate(path, (current, c) => current.Replace(c.ToString(), string.Empty));

    /// <summary>
    /// Converts long file size into typical browser file size.
    /// </summary>
    public static string ToFileSize(this ulong size)
    {
        if (size < 1024) { return (size).ToString("F0") + " Bytes"; }
        if (size < Math.Pow(1024, 2)) { return (size / 1024).ToString("F0") + "KB"; }
        if (size < Math.Pow(1024, 3)) { return (size / Math.Pow(1024, 2)).ToString("F0") + "MB"; }
        if (size < Math.Pow(1024, 4)) { return (size / Math.Pow(1024, 3)).ToString("F0") + "GB"; }
        if (size < Math.Pow(1024, 5)) { return (size / Math.Pow(1024, 4)).ToString("F0") + "TB"; }
        if (size < Math.Pow(1024, 6)) { return (size / Math.Pow(1024, 5)).ToString("F0") + "PB"; }
        return (size / Math.Pow(1024, 6)).ToString("F0") + "EB";
    }
    #endregion

    #region [Time and Date]
    /// <summary>
    /// Convert a <see cref="DateTime"/> object into an ISO 8601 formatted string.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns>ISO 8601 formatted string</returns>
    public static string ToJsonFriendlyFormat(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    /// <summary>
    /// 1/1/2023 7:00:00 AM Local to a DateTimeOffset value of 1/1/2023 7:00:00 AM -07:00
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns><see cref="DateTimeOffset"/></returns>
    public static DateTimeOffset ToLocalTimeOffset(this DateTime dateTime)
    {
        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
        DateTimeOffset localTime = dateTime;
        return localTime;
    }

    /// <summary>
    /// 1/1/2023 7:00:00 AM Utc to a DateTimeOffset value of 1/1/2023 7:00:00 AM +00:00
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns><see cref="DateTimeOffset"/></returns>
    public static DateTimeOffset ToDateTimeOffset(this DateTime dateTime)
    {
        dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        DateTimeOffset utcTime = dateTime;
        return utcTime;
    }

    /// <summary>
    /// 1/1/2023 7:00:00 AM Unspecified to a DateTime value of 1/1/2023 7:00:00 AM -05:00
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <param name="timeZone"></param>
    /// <returns><see cref="DateTimeOffset"/></returns>
    public static DateTimeOffset ToDateTimeOffset(this DateTime dateTime, string timeZone = "Eastern Standard Time")
    {
        try
        {
            DateTimeOffset dto = new DateTimeOffset(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZone).GetUtcOffset(dateTime));
            Console.WriteLine("Converted {0} {1} to a DateTime value of {2}", dateTime, dateTime.Kind, dto);
            return dto;
        }
        catch (TimeZoneNotFoundException) // Handle exception if time zone is not defined in registry
        {
            Debug.WriteLine("Unable to identify target time zone for conversion.", $"{nameof(Extensions)}");
            return ToDateTimeOffset(dateTime);
        }
    }

    /// <summary>
    /// Convert time to local UTC DateTimeOffset value
    /// </summary>
    /// <param name="dateTime"><see cref="DateTime"/></param>
    /// <returns><see cref="DateTimeOffset"/></returns>
    public static DateTimeOffset ToLocalDateTimeOffset(this DateTime dateTime)
    {
        return new DateTimeOffset(dateTime, TimeZoneInfo.Local.GetUtcOffset(dateTime));
    }

    /// <summary>
    /// Converts DateTimeOffset values to DateTime values.
    /// Based on its offset, it determines whether the DateTimeOffset 
    /// value is a UTC time, a local time, or some other time and defines 
    /// the returned date and time value's Kind property accordingly.
    /// </summary>
    /// <param name="dateTime"><see cref="DateTimeOffset"/></param>
    /// <returns><see cref="DateTime"/></returns>
    public static DateTime ToDateTime(this DateTimeOffset dateTime)
    {
        if (dateTime.Offset.Equals(TimeSpan.Zero))
            return dateTime.UtcDateTime;
        else if (dateTime.Offset.Equals(TimeZoneInfo.Local.GetUtcOffset(dateTime.DateTime)))
            return DateTime.SpecifyKind(dateTime.DateTime, DateTimeKind.Local);
        else
            return dateTime.DateTime;
    }

    /// <summary>
    /// Checks to see if a date is between two dates.
    /// </summary>
    public static bool Between(this DateTime dt, DateTime rangeBeg, DateTime rangeEnd)
    {
        return dt.Ticks >= rangeBeg.Ticks && dt.Ticks <= rangeEnd.Ticks;
    }

    /// <summary>
    /// Returns a range of <see cref="DateTime"/> objects matching the criteria provided.
    /// </summary>
    /// <example>
    /// IEnumerable{DateTime} dateRange = DateTime.Now.GetDateRangeTo(DateTime.Now.AddDays(80));
    /// </example>
    /// <param name="self"><see cref="DateTime"/></param>
    /// <param name="toDate"><see cref="DateTime"/></param>
    /// <returns><see cref="IEnumerable{DateTime}"/></returns>
    public static IEnumerable<DateTime> GetDateRangeTo(this DateTime self, DateTime toDate)
    {
        var range = Enumerable.Range(0, new TimeSpan(toDate.Ticks - self.Ticks).Days);

        return from p in range select self.Date.AddDays(p);
    }

    /// <summary>
    /// Figure out how old something is.
    /// </summary>
    /// <returns>integer amount in years</returns>
    public static int CalculateYearAge(this DateTime dateTime)
    {
        int age = DateTime.Now.Year - dateTime.Year;
        if (DateTime.Now < dateTime.AddYears(age))
        {
            age--;
        }

        return age;
    }

    /// <summary>
    /// Figure out how old something is.
    /// </summary>
    /// <returns>integer amount in months</returns>
    public static int CalculateMonthAge(this DateTime dateTime)
    {
        int age = DateTime.Now.Year - dateTime.Year;
        if (DateTime.Now < dateTime.AddYears(age))
        {
            age--;
        }

        return age * 12;
    }

    /// <summary>
    /// Converts <see cref="TimeSpan"/> objects to a simple human-readable string.
    /// e.g. 420 milliseconds, 3.1 seconds, 2 minutes, 4.231 hours, etc.
    /// </summary>
    /// <param name="span"><see cref="TimeSpan"/></param>
    /// <param name="significantDigits">number of right side digits in output (precision)</param>
    /// <returns>human-friendly string</returns>
    public static string ToTimeString(this TimeSpan span, int significantDigits = 3)
    {
        var format = $"G{significantDigits}";
        return span.TotalMilliseconds < 1000 ? span.TotalMilliseconds.ToString(format) + " milliseconds"
                : (span.TotalSeconds < 60 ? span.TotalSeconds.ToString(format) + " seconds"
                : (span.TotalMinutes < 60 ? span.TotalMinutes.ToString(format) + " minutes"
                : (span.TotalHours < 24 ? span.TotalHours.ToString(format) + " hours"
                : span.TotalDays.ToString(format) + " days")));
    }

    /// <summary>
    /// Converts <see cref="TimeSpan"/> objects to a simple human-readable string.
    /// e.g. 420 milliseconds, 3.1 seconds, 2 minutes, 4.231 hours, etc.
    /// </summary>
    /// <param name="span"><see cref="TimeSpan"/></param>
    /// <param name="significantDigits">number of right side digits in output (precision)</param>
    /// <returns>human-friendly string</returns>
    public static string ToTimeString(this TimeSpan? span, int significantDigits = 3)
    {
        var format = $"G{significantDigits}";
        return span?.TotalMilliseconds < 1000 ? span?.TotalMilliseconds.ToString(format) + " milliseconds"
                : (span?.TotalSeconds < 60 ? span?.TotalSeconds.ToString(format) + " seconds"
                : (span?.TotalMinutes < 60 ? span?.TotalMinutes.ToString(format) + " minutes"
                : (span?.TotalHours < 24 ? span?.TotalHours.ToString(format) + " hours"
                : span?.TotalDays.ToString(format) + " days")));
    }

    /// <summary>
    /// Display a readable sentence as to when the time will happen.
    /// e.g. "in one second" or "in 2 days"
    /// </summary>
    /// <param name="value"><see cref="TimeSpan"/>the future time to compare from now</param>
    /// <returns>human-friendly string</returns>
    public static string ToReadableTime(this TimeSpan value)
    {
        double delta = value.TotalSeconds;
        if (delta < 60) { return value.Seconds == 1 ? "one second" : value.Seconds + " seconds"; }
        if (delta < 120) { return "a minute"; }
        if (delta < 3000) { return value.Minutes + " minutes"; } // 50 * 60
        if (delta < 5400) { return "an hour"; } // 90 * 60
        if (delta < 86400) { return value.Hours + " hours"; } // 24 * 60 * 60
        if (delta < 172800) { return "one day"; } // 48 * 60 * 60
        if (delta < 2592000) { return value.Days + " days"; } // 30 * 24 * 60 * 60
        if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
        {
            int months = Convert.ToInt32(Math.Floor((double)value.Days / 30));
            return months <= 1 ? "one month" : months + " months";
        }
        int years = Convert.ToInt32(Math.Floor((double)value.Days / 365));
        return years <= 1 ? "one year" : years + " years";
    }

    /// <summary>
    /// Display a readable sentence as to when that time happened.
    /// e.g. "5 minutes ago" or "in 2 days"
    /// </summary>
    /// <param name="value"><see cref="DateTime"/>the past/future time to compare from now</param>
    /// <returns>human friendly format</returns>
    public static string ToReadableTime(this DateTime value, bool useUTC = false)
    {
        TimeSpan ts;
        if (useUTC) { ts = new TimeSpan(DateTime.UtcNow.Ticks - value.Ticks); }
        else { ts = new TimeSpan(DateTime.Now.Ticks - value.Ticks); }

        double delta = ts.TotalSeconds;
        if (delta < 0) // in the future
        {
            delta = Math.Abs(delta);
            if (delta < 60) { return Math.Abs(ts.Seconds) == 1 ? "in one second" : "in " + Math.Abs(ts.Seconds) + " seconds"; }
            if (delta < 120) { return "in a minute"; }
            if (delta < 3000) { return "in " + Math.Abs(ts.Minutes) + " minutes"; } // 50 * 60
            if (delta < 5400) { return "in an hour"; } // 90 * 60
            if (delta < 86400) { return "in " + Math.Abs(ts.Hours) + " hours"; } // 24 * 60 * 60
            if (delta < 172800) { return "tomorrow"; } // 48 * 60 * 60
            if (delta < 2592000) { return "in " + Math.Abs(ts.Days) + " days"; } // 30 * 24 * 60 * 60
            if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)Math.Abs(ts.Days) / 30));
                return months <= 1 ? "in one month" : "in " + months + " months";
            }
            int years = Convert.ToInt32(Math.Floor((double)Math.Abs(ts.Days) / 365));
            return years <= 1 ? "in one year" : "in " + years + " years";
        }
        else // in the past
        {
            if (delta < 60) { return ts.Seconds == 1 ? "one second ago" : ts.Seconds + " seconds ago"; }
            if (delta < 120) { return "a minute ago"; }
            if (delta < 3000) { return ts.Minutes + " minutes ago"; } // 50 * 60
            if (delta < 5400) { return "an hour ago"; } // 90 * 60
            if (delta < 86400) { return ts.Hours + " hours ago"; } // 24 * 60 * 60
            if (delta < 172800) { return "yesterday"; } // 48 * 60 * 60
            if (delta < 2592000) { return ts.Days + " days ago"; } // 30 * 24 * 60 * 60
            if (delta < 31104000) // 12 * 30 * 24 * 60 * 60
            {
                int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                return months <= 1 ? "one month ago" : months + " months ago";
            }
            int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
            return years <= 1 ? "one year ago" : years + " years ago";
        }
    }

    /// <summary>
    /// Determines if the date is a working day, weekend, or determine the next workday coming up.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static bool WorkingDay(this DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// Determines if the date is on a weekend (i.e. Saturday or Sunday)
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    public static bool IsWeekend(this DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// Gets the next date that is not a weekend.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    /// <returns><see cref="DateTime"/></returns>
    public static DateTime NextWorkday(this DateTime date)
    {
        DateTime nextDay = date.AddDays(1);
        while (!nextDay.WorkingDay())
        {
            nextDay = nextDay.AddDays(1);
        }
        return nextDay;
    }

    /// <summary>
    /// Determine the Next date by passing in a DayOfWeek (i.e. from this date, when is the next Tuesday?)
    /// </summary>
    /// <returns><see cref="DateTime"/></returns>
    public static DateTime Next(this DateTime current, DayOfWeek dayOfWeek)
    {
        int offsetDays = dayOfWeek - current.DayOfWeek;
        if (offsetDays <= 0)
        {
            offsetDays += 7;
        }
        DateTime result = current.AddDays(offsetDays);
        return result;
    }

    /// <summary>
    /// Converts a DateTime to a DateTimeOffset with the specified offset
    /// </summary>
    /// <param name="date">The DateTime to convert</param>
    /// <param name="offset">The offset to apply to the datetime field</param>
    /// <returns>The corresponding <see cref="DateTimeOffset"/></returns>
    public static DateTimeOffset ToOffset(this DateTime date, TimeSpan offset)
    {
        return new DateTimeOffset(date).ToOffset(offset);
    }

    /// <summary>
    /// Accounts for once date1 is past date2.
    /// </summary>
    public static bool WithinOneDayOrPast(this DateTime date1, DateTime date2)
    {
        DateTime first = DateTime.Parse($"{date1}");
        if (first < date2) // Account for past-due amounts.
        {
            return true;
        }
        else
        {
            TimeSpan difference = first - date2;
            return Math.Abs(difference.TotalDays) <= 1.0;
        }
    }

    /// <summary>
    /// Only accounts for date1 being within range of date2.
    /// </summary>
    public static bool WithinOneDay(this DateTime date1, DateTime date2)
    {
        TimeSpan difference = DateTime.Parse($"{date1}") - date2;
        return Math.Abs(difference.TotalDays) <= 1.0;
    }

    /// <summary>
    /// Only accounts for date1 being within range of date2 by some amount.
    /// </summary>
    public static bool WithinAmountOfDays(this DateTime date1, DateTime date2, double days)
    {
        TimeSpan difference = DateTime.Parse($"{date1}") - date2;
        return Math.Abs(difference.TotalDays) <= days;
    }

    /// <summary>
    /// Move the <see cref="DateTime"/> to the last day in the month.
    /// </summary>
    /// <param name="date"><see cref="DateTime"/></param>
    /// <returns><see cref="DateTime"/></returns>
    public static DateTime ConvertToLastDayOfMonth(this DateTime date) => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    /// <summary>
    /// Multiplies the given <see cref="TimeSpan"/> by the scalar amount provided.
    /// </summary>
    /// <returns><see cref="TimeSpan"/></returns>
    public static TimeSpan Multiply(this TimeSpan timeSpan, double scalar) => new TimeSpan((long)(timeSpan.Ticks * scalar));
    #endregion

    #region [Events]
    /// <summary>
    /// Use to invoke an async <see cref="EventHandler{TEventArgs}"/> using <see cref="DeferredEventArgs"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="EventHandler{TEventArgs}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
    public static Task InvokeAsync<T>(this EventHandler<T> eventHandler, 
        object sender, 
        T eventArgs)
        where T : DeferredEventArgs
    {
        return InvokeAsync(eventHandler, sender, eventArgs, CancellationToken.None);
    }

    /// <summary>
    /// Use to invoke an async <see cref="EventHandler{TEventArgs}"/> using <see cref="DeferredEventArgs"/> with a <see cref="CancellationToken"/>.
    /// </summary>
    /// <typeparam name="T"><see cref="EventArgs"/> type.</typeparam>
    /// <param name="eventHandler"><see cref="EventHandler{TEventArgs}"/> to be invoked.</param>
    /// <param name="sender">Sender of the event.</param>
    /// <param name="eventArgs"><see cref="EventArgs"/> instance.</param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/> option.</param>
    /// <returns><see cref="Task"/> to wait on deferred event handler.</returns>
    public static Task InvokeAsync<T>(this EventHandler<T> eventHandler, 
        object sender, 
        T eventArgs, 
        CancellationToken cancellationToken)
        where T : DeferredEventArgs
    {
        if (eventHandler == null)
        {
            return Task.CompletedTask;
        }

        var tasks = eventHandler.GetInvocationList()
            .OfType<EventHandler<T>>()
            .Select(invocationDelegate =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                invocationDelegate(sender, eventArgs);
#pragma warning disable CS0618 // Type or member is obsolete
                var deferral = eventArgs.GetCurrentDeferralAndReset();
                return deferral?.WaitForCompletion(cancellationToken) ?? Task.CompletedTask;
#pragma warning restore CS0618 // Type or member is obsolete
            })
            .ToArray();

        return Task.WhenAll(tasks);
    }

    public static void Raise(this EventHandler handler, object sender)
    {
        handler?.Invoke(sender, EventArgs.Empty);
    }

    public static void Raise<T>(this EventHandler<EventArgs<T>> handler, object sender, T value)
    {
        handler?.Invoke(sender, new EventArgs<T>(value));
    }

    public static void Raise<T>(this EventHandler<T> handler, object sender, T value) where T : EventArgs
    {
        handler?.Invoke(sender, value);
    }

    public static void Raise<T>(this EventHandler<EventArgs<T>> handler, object sender, EventArgs<T> value)
    {
        handler?.Invoke(sender, value);
    }
    #endregion

    #region [CompilerServices]
    /// <summary>
    /// Used for validating an argument
    /// </summary>
    /// <example>
    /// Action action = some action;
    /// Extensions.ValidateArgument(nameof(action), action is not null);
    /// action();
    /// The expression used for condition is injected by the compiler into the message argument.
    /// When the user calls with a null argument, the following message is thrown:
    /// "Argument failed validation: action is not null"
    /// </example>
    public static void ValidateArgument(string parameterName, bool condition, [CallerArgumentExpression("condition")] string? message = null)
    {
        if (!condition)
        {
            throw new ArgumentException($"Argument failed validation: <{message}>", parameterName);
        }
    }

    /// <summary>
    /// Sample of <see cref="System.Runtime.CompilerServices.CallerArgumentExpressionAttribute"/>.
    /// </summary>
    /// <example>
    /// sample = Enumerable.Range(0, 10).HasEnough(20);
    /// The example code above would throw an exception whose message is the following text:
    /// Expression doesn't have enough elements: Enumerable.Range(0, 10) (Parameter 'sequence')
    /// </example>
    /// <returns>
    /// <see cref="IEnumerable{T}"/> if the frequency argument is satisfied.
    /// <see cref="ArgumentException"/> if the frequency argument is not satisfied.
    /// </returns>
    /// 
    public static IEnumerable<T> HasEnough<T>(this IEnumerable<T> sequence, int frequency, [CallerArgumentExpression(nameof(sequence))] string? message = null)
    {
        if (sequence.Count() < frequency)
            throw new ArgumentException($"Expression doesn't have enough elements: {message}", nameof(sequence));
        int i = 0;
        foreach (T item in sequence)
        {
            if (i++ % frequency == 0)
                yield return item;
        }
    }
    #endregion

    #region [ItemIndexRange]
    public static bool Equals(this ItemIndexRange This, ItemIndexRange range)
    {
        return (This.FirstIndex == range.FirstIndex && This.Length == range.Length);
    }

    public static bool ContiguousOrOverlaps(this ItemIndexRange This, ItemIndexRange range)
    {
        // This is left
        if (This.FirstIndex < range.FirstIndex)
        {
            return (range.FirstIndex <= This.LastIndex + 1);
        }
        // This is right
        else if (This.FirstIndex > range.FirstIndex)
        {
            return (This.FirstIndex <= range.LastIndex + 1);
        }
        // Aligned
        return true;
    }

    public static bool Intersects(this ItemIndexRange This, ItemIndexRange range)
    {
        return (range.FirstIndex >= This.FirstIndex && range.FirstIndex <= This.LastIndex) || (range.LastIndex >= This.FirstIndex && range.LastIndex <= This.LastIndex);
    }

    public static bool Intersects(this ItemIndexRange This, int FirstIndex, uint Length)
    {
        int LastIndex = FirstIndex + (int)Length - 1;
        return (FirstIndex >= This.FirstIndex && FirstIndex <= This.LastIndex) || (LastIndex >= This.FirstIndex && LastIndex <= This.LastIndex);
    }

    public static ItemIndexRange Combine(this ItemIndexRange This, ItemIndexRange range)
    {
        int start = Math.Min(This.FirstIndex, range.FirstIndex);
        int end = Math.Max(This.LastIndex, range.LastIndex);

        return new ItemIndexRange(start, 1 + (uint)Math.Abs(end - start));
    }

    public static bool DiffRanges(this ItemIndexRange RangeA, ItemIndexRange RangeB, out ItemIndexRange InBothAandB, out ItemIndexRange[] OnlyInRangeA, out ItemIndexRange[] OnlyInRangeB)
    {
        List<ItemIndexRange> exA = new List<ItemIndexRange>();
        List<ItemIndexRange> exB = new List<ItemIndexRange>();
        int i, j;
        i = Math.Max(RangeA.FirstIndex, RangeB.FirstIndex);
        j = Math.Min(RangeA.LastIndex, RangeB.LastIndex);

        if (i <= j)
        {
            // Ranges intersect
            InBothAandB = new ItemIndexRange(i, (uint)(1 + j - i));
            if (RangeA.FirstIndex < i) exA.Add(new ItemIndexRange(RangeA.FirstIndex, (uint)(i - RangeA.FirstIndex)));
            if (RangeA.LastIndex > j) exA.Add(new ItemIndexRange(j + 1, (uint)(RangeA.LastIndex - j)));
            if (RangeB.FirstIndex < i) exB.Add(new ItemIndexRange(RangeB.FirstIndex, (uint)(i - RangeB.FirstIndex)));
            if (RangeB.LastIndex > j) exB.Add(new ItemIndexRange(j + 1, (uint)(RangeB.LastIndex - j)));
            OnlyInRangeA = exA.ToArray();
            OnlyInRangeB = exB.ToArray();
            return true;
        }
        else
        {
            InBothAandB = default(ItemIndexRange);
            OnlyInRangeA = new ItemIndexRange[] { RangeA };
            OnlyInRangeB = new ItemIndexRange[] { RangeB };
            return false;
        }
    }

    public static ItemIndexRange? Overlap(this ItemIndexRange RangeA, ItemIndexRange RangeB)
    {
        int i, j;
        i = Math.Max(RangeA.FirstIndex, RangeB.FirstIndex);
        j = Math.Min(RangeA.LastIndex, RangeB.LastIndex);

        if (i <= j)
        {   // Ranges intersect
            return new ItemIndexRange(i, (uint)(1 + j - i));
        }
        else { return null; }
    }
    #endregion

    #region [Web]
    public static async Task<MemoryStream> GetStreamFromWeb(this string url)
    {
        using (var ms = new MemoryStream())
        {
            var webReq = (HttpWebRequest)WebRequest.Create(url);
            webReq.Method = "GET";
            using (var response = (HttpWebResponse)await webReq.GetResponseAsync())
            {
                response.GetResponseStream()?.CopyTo(ms);
                return ms;
            }
        }
    }

    public static async Task<T?> ReadAsJsonAsync<T>(this HttpContent content)
    {
        var json = await content.ReadAsStringAsync();

        if (!string.IsNullOrEmpty(json))
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        else
            return default(T);
    }

    public static async Task<string> DownloadAllAsync(IEnumerable<string> locations)
    {
        using (var client = new HttpClient())
        {
            var downloads = locations.Select(client.GetStringAsync);
            var downloadTasks = downloads.ToArray();
            var pages = await Task.WhenAll(downloadTasks);
            return string.Concat(pages);
        }
    }

    public static bool IsPortAvailableForListening(int portNumber)
    {
        System.Net.IPEndPoint[] activeTcpListeners = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        return activeTcpListeners.Select(x => x.Port == portNumber).FirstOrDefault();
    }
    #endregion

    #region [WriteableBitmap]
    public static async Task SaveAsync(this WriteableBitmap writeableBitmap, StorageFile outputFile)
    {
        var encoderId = GetEncoderId(outputFile.Name);

        try
        {
            Stream stream = writeableBitmap.PixelBuffer.AsStream();
            byte[] pixels = new byte[(uint)stream.Length];
            await stream.ReadAsync(pixels, 0, pixels.Length);

            using (var writeStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(encoderId, writeStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)writeableBitmap.PixelWidth,
                    (uint)writeableBitmap.PixelHeight,
                    96,
                    96,
                    pixels);

                await encoder.FlushAsync();

                using (var outputStream = writeStream.GetOutputStreamAt(0))
                {
                    await outputStream.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            // Your exception handling here..
            throw;
        }
    }

    public static async Task<WriteableBitmap> LoadAsync(this WriteableBitmap writeableBitmap, StorageFile storageFile)
    {
        var wb = writeableBitmap;

        using (var stream = await storageFile.OpenReadAsync())
        {
            await wb.SetSourceAsync(stream);
        }

        return wb;
    }

    static Guid GetEncoderId(string fileName)
    {
        Guid encoderId;

        var ext = Path.GetExtension(fileName);

        if (new[] { ".bmp", ".dib" }.Contains(ext))
            encoderId = BitmapEncoder.BmpEncoderId;
        else if (new[] { ".tiff", ".tif" }.Contains(ext))
            encoderId = BitmapEncoder.TiffEncoderId;
        else if (new[] { ".gif" }.Contains(ext))
            encoderId = BitmapEncoder.GifEncoderId;
        else if (new[] { ".jpg", ".jpeg", ".jpe", ".jfif", ".jif" }.Contains(ext))
            encoderId = BitmapEncoder.JpegEncoderId;
        else if (new[] { ".hdp", ".jxr", ".wdp" }.Contains(ext))
            encoderId = BitmapEncoder.JpegXREncoderId;
        else //if (new [] {".png"}.Contains(ext))
            encoderId = BitmapEncoder.PngEncoderId;

        return encoderId;
    }
    #endregion

    #region [CropBitmap]
    /// <summary>
    /// Get a cropped bitmap from a image file.
    /// </summary>
    /// <param name="originalImageFile">
    /// The original image file.
    /// </param>
    /// <param name="startPoint">
    /// The start point of the region to be cropped.
    /// </param>
    /// <param name="corpSize">
    /// The size of the region to be cropped.
    /// </param>
    /// <returns>
    /// The cropped image.
    /// </returns>
    public async static Task<WriteableBitmap> GetCroppedBitmapAsync(StorageFile originalImageFile,
        Windows.Foundation.Point startPoint,
        Windows.Foundation.Size corpSize,
        double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale))
            scale = 1;

        // Convert start point and size to integer.
        uint startPointX = (uint)Math.Floor(startPoint.X * scale);
        uint startPointY = (uint)Math.Floor(startPoint.Y * scale);
        uint height = (uint)Math.Floor(corpSize.Height * scale);
        uint width = (uint)Math.Floor(corpSize.Width * scale);

        using (IRandomAccessStream stream = await originalImageFile.OpenReadAsync())
        {

            // Create a decoder from the stream. With the decoder, we can get 
            // the properties of the image.
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            // The scaledSize of original image.
            uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
            uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);


            // Refine the start point and the size. 
            if (startPointX + width > scaledWidth)
                startPointX = scaledWidth - width;

            if (startPointY + height > scaledHeight)
                startPointY = scaledHeight - height;

            // Get the cropped pixels.
            byte[] pixels = await GetPixelData(decoder,
                startPointX, startPointY,
                width, height,
                scaledWidth, scaledHeight);

            // Stream the bytes into a WriteableBitmap
            WriteableBitmap cropBmp = new WriteableBitmap((int)width, (int)height);
            Stream pixStream = cropBmp.PixelBuffer.AsStream();
            pixStream.Write(pixels, 0, (int)(width * height * 4));

            return cropBmp;
        }
    }

    /// <summary>
    /// Use BitmapTransform to define the region to crop, and then get the pixel data in the region
    /// </summary>
    public async static Task<byte[]> GetPixelData(BitmapDecoder decoder,
        uint startPointX, uint startPointY,
        uint width, uint height)
    {
        return await GetPixelData(decoder,
            startPointX, startPointY,
            width, height,
            decoder.PixelWidth, decoder.PixelHeight);
    }

    /// <summary>
    /// Use BitmapTransform to define the region to crop, and then get the pixel data in the region.
    /// If you want to get the pixel data of a scaled image, set the scaledWidth and scaledHeight
    /// of the scaled image.
    /// </summary>
    public async static Task<byte[]> GetPixelData(BitmapDecoder decoder,
        uint startPointX, uint startPointY,
        uint width, uint height,
        uint scaledWidth, uint scaledHeight)
    {
        BitmapTransform transform = new BitmapTransform();
        BitmapBounds bounds = new BitmapBounds();
        bounds.X = startPointX;
        bounds.Y = startPointY;
        bounds.Height = height;
        bounds.Width = width;
        transform.Bounds = bounds;
        transform.ScaledWidth = scaledWidth;
        transform.ScaledHeight = scaledHeight;

        try
        {
            // Get the cropped pixels within the bounds of transform.
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            byte[] pixels = pix.DetachPixelData();

            return pixels;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetPixelDataScaled: {ex.Message}");
            return new byte[] { 0 };
        }
    }
    #endregion
}

#region [Supporting Classes]
/// <summary>
/// Supporting class for the <see cref="Extensions.Raise"/> helpers.
/// </summary>
/// <typeparam name="T"></typeparam>
public class EventArgs<T> : EventArgs
{
    public T Value { get; private set; }
    public EventArgs(T value) { Value = value; }
}

/// <summary>
/// <see cref="EventArgs"/> which can retrieve a <see cref="EventDeferral"/> in order to process data asynchronously before an <see cref="EventHandler"/> completes and returns to the calling control.
/// </summary>
public class DeferredEventArgs : EventArgs
{
    /// <summary>
    /// Gets a new <see cref="DeferredEventArgs"/> to use in cases where no <see cref="EventArgs"/> wish to be provided.
    /// </summary>
    public static new DeferredEventArgs Empty => new DeferredEventArgs();

    private readonly object _eventDeferralLock = new object();

    private EventDeferral? _eventDeferral;

    /// <summary>
    /// Returns an <see cref="EventDeferral"/> which can be completed when deferred event is ready to continue.
    /// </summary>
    /// <returns><see cref="EventDeferral"/> instance.</returns>
    public EventDeferral GetDeferral()
    {
        lock (_eventDeferralLock)
        {
            return _eventDeferral ??= new EventDeferral();
        }
    }

    /// <summary>
    /// This is a support method used by EventHandlerExtensions. It is public only for
    /// additional usage within extensions for the UWP based TypedEventHandler extensions.
    /// </summary>
    /// <returns>Internal EventDeferral reference</returns>
#if !NETSTANDARD1_4
    [Browsable(false)]
#endif
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This is an internal only method to be used by EventHandler extension classes, public callers should call GetDeferral() instead.")]
    public EventDeferral? GetCurrentDeferralAndReset()
    {
        lock (_eventDeferralLock)
        {
            var eventDeferral = _eventDeferral;

            _eventDeferral = null;

            return eventDeferral;
        }
    }
}

/// <summary>
/// Deferral handle provided by a <see cref="DeferredEventArgs"/>.
/// </summary>
public class EventDeferral : IDisposable
{
    // TODO: If/when .NET 5 is base, we can upgrade to non-generic version
    private readonly TaskCompletionSource<object?> _taskCompletionSource = new TaskCompletionSource<object?>();

    internal EventDeferral()
    {
    }

    /// <summary>
    /// Call when finished with the Deferral.
    /// </summary>
    public void Complete() => _taskCompletionSource.TrySetResult(null);

    /// <summary>
    /// Waits for the <see cref="EventDeferral"/> to be completed by the event handler.
    /// </summary>
    /// <param name="cancellationToken"><see cref="CancellationToken"/>.</param>
    /// <returns><see cref="Task"/>.</returns>
#if !NETSTANDARD1_4
    [Browsable(false)]
#endif
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This is an internal only method to be used by EventHandler extension classes, public callers should call GetDeferral() instead on the DeferredEventArgs.")]
    public async Task WaitForCompletion(CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() => _taskCompletionSource.TrySetCanceled()))
        {
            await _taskCompletionSource.Task;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Complete();
    }
}

/// <summary>
/// A <see langword="ref"/> <see langword="struct"/> that tokenizes a given <see cref="ReadOnlySpan{T}"/> instance.
/// </summary>
/// <typeparam name="T">The type of items to enumerate.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct ReadOnlySpanTokenizer<T> where T : IEquatable<T>
{
    /// <summary>
    /// The source <see cref="ReadOnlySpan{T}"/> instance.
    /// </summary>
    private readonly ReadOnlySpan<T> span;

    /// <summary>
    /// The separator item to use.
    /// </summary>
    private readonly T separator;

    /// <summary>
    /// The current initial offset.
    /// </summary>
    private int start;

    /// <summary>
    /// The current final offset.
    /// </summary>
    private int end;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlySpanTokenizer{T}"/> struct.
    /// </summary>
    /// <param name="span">The source <see cref="ReadOnlySpan{T}"/> instance.</param>
    /// <param name="separator">The separator item to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpanTokenizer(ReadOnlySpan<T> span, T separator)
    {
        this.span = span;
        this.separator = separator;
        this.start = 0;
        this.end = -1;
    }

    /// <summary>
    /// Implements the duck-typed <see cref="IEnumerable{T}.GetEnumerator"/> method.
    /// </summary>
    /// <returns>An <see cref="ReadOnlySpanTokenizer{T}"/> instance targeting the current <see cref="ReadOnlySpan{T}"/> value.</returns>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpanTokenizer<T> GetEnumerator() => this;

    /// <summary>
    /// Implements the duck-typed <see cref="System.Collections.IEnumerator.MoveNext"/> method.
    /// </summary>
    /// <returns><see langword="true"/> whether a new element is available, <see langword="false"/> otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int
            newEnd = this.end + 1,
            length = this.span.Length;

        // Additional check if the separator is not the last character
        if (newEnd <= length)
        {
            this.start = newEnd;

            // We need to call this extension explicitly or the extension method resolution rules for the C# compiler
            // will end up picking Microsoft.Toolkit.HighPerformance.ReadOnlySpanExtensions.IndexOf instead, even
            // though the latter takes the parameter via a readonly reference. This is because the "in" modifier is
            // implicit, which makes the signature compatible, and because extension methods are matched in such a
            // way that methods "closest" to where they're used are preferred. Since this type shares the same root
            // namespace, this makes that extension a better match, so that it overrides the MemoryExtensions one.
            // This is not a problem for consumers of this package, as their code would be outside of the
            // Microsoft.Toolkit.HighPerformance namespace, so both extensions would be "equally distant", so that
            // when they're both in scope it will be possible to choose which one to use by adding an explicit "in".
            int index = System.MemoryExtensions.IndexOf(this.span.Slice(newEnd), this.separator);

            // Extract the current subsequence
            if (index >= 0)
            {
                this.end = newEnd + index;

                return true;
            }

            this.end = length;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the duck-typed <see cref="IEnumerator{T}.Current"/> property.
    /// </summary>
    public readonly ReadOnlySpan<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.span.Slice(this.start, this.end - this.start);
    }
}

/// <summary>
/// Provides very basic JSON formatting support to the string extensions.
/// Currently this is only used for <see cref="Extensions.ToJsonObject(Windows.Web.Http.HttpProgress)"/>
/// helper method but could easily be expanded upon for other helper methods.
/// </summary>
public class JsonBuilder
{
    private Windows.Data.Json.JsonObject container = new();
    private Windows.Data.Json.JsonObject jo = new();

    public JsonBuilder(string className)
    {
        container.Add(className, jo);
    }

    public void AddNumber(string key, double? value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateNumberValue(value.Value));
    }

    public void AddBoolean(string key, bool? value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateBooleanValue(value.Value));
    }

    public void AddString(string key, object value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateStringValue(value.ToString()));
    }

    public void AddTimeSpan(string key, TimeSpan? value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateStringValue(value.ToString()));
    }

    public void AddDateTime(string key, DateTime? value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateStringValue(value.Value.ToString("u")));
    }

    public void AddDateTime(string key, DateTimeOffset? value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : Windows.Data.Json.JsonValue.CreateStringValue(value.Value.ToString("u")));
    }

    public void AddJsonValue(string key, Windows.Data.Json.IJsonValue value)
    {
        jo.Add(key, value == null ? Windows.Data.Json.JsonValue.CreateNullValue() : value);
    }

    public Windows.Data.Json.JsonObject GetJsonObject()
    {
        return container;
    }

    public override string ToString()
    {
        return container.ToString();
    }
}
#endregion
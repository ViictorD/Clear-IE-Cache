using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClearCache
{
    class Program
    {
        // Thanks to agoesz who did 90% of this work.
        // ref: http://theagussantoso.blogspot.fr/2008/11/get-temporary-internet-explorer-files.html

        //Declare the WIN32 API calls to get the entries from IE's history cache  
        [DllImport("wininet.dll", SetLastError = true)]
        public static extern Boolean DeleteUrlCacheEntry(String lpszUrlSearchPattern);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern IntPtr FindFirstUrlCacheEntry(string lpszUrlSearchPattern, IntPtr lpFirstCacheEntryInfo, out UInt32 lpdwFirstCacheEntryInfoBufferSize);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern long FindNextUrlCacheEntry(IntPtr hEnumHandle, IntPtr lpNextCacheEntryInfo, out UInt32 lpdwNextCacheEntryInfoBufferSize);

        [DllImport("wininet.dll", SetLastError = true)]
        public static extern long FindCloseUrlCache(IntPtr hEnumHandle);

        [StructLayout(LayoutKind.Sequential)]
        public struct INTERNET_CACHE_ENTRY_INFO
        {
            public UInt32 dwStructSize;
            public string lpszSourceUrlName;
            public string lpszLocalFileName;
            public UInt32 CacheEntryType;
            public UInt32 dwUseCount;
            public UInt32 dwHitRate;
            public UInt32 dwSizeLow;
            public UInt32 dwSizeHigh;
            public FILETIME LastModifiedTime;
            public FILETIME ExpireTime;
            public FILETIME LastAccessTime;
            public FILETIME LastSyncTime;
            public IntPtr lpHeaderInfo;
            public UInt32 dwHeaderInfoSize;
            public string lpszFileExtension;
            public UInt32 dwExemptDelta;
        };

        public static class Hresults
        {
            public const int ERROR_SUCCESS = 0;
            public const int ERROR_INSUFFICIENT_BUFFER = 122;
            public const int ERROR_NO_MORE_ITEMS = 259;
        };

        static void Main(string[] args)
        {
            // Default url if you need it
            string urlFilter = "google.com";

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Compare(args[i].ToLower(), "-help") == 0)
                {
                    print_help();
                    return ;
                }
                else if (String.Compare(args[i].ToLower(), "-url") == 0)
                {
                    if (i + 1 < args.Length)
                        urlFilter = args[i++ + 1].ToLower();
                    else
                    {
                        Console.WriteLine("No domain name specified");
                        return ;
                    }
                }
                else
                {
                    Console.WriteLine("Argument: '" + args[i] + "' is invalid.\n");
                    Console.WriteLine("Type: '-help' to see usage.\n");
                    return ;
                }
            }

            List<string> myFiles = getUrlEntriesInHistory(urlFilter);
            int count = 0;
            foreach (string pathName in myFiles)
            {
                try
                {
                    DeleteUrlCacheEntry(pathName);
                    ++count;
                }
                catch (Exception) { }
            }
            Console.WriteLine("Number of cached files deleted with '" + urlFilter + "' URL: " + count.ToString() + "\n");
            Thread.Sleep(3000);
        }

        private static void print_help()
        {
            Console.WriteLine("ClearCache.exe [-url domain]\n");
            Console.WriteLine("\t-url\t Specify the domain name to clear.");
        }

        private static List<string> getUrlEntriesInHistory(string sourceUrlFilter)
        {
            List<string> filesList = new List<string>();
            IntPtr buffer = IntPtr.Zero;
            UInt32 structSize;

            //This call will fail but returns the size required in structSize  
            //to allocate necessary buffer  
            IntPtr hEnum = FindFirstUrlCacheEntry(null, buffer, out structSize);
            try
            {
                if (hEnum == IntPtr.Zero)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    if (lastError == Hresults.ERROR_INSUFFICIENT_BUFFER)
                    {
                        //Allocate buffer  
                        buffer = Marshal.AllocHGlobal((int)structSize);
                        //Call again, this time it should succeed  
                        hEnum = FindFirstUrlCacheEntry(null, buffer, out structSize);
                    }
                    else if (lastError == Hresults.ERROR_NO_MORE_ITEMS)
                    {
                        Console.Error.WriteLine("No entries in IE's history cache");
                        return filesList;
                    }
                    else if (lastError != Hresults.ERROR_SUCCESS)
                    {
                        Console.Error.WriteLine("Unable to fetch entries from IE's history cache");
                        return filesList;
                    }
                }

                INTERNET_CACHE_ENTRY_INFO result = (INTERNET_CACHE_ENTRY_INFO)Marshal.PtrToStructure(buffer, typeof(INTERNET_CACHE_ENTRY_INFO));
                string fileUrl = result.lpszSourceUrlName.Substring(result.lpszSourceUrlName.LastIndexOf('@') + 1);
                if (fileUrl.Contains(sourceUrlFilter))
                    filesList.Add(result.lpszSourceUrlName);

                // Free the buffer  
                if (buffer != IntPtr.Zero)
                {
                    try { Marshal.FreeHGlobal(buffer); }
                    catch { }
                    buffer = IntPtr.Zero;
                    structSize = 0;
                }

                //Loop through all entries, attempt to find matches  
                while (true)
                {
                    long nextResult = FindNextUrlCacheEntry(hEnum, buffer, out structSize);
                    if (nextResult != 1) //TRUE  
                    {
                        int lastError = Marshal.GetLastWin32Error();
                        if (lastError == Hresults.ERROR_INSUFFICIENT_BUFFER)
                        {
                            buffer = Marshal.AllocHGlobal((int)structSize);
                            nextResult = FindNextUrlCacheEntry(hEnum, buffer, out structSize);
                        }
                        else if (lastError == Hresults.ERROR_NO_MORE_ITEMS)
                        {
                            break;
                        }
                    }

                    result = (INTERNET_CACHE_ENTRY_INFO)Marshal.PtrToStructure(buffer, typeof(INTERNET_CACHE_ENTRY_INFO));
                    fileUrl = result.lpszSourceUrlName.Substring(result.lpszSourceUrlName.LastIndexOf('@') + 1);
                    if (fileUrl.Contains(sourceUrlFilter))
                        filesList.Add(result.lpszSourceUrlName);

                    if (buffer != IntPtr.Zero)
                    {
                        try { Marshal.FreeHGlobal(buffer); }
                        catch { }
                        buffer = IntPtr.Zero;
                        structSize = 0;
                    }
                }

            }
            finally
            {
                if (hEnum != IntPtr.Zero)
                {
                    FindCloseUrlCache(hEnum);
                }
                if (buffer != IntPtr.Zero)
                {
                    try { Marshal.FreeHGlobal(buffer); }
                    catch { }
                }
            }
            return (filesList);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImportarIPhone
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
        public PROPERTYKEY(string g, uint p) { fmtid = new Guid(g); pid = p; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx; public int cy; }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem2
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
        void GetPropertyStore(int flags, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetPropertyStoreWithCreateObject(int flags, IntPtr punk, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetPropertyStoreForKeys(IntPtr rgKeys, uint cKeys, int flags, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList(ref PROPERTYKEY keyType, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void Update(IntPtr pbc);
        void GetProperty(ref PROPERTYKEY key, IntPtr ppropvar);
        void GetCLSID(ref PROPERTYKEY key, out Guid pclsid);
        [PreserveSig] int GetFileTime(ref PROPERTYKEY key, out long pft);
        [PreserveSig] int GetInt32(ref PROPERTYKEY key, out int pi);
        [PreserveSig] int GetString(ref PROPERTYKEY key, [MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
        [PreserveSig] int GetUInt32(ref PROPERTYKEY key, out uint pui);
        [PreserveSig] int GetUInt64(ref PROPERTYKEY key, out ulong pull);
        [PreserveSig] int GetBool(ref PROPERTYKEY key, out int pf);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumShellItems
    {
        [PreserveSig] int Next(uint celt, [MarshalAs(UnmanagedType.Interface)] out IShellItem rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out IEnumShellItems ppenum);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [ComImport, Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileOperation
    {
        void Advise(IntPtr pfops, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOperationFlags(uint dwOperationFlags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string pszMessage);
        void SetProgressDialog(IntPtr popd);
        void SetProperties(IntPtr pproparray);
        void SetOwnerWindow(IntPtr hwndOwner);
        void ApplyPropertiesToItem(IShellItem psiItem);
        void ApplyPropertiesToItems(IntPtr punkItems);
        void RenameItem(IShellItem psiItem, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void RenameItems(IntPtr pUnkItems, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName);
        void MoveItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszNewName, IntPtr pfopsItem);
        void MoveItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void CopyItem(IShellItem psiItem, IShellItem psiDestinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string pszCopyName, IntPtr pfopsItem);
        void CopyItems(IntPtr punkItems, IShellItem psiDestinationFolder);
        void DeleteItem(IShellItem psiItem, IntPtr pfopsItem);
        void DeleteItems(IntPtr punkItems);
        void NewItem(IShellItem psiDestinationFolder, uint dwFileAttributes, [MarshalAs(UnmanagedType.LPWStr)] string pszName, [MarshalAs(UnmanagedType.LPWStr)] string pszTemplateName, IntPtr pfopsItem);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool pfAnyOperationsAborted);
    }

    public static class Shell
    {
        public static readonly Guid BHID_EnumItems = new Guid("94f60519-2850-4924-aa5a-d15e84868039");
        public static readonly Guid IID_IEnumShellItems = new Guid("70629033-e363-4a28-a567-0db78006e6d7");
        public static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        public static readonly Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
        public static readonly Guid CLSID_FileOperation = new Guid("3ad05575-8857-4850-9277-11b85bdb8e09");

        public const string ThisPC = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
        public const uint SIGDN_NORMALDISPLAY = 0;
        public const uint SIGDN_PARENTRELATIVEPARSING = 0x80018001;
        public const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;
        public const uint SFGAO_FOLDER = 0x20000000;

        public static PROPERTYKEY PKEY_Size = new PROPERTYKEY("b725f130-47ef-101a-a5f1-02608c9eebac", 12);
        public static PROPERTYKEY PKEY_DateModified = new PROPERTYKEY("b725f130-47ef-101a-a5f1-02608c9eebac", 14);
        public static PROPERTYKEY PKEY_FileName = new PROPERTYKEY("41cf5ae0-f75a-4806-bd87-59c7d9248eb9", 100);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [DllImport("shell32.dll", PreserveSig = false)]
        static extern void SHCreateItemFromIDList(IntPtr pidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [DllImport("shell32.dll", PreserveSig = false)]
        static extern void SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, out IntPtr ppidl);
        [DllImport("shell32.dll")]
        static extern uint ILGetSize(IntPtr pidl);
        [DllImport("ole32.dll")]
        static extern void CoTaskMemFree(IntPtr p);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr h);

        public static IShellItem FromParsingName(string path)
        {
            object o; SHCreateItemFromParsingName(path, IntPtr.Zero, IID_IShellItem, out o);
            return (IShellItem)o;
        }

        // PIDL copiado a un byte[] para poder recrear el item en cualquier hilo
        public static byte[] ToIdList(IShellItem item)
        {
            IntPtr pidl; SHGetIDListFromObject(item, out pidl);
            try
            {
                int n = (int)ILGetSize(pidl);
                byte[] b = new byte[n];
                Marshal.Copy(pidl, b, 0, n);
                return b;
            }
            finally { CoTaskMemFree(pidl); }
        }

        public static IShellItem FromIdList(byte[] idl)
        {
            IntPtr p = Marshal.AllocCoTaskMem(idl.Length);
            try
            {
                Marshal.Copy(idl, 0, p, idl.Length);
                object o; SHCreateItemFromIDList(p, IID_IShellItem, out o);
                return (IShellItem)o;
            }
            finally { Marshal.FreeCoTaskMem(p); }
        }

        public static List<IShellItem> Children(IShellItem folder)
        {
            var list = new List<IShellItem>();
            object o;
            folder.BindToHandler(IntPtr.Zero, BHID_EnumItems, IID_IEnumShellItems, out o);
            var en = (IEnumShellItems)o;
            while (true)
            {
                IShellItem child; uint got;
                int hr = en.Next(1, out child, out got);
                if (hr != 0 || got == 0 || child == null) break;
                list.Add(child);
            }
            Marshal.ReleaseComObject(en);
            return list;
        }

        public static string Name(IShellItem item, uint sigdn)
        {
            string s; item.GetDisplayName(sigdn, out s); return s;
        }

        public static bool IsFolder(IShellItem item)
        {
            uint a; item.GetAttributes(SFGAO_FOLDER, out a); return (a & SFGAO_FOLDER) != 0;
        }

        public static string GetString(IShellItem item, PROPERTYKEY key)
        {
            var i2 = item as IShellItem2; if (i2 == null) return null;
            string s; return i2.GetString(ref key, out s) == 0 ? s : null;
        }

        public static long GetSize(IShellItem item)
        {
            var i2 = item as IShellItem2; if (i2 == null) return 0;
            ulong v; var k = PKEY_Size; return i2.GetUInt64(ref k, out v) == 0 ? (long)v : 0;
        }

        public static DateTime? GetDate(IShellItem item)
        {
            var i2 = item as IShellItem2; if (i2 == null) return null;
            long ft; var k = PKEY_DateModified;
            if (i2.GetFileTime(ref k, out ft) != 0 || ft <= 0) return null;
            return DateTime.FromFileTimeUtc(ft);
        }

        public const int SIIGBF_THUMBNAILONLY = 0x8;   // sin íconos genéricos: si no hay miniatura real, falla

        public static Bitmap Thumbnail(IShellItem item, int size, int flags = 0)
        {
            var f = item as IShellItemImageFactory; if (f == null) return null;
            SIZE s; s.cx = size; s.cy = size;
            IntPtr h;
            if (f.GetImage(s, flags, out h) != 0 || h == IntPtr.Zero) return null;
            try { return HBitmapToBitmap(h); }
            finally { DeleteObject(h); }
        }

        static Bitmap HBitmapToBitmap(IntPtr h)
        {
            using (Bitmap src = Image.FromHbitmap(h))
            {
                if (Image.GetPixelFormatSize(src.PixelFormat) < 32) return new Bitmap(src);
                var rect = new Rectangle(0, 0, src.Width, src.Height);
                BitmapData d = src.LockBits(rect, ImageLockMode.ReadOnly, src.PixelFormat);
                try
                {
                    // ¿Tiene canal alfa de verdad? Si todo es 0, es una imagen opaca.
                    // El stride puede ser negativo (imagen guardada de abajo hacia arriba): copiamos fila por fila
                    int row = src.Width * 4;
                    byte[] buf = new byte[row * src.Height];
                    for (int y = 0; y < src.Height; y++)
                        Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), buf, y * row, row);
                    bool alpha = false;
                    for (int i = 3; i < buf.Length; i += 4) { if (buf[i] != 0) { alpha = true; break; } }
                    if (!alpha) return new Bitmap(src);
                    var dst = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
                    BitmapData dd = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < src.Height; y++)
                        Marshal.Copy(buf, y * row, IntPtr.Add(dd.Scan0, y * dd.Stride), row);
                    dst.UnlockBits(dd);
                    return dst;
                }
                finally { src.UnlockBits(d); }
            }
        }

        // Copia un archivo del iPhone a una carpeta local. Es síncrono: vuelve cuando terminó.
        public static void CopyTo(IShellItem item, string destFolder, string newName)
        {
            var dest = FromParsingName(destFolder);
            var op = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOperation));
            try
            {
                // FOF_SILENT | FOF_NOCONFIRMATION | FOF_NOCONFIRMMKDIR | FOF_NOERRORUI
                op.SetOperationFlags(0x0004 | 0x0010 | 0x0200 | 0x0400);
                op.CopyItem(item, dest, newName, IntPtr.Zero);
                op.PerformOperations();
                bool aborted; op.GetAnyOperationsAborted(out aborted);
                if (aborted) throw new Exception(L.T("la copia se canceló", "the copy was cancelled"));
            }
            finally
            {
                Marshal.ReleaseComObject(op);
                Marshal.ReleaseComObject(dest);
            }
        }
    }
}

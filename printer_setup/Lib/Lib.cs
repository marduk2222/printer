using System;
using System.Text;

namespace Lib
{
    public class DllInvoke
    {
        #region DLL
        public enum LoadLibraryFlags : uint
        {
            /// <summary>
            /// DONT_RESOLVE_DLL_REFERENCES
            /// </summary>
            DONT_RESOLVE_DLL_REFERENCES = 0x00000001,

            /// <summary>
            /// LOAD_IGNORE_CODE_AUTHZ_LEVEL
            /// </summary>
            LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,

            /// <summary>
            /// LOAD_LIBRARY_AS_DATAFILE
            /// </summary>
            LOAD_LIBRARY_AS_DATAFILE = 0x00000002,

            /// <summary>
            /// LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE
            /// </summary>
            LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,

            /// <summary>
            /// LOAD_LIBRARY_AS_IMAGE_RESOURCE
            /// </summary>
            LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,

            /// <summary>
            /// LOAD_LIBRARY_SEARCH_APPLICATION_DIR
            /// </summary>
            LOAD_LIBRARY_SEARCH_APPLICATION_DIR = 0x00000200,

            /// <summary>
            /// LOAD_LIBRARY_SEARCH_DEFAULT_DIRS
            /// </summary>
            LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000,

            /// <summary>
            /// LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR
            /// </summary>
            LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100,

            /// <summary>
            /// LOAD_LIBRARY_SEARCH_SYSTEM32
            /// </summary>
            LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800,

            /// <summary>
            /// LOAD_LIBRARY_SEARCH_USER_DIRS
            /// </summary>
            LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400,

            /// <summary>
            /// LOAD_WITH_ALTERED_SEARCH_PATH
            /// </summary>
            LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private extern static IntPtr LoadLibrary(string path);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private extern static IntPtr LoadLibraryEx(string path, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private extern static IntPtr GetProcAddress(IntPtr lib, string funcName);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private extern static bool FreeLibrary(IntPtr lib);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetLastError();

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        public extern static int FormatMessage(int flag, ref IntPtr source, int msgid, int langid, ref string buf, int size, ref IntPtr args);
        #endregion

        private IntPtr hLib { get; set; }
        public DllInvoke(string DLLName)
        {
            if (!System.IO.File.Exists(DLLName)) return;
            var DLLPath = new System.IO.FileInfo(DLLName).FullName;
            //hLib = LoadLibrary();

            hLib = LoadLibraryEx(DLLPath, IntPtr.Zero, LoadLibraryFlags.LOAD_WITH_ALTERED_SEARCH_PATH);
            Console.WriteLine("LoadLibraryEx");
        }

        ~DllInvoke() { if (hLib != IntPtr.Zero) FreeLibrary(hLib); Console.WriteLine("FreeLibrary"); }
        public void Close()
        {
            if (hLib != IntPtr.Zero) FreeLibrary(hLib); Console.WriteLine("FreeLibrary");
        }

        //將要執行的函式轉換為委託
        public Delegate Invoke(string APIName, Type t)
        {
            var func = GetProcAddress(hLib, APIName);
            if (func == IntPtr.Zero)
            {
                var errCode = GetLastError();

                string msg = null;
                FormatMessage(0x1300, ref func, errCode, 0, ref msg, 255, ref func);
                //return msg;
                return null;
            }
            else return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer(func, t);
        }
    }

    public class Config
    {
        #region 讀寫設定檔
        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        public static extern bool WritePrivateProfileString(byte[] Section, byte[] Key, byte[] Value, string FilePath);
        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        public static extern int GetPrivateProfileString(byte[] Section, byte[] Key, byte[] DefaultValue, byte[] RetVal, int Size, string FilePath);
        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern int GetPrivateProfileInt(string Section, string Key, int DefaultValue, string FilePath);
        //与ini交互必须统一编码格式
        private static string FilePath = $@"{AppDomain.CurrentDomain.BaseDirectory}Config.ini";   //@".\Config.ini";
        private static System.Text.Encoding Encoding = System.Text.Encoding.ASCII; //System.Text.Encoding.GetEncoding(950);
        public static string ReadString(string SectionName, string KeyName, string Default, string FilePath, int Size = 1024)
        {
            var Buffer = new byte[Size];
            var Read = GetPrivateProfileString(Encoding.GetBytes(SectionName), Encoding.GetBytes(KeyName), (Default != null) ? Encoding.GetBytes(Default) : null, Buffer, Size, FilePath);
            return (Read != 0) ? Encoding.GetString(Buffer, 0, Read).Trim() : Default;
        }
        public static bool WriteString(string SectionName, string KeyName, string Value, string FilePath) => WritePrivateProfileString(Encoding.GetBytes(SectionName), Encoding.GetBytes(KeyName), Encoding.GetBytes(Value), FilePath);

        public static bool GetBoolean(string Section, string Key, bool DefaultValue) => (GetPrivateProfileInt(Section, Key, (DefaultValue ? 1 : 0), FilePath) == 1);
        public static void SetBoolean(string Section, string Key, bool Value) => WriteString(Section, Key, (Value ? "1" : "0"), FilePath);

        public static int GetInteger(string Section, string Key, int DefaultValue) => GetPrivateProfileInt(Section, Key, DefaultValue, FilePath);
        public static void SetInteger(string Section, string Key, int Value) => WriteString(Section, Key, Value.ToString(), FilePath);

        public static double GetDouble(string Section, string Key, double DefaultValue)
        {
            var Temp = ReadString(Section, Key, null, FilePath);
            return (Temp == null || !double.TryParse(Temp, out var Double)) ? DefaultValue : Double;
        }
        public static void SetDouble(string Section, string Key, double Value) => WriteString(Section, Key, Value.ToString(), FilePath);

        public static string GetString(string Section, string Key, string DefaultValue) => ReadString(Section, Key, DefaultValue, FilePath);
        public static void SetString(string Section, string Key, string Value) => WriteString(Section, Key, Value, FilePath);
        #endregion
    }

    public class Setup
    {
        #region Demo
        public class Demo
        {
            // ========== Demo ==========
            //public static string Str
            //{
            //    get { return Lib.GetString("General", "Str", "123ABC"); }
            //    set { Lib.SetString("General", "Str", value); }
            //}
            //public static bool Bool
            //{
            //    get { return Lib.GetBoolean("General", "Bool", false); }
            //    set { Lib.SetBoolean("General", "Bool", value); }
            //}
            //public static int Int
            //{
            //    get { return Lib.GetInteger("General", "Int", 5); }
            //    set { Lib.SetInteger("General", "Int", value); }
            //}
            //public static double Double
            //{
            //    get { return Lib.GetDouble("General", "Double", 5.0d); }
            //    set { Lib.SetDouble("General", "Double", value); }
            //}
            //public static bool Debug
            //{
            //    get { return Lib.GetBoolean("General", "Debug", false); }
            //    set { Lib.SetBoolean("General", "Debug", value); }
            //}
        }
        #endregion

        #region General
        public class General
        {
            public static string TaxID => Config.GetString("General", "TaxID", null);
            public static int Partner
            {
                get { return Config.GetInteger("General", "Partner", 0); }
                set { Config.SetInteger("General", "Partner", value); }
            }

            /// <summary>
            /// Communication protocol: REST
            /// </summary>
            public static string Protocol => Config.GetString("General", "Protocol", "REST");

            /// <summary>
            /// REST API Base URL (預設對接同 repo 的 printer web 專案)
            /// </summary>
            public static string RestUrl => Config.GetString("General", "RestUrl", "http://localhost:5062");

            /// <summary>
            /// Odoo Database name for authentication
            /// </summary>
            public static string RestDb => Config.GetString("General", "RestDb", null);

            /// <summary>
            /// Odoo Login username for authentication
            /// </summary>
            public static string RestLogin => Config.GetString("General", "RestLogin", null);

            /// <summary>
            /// Odoo Password for authentication
            /// </summary>
            public static string RestPassword => Config.GetString("General", "RestPassword", null);
        }
        #endregion
    }
}
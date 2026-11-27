using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// FANUC FOCAS2 库加载与调用（社区版，.NET Framework 4.8）。
    ///
    /// 为什么用 LoadLibrary 而不是 static DllImport：
    ///   Fwlib32/64.dll 受厂商授权约束，不能随本仓库分发；用户自备后放在程序目录或 focas\win64 下即可。
    ///   静态 DllImport 会在缺库时抛 DllNotFoundException 且无法指定路径，动态加载能给出准确原因。
    ///
    /// 结构体与导出名取自 FANUC 官方 fwlib32.h / Fwlib64 头文件（与官方 C# 示例一致）：
    ///   ODBM   { short datano; short dummy; long mcr_val; short dec_val; }
    ///   ODBACT { short dummy[2]; long data; }
    ///   ODBAXIS{ short dummy; short type; long data[32]; }
    ///   ODBST  { short dummy[2]; short aut, manual, run, edit, motion, mstb, emergency, write, labelskip; ... }
    /// </summary>
    public static class FocasNative
    {
        // ── kernel32 ──
        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // ── 结构（Pack=4 与官方头一致） ──
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct OdbMacro
        {
            public short datano;
            public short dummy;
            public int mcr_val;
            public short dec_val;
            public short padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct OdbAct
        {
            public short d0;
            public short d1;
            public int data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct OdbAxis
        {
            public short dummy;
            public short type;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] data;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct OdbSt
        {
            public short d0;
            public short d1;
            public short aut;
            public short manual;
            public short run;
            public short edit;
            public short motion;
            public short mstb;
            public short emergency;
            public short write;
            public short labelskip;
            public short r1, r2, r3, r4, r5, r6, r7, r8;
        }

        // ── 委托 ──
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short AllocDelegate(string ip, ushort port, int timeout, out ushort handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short RdmAcroDelegate(ushort handle, short number, short length, out OdbMacro macro);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short FreeDelegate(ushort handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short ActsDelegate(ushort handle, out OdbAct act);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short ActfDelegate(ushort handle, out OdbAct act);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short AbsoluteDelegate(ushort handle, short axis, short length, out OdbAxis axisData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short Alarm2Delegate(ushort handle, out int alarmNo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate short StatinfoDelegate(ushort handle, out OdbSt st);

        public static AllocDelegate Alloc { get; private set; }
        public static RdmAcroDelegate RdmAcro { get; private set; }
        public static FreeDelegate Free { get; private set; }
        public static ActsDelegate Acts { get; private set; }
        public static ActfDelegate Actf { get; private set; }
        public static AbsoluteDelegate Absolute { get; private set; }
        public static Alarm2Delegate Alarm2 { get; private set; }
        public static StatinfoDelegate Statinfo { get; private set; }

        public static string LoadedPath { get; private set; }
        public static string LastError { get; private set; }
        public static bool IsLoaded { get { return Alloc != null; } }

        private static IntPtr _lib = IntPtr.Zero;
        private static readonly object _gate = new object();

        /// <summary>按优先级尝试加载 FOCAS 库。成功返回 true；失败时 LastError 说明原因（含尝试过的路径）。</summary>
        public static bool TryLoad(string explicitPath)
        {
            lock (_gate)
            {
                if (IsLoaded) return true;

                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(explicitPath)) candidates.Add(explicitPath.Trim());

                var dir = AppDomain.CurrentDomain.BaseDirectory;
                bool is64 = IntPtr.Size == 8;

                // 约定目录：focas\win64、focas\win32；也接受程序根目录平铺
                if (is64)
                {
                    candidates.Add(Path.Combine(dir, "focas", "win64", "Fwlib64.dll"));
                    candidates.Add(Path.Combine(dir, "Fwlib64.dll"));
                }
                else
                {
                    candidates.Add(Path.Combine(dir, "focas", "win32", "Fwlib32.dll"));
                    candidates.Add(Path.Combine(dir, "Fwlib32.dll"));
                }

                var tried = new List<string>();
                foreach (var cand in candidates)
                {
                    tried.Add(cand);
                    if (!File.Exists(cand)) continue;

                    var h = LoadLibrary(cand);
                    if (h == IntPtr.Zero)
                    {
                        var code = Marshal.GetLastWin32Error();
                        LastError = "LoadLibrary 失败(" + code + ")：" + cand +
                                    (code == 193 ? "（位数不符：64 位程序只能加载 Fwlib64.dll）" : "");
                        continue;
                    }

                    var alloc = GetProcAddress(h, "cnc_allclibhndl3");
                    var rd = GetProcAddress(h, "cnc_rdmacro");
                    if (alloc == IntPtr.Zero || rd == IntPtr.Zero)
                    {
                        FreeLibrary(h);
                        LastError = "该 DLL 不含 cnc_allclibhndl3 / cnc_rdmacro 导出：" + cand +
                                    "（可能不是 FOCAS 库，或机型库 fwlib*32/64.dll 未放在同目录）";
                        continue;
                    }

                    Alloc = (AllocDelegate)Marshal.GetDelegateForFunctionPointer(alloc, typeof(AllocDelegate));
                    RdmAcro = (RdmAcroDelegate)Marshal.GetDelegateForFunctionPointer(rd, typeof(RdmAcroDelegate));

                    var free = GetProcAddress(h, "cnc_freelibhndl");
                    if (free == IntPtr.Zero) free = GetProcAddress(h, "cnc_freehndl");
                    if (free != IntPtr.Zero)
                        Free = (FreeDelegate)Marshal.GetDelegateForFunctionPointer(free, typeof(FreeDelegate));

                    BindOptional(h);

                    _lib = h;
                    LoadedPath = cand;
                    LastError = "";
                    return true;
                }

                if (string.IsNullOrEmpty(LastError))
                    LastError = "未找到 FOCAS 库。已尝试：" + string.Join("；", tried) +
                                "。请向 FANUC 或其授权代理取得 Fwlib64.dll（64 位，需同目录的 fwlib*64.dll 机型库），" +
                                "放到程序目录或 focas\\win64\\ 下。";
                return false;
            }
        }

        /// <summary>可选导出：缺失只影响对应动态点位，不影响宏变量。</summary>
        private static void BindOptional(IntPtr h)
        {
            var acts = GetProcAddress(h, "cnc_acts");
            if (acts != IntPtr.Zero)
                Acts = (ActsDelegate)Marshal.GetDelegateForFunctionPointer(acts, typeof(ActsDelegate));

            var actf = GetProcAddress(h, "cnc_actf");
            if (actf != IntPtr.Zero)
                Actf = (ActfDelegate)Marshal.GetDelegateForFunctionPointer(actf, typeof(ActfDelegate));

            var abs = GetProcAddress(h, "cnc_absolute");
            if (abs != IntPtr.Zero)
                Absolute = (AbsoluteDelegate)Marshal.GetDelegateForFunctionPointer(abs, typeof(AbsoluteDelegate));

            var alm = GetProcAddress(h, "cnc_alarm2");
            if (alm != IntPtr.Zero)
                Alarm2 = (Alarm2Delegate)Marshal.GetDelegateForFunctionPointer(alm, typeof(Alarm2Delegate));

            var st = GetProcAddress(h, "cnc_statinfo");
            if (st != IntPtr.Zero)
                Statinfo = (StatinfoDelegate)Marshal.GetDelegateForFunctionPointer(st, typeof(StatinfoDelegate));
        }

        public static void Unload()
        {
            lock (_gate)
            {
                if (_lib != IntPtr.Zero)
                {
                    try { FreeLibrary(_lib); } catch { }
                    _lib = IntPtr.Zero;
                }
                Alloc = null; RdmAcro = null; Free = null;
                Acts = null; Actf = null; Absolute = null; Alarm2 = null; Statinfo = null;
                LoadedPath = null;
            }
        }

        /// <summary>FOCAS 返回码 → 可读说明（常用码，取自官方 EW_* 定义）。</summary>
        public static string DescribeError(short code)
        {
            switch (code)
            {
                case 0: return "EW_OK(0)";
                case -1: return "EW_BUSY(-1) 忙";
                case -2: return "EW_RESET(-2) 复位/停止中断";
                case -3: return "EW_MMCSYS(-3) MMC 系统错误";
                case -6: return "EW_UNEXP(-6) 异常错误";
                case -7: return "EW_VERSION(-7) CNC/PMC 版本不匹配";
                case -8: return "EW_HANDLE(-8) 句柄无效（未连接/已释放）";
                case -11: return "EW_BUS(-11) 总线错误";
                case -15: return "EW_NODLL(-15) 找不到机型库（fwlib*64.dll 需与 Fwlib64.dll 同目录）";
                case -16: return "EW_SOCKET(-16) 网络连接失败（IP/端口/防火墙，或 CNC 未开以太网）";
                case 1: return "EW_FUNC(1) 功能/命令不支持";
                case 2: return "EW_LENGTH(2) 数据块长度错";
                case 3: return "EW_NUMBER(3) 数据号/地址范围错（宏号不存在或未开通）";
                case 4: return "EW_ATTRIB(4) 数据属性/类型错";
                case 5: return "EW_DATA(5) 数据错";
                case 6: return "EW_NOOPT(6) 机床未开通该选项";
                case 7: return "EW_PROT(7) 写保护";
                case 8: return "EW_PARAM(8) CNC 参数错误";
                default: return "错误码 " + code;
            }
        }
    }
}

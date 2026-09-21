using System;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// Modbus 寄存器数据解析工具（处理字节序）
    /// </summary>
    public static class ModbusHelper
    {
        /// <summary>
        /// 将 ushort[] 寄存器数组按指定字节序转换为字节数组
        /// </summary>
        public static byte[] RegistersToBytes(ushort[] registers, ByteOrder order)
        {
            int count = registers.Length;
            byte[] bytes = new byte[count * 2];

            for (int i = 0; i < count; i++)
            {
                // 默认 ABCD：每个寄存器内高字节在前
                bytes[i * 2] = (byte)(registers[i] >> 8);
                bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
            }

            switch (order)
            {
                case ByteOrder.DCBA:
                    // 完全反转
                    Array.Reverse(bytes);
                    break;

                case ByteOrder.BADC:
                    // 每 2 字节内部交换（寄存器数不变、字间顺序不变 → 相当于每对字节交换）
                    for (int i = 0; i < count; i++)
                    {
                        byte tmp = bytes[i * 2];
                        bytes[i * 2] = bytes[i * 2 + 1];
                        bytes[i * 2 + 1] = tmp;
                    }
                    break;

                case ByteOrder.CDAB:
                    // 每 4 字节组内，前2和后2交换 → 相当于字间交换，字内不变
                    for (int i = 0; i < count / 2; i++)
                    {
                        int idx = i * 4;
                        byte tmp0 = bytes[idx];
                        byte tmp1 = bytes[idx + 1];
                        bytes[idx] = bytes[idx + 2];
                        bytes[idx + 1] = bytes[idx + 3];
                        bytes[idx + 2] = tmp0;
                        bytes[idx + 3] = tmp1;
                    }
                    break;

                // ABCD: 默认，无需调整
            }

            return bytes;
        }

        /// <summary>
        /// 按"值的字节序"返回 BitConverter 可直接解释的数值字节数组(小端序)。
        /// 修复(2026-09-09):BitConverter 在 Windows x86/x64 上是小端序,而寄存器原始流
        /// 与 RegistersToBytes 的输出是大端序;此前 float/int32/uint32/int64/uint64/double
        /// 等数值类型直接把大端字节交给 BitConverter 解析,导致默认 ABCD(大端)配置下
        /// 标准 Modbus 设备解出乱值(例:设备值 33.342533 被解成 -13.876;11.87 被解成 -0.000115)。
        /// 现约定:RegistersToBytes 输出"值的大端字节序"(供字符串/协议帧使用),
        /// RegistersToNumericBytes 输出"值的小端字节序"(供 BitConverter 使用)。
        /// </summary>
        public static byte[] RegistersToNumericBytes(ushort[] registers, ByteOrder order)
        {
            byte[] bytes = RegistersToBytes(registers, order);
            Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Modbus 四区地址解析结果（修复 2026-09-21：此前地址原样当保持寄存器协议地址直读，
        /// 30001/40001 全部读错区，表现为"连接成功但采集值全 0"）
        /// </summary>
        public sealed class ModbusAddress
        {
            /// <summary>功能码：1=线圈 2=离散输入 3=保持寄存器 4=输入寄存器</summary>
            public byte FunctionCode;
            /// <summary>0 基协议地址</summary>
            public ushort Address;
        }

        /// <summary>
        /// 解析 Modbus 变量地址，支持 1-based 四区前缀约定：
        /// 0xxxx=线圈(fc01)、1xxxx=离散输入(fc02)、3xxxx=输入寄存器(fc04)、4xxxx=保持寄存器(fc03)，
        /// 前缀后数字为 1 基序号（例：30001 → fc04@协议地址0）；
        /// 纯数字（位数不足 5 位，或 2/5-9 开头）按保持寄存器(fc03) 0 基协议地址直读，与旧版行为兼容。
        /// </summary>
        public static bool TryResolveAddress(string raw, out ModbusAddress resolved, out string error)
        {
            resolved = null;
            error = null;
            if (raw == null) { error = "地址为空"; return false; }
            string s = raw.Trim();
            if (s.Length == 0) { error = "地址为空"; return false; }
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') { error = "地址只能为数字: " + raw; return false; }
            }
            if (s.Length >= 5 && (s[0] == '0' || s[0] == '1' || s[0] == '3' || s[0] == '4'))
            {
                byte fc = s[0] == '0' ? (byte)1 : (s[0] == '1' ? (byte)2 : (s[0] == '3' ? (byte)4 : (byte)3));
                long seq;
                if (!long.TryParse(s.Substring(1), out seq) || seq < 1)
                {
                    error = string.Format("地址 {0}：{1}xxxx 区序号须从 1 开始（1-based）", raw, s[0]);
                    return false;
                }
                long zeroBased = seq - 1;
                if (zeroBased > 65535)
                {
                    error = string.Format("地址 {0} 超出 Modbus 寻址范围（协议地址≤65535）", raw);
                    return false;
                }
                resolved = new ModbusAddress { FunctionCode = fc, Address = (ushort)zeroBased };
                return true;
            }
            long plain;
            if (!long.TryParse(s, out plain) || plain < 0 || plain > 65535)
            {
                error = string.Format("地址 {0} 无效（应为 0~65535 或 0/1/3/4xxxx 区前缀）", raw);
                return false;
            }
            resolved = new ModbusAddress { FunctionCode = 3, Address = (ushort)plain };
            return true;
        }
    }
}

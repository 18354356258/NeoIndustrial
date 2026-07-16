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
    }
}

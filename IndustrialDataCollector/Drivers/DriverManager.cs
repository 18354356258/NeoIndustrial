using System;
using System.Collections.Generic;
using IndustrialDataCollection.Models;

namespace IndustrialDataCollection.Drivers
{
    /// <summary>
    /// 驱动管理器 - 根据驱动类型创建对应的驱动实例
    /// </summary>
    public static class DriverManager
    {
        /// <summary>
        /// 生产就绪驱动列表——所有已实现真实协议的驱动
        /// </summary>
        private static readonly HashSet<string> ProductionReadyDrivers = new HashSet<string>
        {
            "ModbusTcp", "ModbusRtu", "SiemensS7", "Siemens840D",
            "OpcUa", "OpcUaPubSub", "OPCDA", "EtherNetIp",
            "FanucFocas", "HaasCNC", "Mazak", "Heidenhain",
            "KeyenceKV", "MitsubishiFX", "MELSECMc", "PanasonicMewtocol",
            "Fins", "HostLink", "CODESYS", "BeckhoffADS",
            "BACnet", "IEC104", "IEC61850", "DNP3",
            "KNX", "DALI", "MBus", "DLMS",
            "PROFIBUS", "DeviceNet", "CCLink", "HARTIP",
            "MqttSubscribe", "HttpRest", "MTConnect", "SparkplugB",
            "SecsGem", "Profinet", "LonWorks", "Simulator"
        };

        /// <summary>
        /// 判断驱动是否为生产就绪（有真实协议实现）
        /// </summary>
        public static bool IsProductionReady(string driverType)
            => !string.IsNullOrEmpty(driverType) && ProductionReadyDrivers.Contains(driverType);
        /// <summary>
        /// 根据设备配置创建驱动实例
        /// </summary>
        public static IDriver CreateDriver(DeviceConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            switch (config.DriverType)
            {
                case "ModbusTcp":
                    return new ModbusTcpDriver();
                case "SiemensS7":
                    return new SiemensS7Driver();
                case "Simulator":
                    return new SimulatorDriver();
                case "ModbusRtu":
                    return new ModbusRtuDriver();
                case "MqttSubscribe":
                    return new MqttSubscribeDriver();
                case "OpcUa":
                    return new OpcUaDriver();
                case "HttpRest":
                    return new HttpRestDriver();
                case "EtherNetIp":
                    return new EtherNetIpDriver();
                case "Profinet":
                    return new ProfinetDriver();
                case "BACnet":
                    return new BACnetDriver();
                case "IEC104":
                    return new IEC104Driver();
                case "MELSECMc":
                    return new MELSECMcDriver();
                case "Fins":
                    return new FinsDriver();
                case "HostLink":
                    return new HostLinkDriver();
                case "KeyenceKV":
                    return new KeyenceKVDriver();
                case "IEC61850":
                    return new IEC61850Driver();
                case "DNP3":
                    return new DNP3Driver();
                case "LonWorks":
                    return new LonWorksDriver();
                case "KNX":
                    return new KNXDriver();
                case "SecsGem":
                    return new SecsGemDriver();
                case "FanucFocas":
                    return new FanucFocasDriver();
                case "MTConnect":
                    return new MTConnectDriver();
                case "Heidenhain":
                    return new HeidenhainDriver();
                case "OpcUaPubSub":
                    return new OpcUaPubSubDriver();
                case "SparkplugB":
                    return new SparkplugBDriver();
                case "CODESYS":
                    return new CODESYSDriver();
                case "BeckhoffADS":
                    return new BeckhoffADSDriver();
                case "CCLink":
                    return new CCLinkDriver();
                case "PROFIBUS":
                    return new PROFIBUSDriver();
                case "DeviceNet":
                    return new DeviceNetDriver();
                case "MitsubishiFX":
                    return new MitsubishiFXDriver();
                case "PanasonicMewtocol":
                    return new PanasonicMewtocolDriver();
                case "HaasCNC":
                    return new HaasCNCDriver();
                case "Siemens840D":
                    return new Siemens840DDriver();
                case "Mazak":
                    return new MazakDriver();
                case "DALI":
                    return new DALIDriver();
                case "MBus":
                    return new MBusDriver();
                case "DLMS":
                    return new DLMSDriver();
                case "HARTIP":
                    return new HARTIPDriver();
                case "OPCDA":
                    return new OPCDADriver();
                default:
                    throw new NotSupportedException($"不支持的驱动类型: {config.DriverType}");
            }
        }
    }
}

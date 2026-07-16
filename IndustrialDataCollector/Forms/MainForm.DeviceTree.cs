using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// MainForm 设备树分部 —— 设备列表、分组管理、拖拽排序、搜索过滤、MQTT JSON 视图
    /// 
    /// 规则 52：新设备树相关代码一律写在此文件中，不往 MainForm.cs 追加
    /// 现有 MainForm.cs 中的设备树代码逐步迁移至此
    /// </summary>
    public partial class MainForm
    {
        // 设备树相关字段声明已移至此处，通过 partial class 与 MainForm.cs 共享
    }
}

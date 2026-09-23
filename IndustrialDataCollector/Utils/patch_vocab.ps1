$file = 'C:\Users\Administrator\Desktop\C#\IndustrialDataCollectionⅡ\IndustrialDataCollection\Utils\IndustrialVocabulary.cs'
$content = Get-Content $file -Raw -Encoding UTF8

# Find the closing of the Map dictionary
$oldMarker = '"旧", "old"'
$closingMarker = '        };'

# Build the new vocabulary section
$newVocab = @'

            // ====================================================================
            // v1.8.0 massive expansion — 350+ new compound-ready vocabulary terms
            // ====================================================================

            // ── Manufacturing Basic Terms ──
            {"模拟", "simulation"}, {"数字", "digital"}, {"输入", "input"}, {"输出", "output"},
            {"计算", "calculation"}, {"修正", "correction"}, {"补偿", "compensation"},
            {"补偿值", "offset_value"}, {"修正值", "correction_value"}, {"计算值", "calculated_value"},
            {"实测值", "measured_value"}, {"目标值", "target_value"}, {"初始值", "initial_value"},
            {"极限值", "limit_value"}, {"平均值", "average"}, {"最大值", "maximum"}, {"最小值", "minimum"},
            {"累计值", "accumulated"}, {"瞬时值", "instantaneous"}, {"有效值", "rms_value"},
            {"实验", "experiment"}, {"实际", "actual"}, {"标定", "calibration"},
            {"校准", "calibrate"}, {"校验", "verify"}, {"标定值", "calibration_value"},
            {"测量", "measurement"}, {"监测", "monitoring"}, {"监控", "monitor"},
            {"控制", "control"}, {"调节", "adjust"}, {"配置", "configuration"},
            {"额定", "rated"}, {"标称", "nominal"}, {"标称值", "nominal_value"},
            {"默认", "default"}, {"参考", "reference"}, {"参考值", "reference_value"},
            {"信号", "signal"}, {"模拟量输入", "analog_input"}, {"模拟量输出", "analog_output"},
            {"数字量输入", "digital_input"}, {"数字量输出", "digital_output"},
            {"脉冲输入", "pulse_input"}, {"脉冲输出", "pulse_output"},
            {"开关量输入", "switch_input"}, {"开关量输出", "switch_output"},
            {"运行时间", "runtime"}, {"工作时间", "work_time"}, {"累计时间", "accumulated_time"},
            {"等待时间", "wait_time"}, {"循环时间", "cycle_time"}, {"周期时间", "period_time"},
            {"延迟", "delay"}, {"延时", "delay_time"}, {"超时", "timeout"},

            // ── Material Flow Terms ──
            {"物料", "material"}, {"辅料", "auxiliary_material"},
            {"中间品", "intermediate"}, {"原料仓", "raw_material_bin"}, {"成品仓", "product_bin"},
            {"混料", "blend"}, {"配比", "ratio"}, {"配方", "recipe"},
            {"批次", "batch"}, {"批号", "batch_number"},
            {"净重", "net_weight"}, {"毛重", "gross_weight"}, {"皮重", "tare_weight"},
            {"计量", "metering"}, {"给料", "feeding"},
            {"上料", "load"}, {"下料", "unload"},
            {"碎料", "crushed_material"}, {"余料", "residual_material"}, {"废料", "waste_material"},

            // ── Operating States ──
            {"正常", "normal"}, {"异常", "abnormal"}, {"运行中", "running"},
            {"停止中", "stopped"}, {"待机中", "standby"}, {"暂停中", "paused"},
            {"维护中", "maintenance"}, {"故障中", "fault"}, {"报警中", "alarm"},
            {"离线", "offline"}, {"在线", "online"}, {"就绪", "ready"}, {"未就绪", "not_ready"},
            {"完成", "completed"}, {"进行中", "in_progress"}, {"中断", "interrupted"},
            {"复位中", "resetting"}, {"启动中", "starting"},

            // ── Mechanical / Forming Specific ──
            {"挤压", "extrusion"}, {"挤压力", "extrusion_force"},
            {"挤压温度", "extrusion_temperature"},
            {"压余", "butt"}, {"残料", "remnant"}, {"闷车", "stuck"}, {"拖模", "drag_mark"},
            {"划伤", "scratch"}, {"擦伤", "abrasion"}, {"碰伤", "dent"}, {"扭拧", "twist"},
            {"弯曲度", "bend_degree"}, {"平面度", "flatness"}, {"垂直度", "perpendicularity"},
            {"平行度", "parallelism"}, {"圆度", "roundness"}, {"同轴度", "concentricity"},
            {"对称度", "symmetry"}, {"跳动", "runout"}, {"轮廓度", "profile_tolerance"},
            {"位置度", "position_tolerance"},

            // ── Metal Grades / Alloys ──
            {"铝合金", "aluminum_alloy"}, {"6063", "aa6063"}, {"6061", "aa6061"},
            {"6060", "aa6060"}, {"6005", "aa6005"}, {"6082", "aa6082"}, {"7075", "aa7075"},
            {"纯铝", "pure_aluminum"}, {"铜合金", "copper_alloy"}, {"镁合金", "magnesium_alloy"},
            {"钛合金", "titanium_alloy"}, {"锌合金", "zinc_alloy"}, {"不锈钢", "stainless_steel"},
            {"碳钢", "carbon_steel"}, {"合金钢", "alloy_steel"}, {"模具钢", "die_steel"},
            {"工具钢", "tool_steel"}, {"高速钢", "high_speed_steel"}, {"硬质合金", "carbide"},

            // ── Temperature-Specific ──
            {"炉温", "furnace_temperature"}, {"模温", "mold_temperature"},
            {"料温", "material_temperature"}, {"棒温", "billet_temperature"},
            {"出口温度", "outlet_temperature"}, {"入口温度", "inlet_temperature"},
            {"环境温度", "ambient_temperature"}, {"设定温度", "set_temperature"},
            {"实际温度", "actual_temperature"}, {"温度偏差", "temperature_deviation"},
            {"温差", "temperature_difference"}, {"过热", "overheat"}, {"过冷", "supercooling"},
            {"加热段", "heating_zone"}, {"冷却段", "cooling_zone"},
            {"保温段", "holding_zone"}, {"预热段", "preheating_zone"},

            // ── Pressure-Specific ──
            {"液压压力", "hydraulic_pressure"}, {"气压", "air_pressure"},
            {"系统压力", "system_pressure"}, {"工作压力", "working_pressure"},
            {"设定压力", "set_pressure"}, {"实际压力", "actual_pressure"},
            {"主缸压力", "main_cylinder_pressure"}, {"侧缸压力", "side_cylinder_pressure"},
            {"背压", "back_pressure"}, {"前腔压力", "front_chamber_pressure"},
            {"后腔压力", "rear_chamber_pressure"}, {"锁模压力", "clamping_pressure"},
            {"注射压力", "injection_pressure"}, {"保压压力", "holding_pressure"},
            {"充填压力", "filling_pressure"},

            // ── More Equipment Types ──
            {"加热炉", "heating_furnace"}, {"保温炉", "holding_furnace"},
            {"熔炼炉", "melting_furnace"}, {"热处理炉", "heat_treatment_furnace"},
            {"时效炉", "aging_furnace"}, {"退火炉", "annealing_furnace"},
            {"淬火炉", "quenching_furnace"}, {"回火炉", "tempering_furnace"},
            {"感应炉", "induction_furnace"}, {"电弧炉", "arc_furnace"},
            {"电阻炉", "resistance_furnace"}, {"工频炉", "line_frequency_furnace"},
            {"中频炉", "medium_frequency_furnace"}, {"高频炉", "high_frequency_furnace"},
            {"热剪", "hot_shear"}, {"冷剪", "cold_shear"}, {"锯床", "sawing_machine"},
            {"矫直机", "straightener"}, {"拉直机", "stretcher"}, {"扭拧机", "twister"},
            {"矫平机", "leveler"}, {"倒角机", "chamfering_machine"},
            {"去毛刺机", "deburring_machine"}, {"喷砂机", "sandblasting_machine"},
            {"抛光机", "polishing_machine"}, {"淬火槽", "quenching_tank"},
            {"水槽", "water_tank"}, {"油槽", "oil_tank"},

            // ── Electrical ──
            {"电流表", "ammeter"}, {"电压表", "voltmeter"}, {"功率表", "wattmeter"},
            {"电能表", "energy_meter"}, {"频率表", "frequency_meter"},
            {"功率因数表", "power_factor_meter"}, {"相电压", "phase_voltage"},
            {"线电压", "line_voltage"}, {"相电流", "phase_current"}, {"线电流", "line_current"},
            {"有功功率", "active_power"}, {"无功功率", "reactive_power"},
            {"总谐波", "total_harmonic"}, {"电压谐波", "voltage_harmonic"},
            {"电流谐波", "current_harmonic"}, {"漏电流", "leakage_current"},
            {"接地电阻", "ground_resistance"}, {"绝缘电阻", "insulation_resistance"},
            {"绕组温度", "winding_temperature"}, {"轴承温度", "bearing_temperature"},
            {"外壳温度", "housing_temperature"}, {"定子", "stator"}, {"转子", "rotor"},

            // ── Building / Facility ──
            {"厂房", "plant_building"}, {"仓库", "warehouse"},
            {"办公室", "office"}, {"实验室", "laboratory"}, {"控制室", "control_room"},
            {"配电室", "power_room"}, {"配电间", "power_distribution_room"},
            {"空压站", "compressor_station"}, {"水泵房", "pump_house"},
            {"锅炉房", "boiler_room"}, {"冷冻站", "refrigeration_station"},
            {"空调机房", "hvac_room"}, {"消防泵房", "fire_pump_room"}, {"门卫", "gatehouse"},

            // ── Safety / Environment ──
            {"安全", "safety"}, {"安全门", "safety_door"},
            {"安全光幕", "safety_light_curtain"}, {"安全光栅", "safety_grating"},
            {"安全继电器", "safety_relay"}, {"安全PLC", "safety_plc"},
            {"急停按钮", "e_stop"}, {"紧急停止", "emergency_stop"},
            {"消防", "fire_protection"}, {"灭火", "fire_extinguishing"},
            {"烟雾探测", "smoke_detection"}, {"火焰探测", "flame_detection"},
            {"温度探测", "temperature_detection"}, {"气体探测", "gas_detection"},
            {"燃气探测", "combustible_gas_detection"}, {"有毒气体", "toxic_gas"},
            {"可燃气体", "combustible_gas"}, {"粉尘浓度", "dust_concentration"},
            {"噪音检测", "noise_monitoring"}, {"排放", "emission"},
            {"废气排放", "exhaust_emission"}, {"废水排放", "wastewater_discharge"},
            {"固废", "solid_waste"}, {"危废", "hazardous_waste"},

            // ── Common Equipment Numbering / Naming Words ──
            {"号线体", "line_body"}, {"机台", "machine"}, {"机列", "machine_line"},
            {"号炉", "furnace_no"},
            {"前段", "front_section"}, {"后段", "rear_section"}, {"中段", "middle_section"},
            {"上段", "upper_section"}, {"下段", "lower_section"},
            {"左段", "left_section"}, {"右段", "right_section"},
            {"前区", "front_zone"}, {"后区", "rear_zone"}, {"中区", "middle_zone"},
            {"上区", "upper_zone"}, {"下区", "lower_zone"},
            {"主线", "main_line"}, {"副线", "auxiliary_line"}, {"支线", "branch_line"},
            {"装配线", "assembly_line"}, {"生产线", "production_line"},
            {"包装线", "packaging_line"}, {"检测线", "inspection_line"},
'@

# Find the position of the closing }; after the last old entry
$lastOldIdx = $content.LastIndexOf('"旧", "old"')
if ($lastOldIdx -lt 0) {
    Write-Error "Could not find marker"
    exit 1
}

# Find the closing }; after this point
$closingIdx = $content.IndexOf('        };', $lastOldIdx)
if ($closingIdx -lt 0) {
    Write-Error "Could not find closing };"
    exit 1
}

# Insert new vocab before };
$content = $content.Substring(0, $closingIdx) + $newVocab + "`r`n" + $content.Substring($closingIdx)

# Now modify Translate method to fall back to TranslateCompound
$translatePattern = 'return chinese; // fallback.*'
$translateReplace = 'return TranslateCompound(chinese);'
$content = $content -replace $translatePattern, $translateReplace

# Add TranslateCompound method after Translate
$translateMethodEnd = 'if \(Map\.TryGetValue\(chinese, out string english\)\) return english;\s*// Fallback: try compound decomposition\s*return TranslateCompound\(chinese\);\s*\}'
# Actually, let me insert after the closing brace of Translate

# Find the end of Translate method
$translateEndIdx = $content.IndexOf('return TranslateCompound(chinese);')
if ($translateEndIdx -lt 0) {
    Write-Error "Could not find TranslateCompound call"
    exit 1
}
# Find the } that closes Translate
$closeBraceIdx = $content.IndexOf('}', $translateEndIdx)
# Find next line with TryGet
$tryGetIdx = $content.IndexOf('public static bool TryGet', $closeBraceIdx)

# The content between closeBrace and tryGet - insert TranslateCompound there
$before = $content.Substring(0, $closeBraceIdx + 1)
$after = $content.Substring($closeBraceIdx + 1)

$translateCompound = @'

        /// <summary>
        /// Translate a Chinese string by decomposing unknown compounds into known vocabulary words.
        /// Uses greedy left-to-right longest-prefix matching against the vocabulary dictionary.
        /// Unknown segments are left as-is (will be converted to pinyin-like or stripped by Slugify).
        /// </summary>
        public static string TranslateCompound(string chinese)
        {
            if (string.IsNullOrEmpty(chinese)) return "";

            // Try exact match first (fast path)
            if (Map.TryGetValue(chinese, out string exact)) return exact;

            // Greedy left-to-right segmentation
            var result = new System.Text.StringBuilder();
            int i = 0;
            while (i < chinese.Length)
            {
                bool found = false;
                // Try longest match first (max 6 chars, typical Chinese compound)
                for (int len = Math.Min(6, chinese.Length - i); len >= 1; len--)
                {
                    string token = chinese.Substring(i, len);
                    if (Map.TryGetValue(token, out string english))
                    {
                        if (result.Length > 0) result.Append('_');
                        result.Append(english);
                        i += len;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    // Single unknown character — try to keep digits/letters, skip others
                    char c = chinese[i];
                    if (char.IsLetterOrDigit(c))
                    {
                        if (result.Length > 0) result.Append('_');
                        result.Append(c);
                    }
                    i++;
                }
            }
            return result.ToString();
        }
'@

$content = $before + $translateCompound + $after

Set-Content $file $content -Encoding UTF8 -NoNewline
Write-Host "Done patching IndustrialVocabulary.cs"

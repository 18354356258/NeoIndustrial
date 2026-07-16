using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace IndustrialDataCollection.Services
{
    // ======================== Stub: 社区版不含认证系统 ========================
    public class AuthService
    {
        public static AuthService Instance => _instance.Value;
        private static readonly Lazy<AuthService> _instance = new Lazy<AuthService>(() => new AuthService());
        public string CurrentUser => "社区版用户";
        public void Initialize() { }
        public void Logout() { }
    }

    public class LicenseService
    {
        public static LicenseService Instance => _instance.Value;
        private static readonly Lazy<LicenseService> _instance = new Lazy<LicenseService>(() => new LicenseService());
        public bool IsActivated() => true;
        public dynamic GetCurrentLicense() => new
        {
            LicenseTypeDisplay = "社区版",
            MaxDevices = 999,
            IsPermanent = true,
            ExpireDate = "永不",
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd")
        };
        public string GetMachineId() => "COMMUNITY-EDITION";
    }

    public class ApplicationLifecycle
    {
        public static ApplicationLifecycle Instance => _instance.Value;
        private static readonly Lazy<ApplicationLifecycle> _instance = new Lazy<ApplicationLifecycle>(() => new ApplicationLifecycle());
        public void Init() { }
        public void Register(string name, Action<string> shutdownAction) { }
        public void Shutdown() { }
    }

    // ======================== Stub: 语义层（企业版专属） ========================
    public class SemanticService
    {
        public static SemanticService Instance => _instance.Value;
        private static readonly Lazy<SemanticService> _instance = new Lazy<SemanticService>(() => new SemanticService());
        public void Init() { }
        public void SyncFromDeviceConfigs(List<Models.DeviceConfig> devices) { }
        public void SyncFromDeviceConfigs() { }
        public void SyncFromDataSources(List<Models.DataSourceConnection> sources, DataSourceService dss) { }
        public Models.SemanticNode GetNodeBySource(string sourceType, string sourceId) => null;
        public void UpdateNodeStatus(string nodeId, Models.NodeStatus status) { }
        public void SaveEvent(Models.SemanticVariableEvent evt) { }
        public void GetBindingCounts(string nodeId, out int relationCount, out int eventCount)
        { relationCount = 0; eventCount = 0; }
    }

    // ======================== Stub: 离线缓存 ========================
    public class OfflineCacheService
    {
        public static OfflineCacheService Instance => _instance.Value;
        private static readonly Lazy<OfflineCacheService> _instance = new Lazy<OfflineCacheService>(() => new OfflineCacheService());

        // 可注入的委托属性
        public Func<bool> IsMqttConnected { get; set; }
        public Func<bool> IsDbConnected { get; set; }
        public Func<Drivers.CycleDataBatch, Task<bool>> MqttFlushHandler { get; set; }
        public Func<Drivers.CycleDataBatch, Task<bool>> DbFlushHandler { get; set; }

        public void Init() { }
        public void Start() { }
        public void Stop() { }
        public void WriteHeartbeat() { }
        public void RecordHeartbeat(string deviceName, string driver, bool success, string errorMsg = null) { }
        public dynamic StoreBatch(Drivers.CycleDataBatch batch, bool needMqtt, bool needDb)
            => new { MqttId = "", DbId = "" };
        public void MarkMqttSent(string id) { }
        public void MarkDbSent(string id) { }
        public int GetPendingCount() => 0;
        public Task FlushAsync() => Task.CompletedTask;
    }

    // ======================== Stub: 数据流引擎 ========================
    public class DataStreamEngine
    {
        public static DataStreamEngine Instance => _instance.Value;
        private static readonly Lazy<DataStreamEngine> _instance = new Lazy<DataStreamEngine>(() => new DataStreamEngine());
        public void Init() { }
        public void Start() { }
        public void Stop() { }
        public Task<Dictionary<string, bool>> RouteAsync(Drivers.CycleDataBatch batch)
            => Task.FromResult(new Dictionary<string, bool> { ["MQTT"] = true, ["Database"] = true });
    }

    // ======================== Stub: 事件处理 ========================
    public class EventProcessingService
    {
        public static EventProcessingService Instance => _instance.Value;
        private static readonly Lazy<EventProcessingService> _instance = new Lazy<EventProcessingService>(() => new EventProcessingService());
        public void Process(Models.SemanticVariableEvent evt) { }
    }

    // ======================== Stub: Tag 服务 ========================
    public class TagGenerationService
    {
        public static void GenerateTags(List<Models.DataPoint> points, Models.DeviceConfig device) { }
    }

    public class TagMappingService
    {
        public static TagMappingService Instance => _instance.Value;
        private static readonly Lazy<TagMappingService> _instance = new Lazy<TagMappingService>(() => new TagMappingService());
        public string GetTagId(string variableId) => null;
    }

    public class TagMigrationService
    {
        public class MigrationResult
        {
            public int VariableIdsGenerated;
            public int TagsCreated;
            public override string ToString() => "社区版：Tag 迁移已跳过";
        }
        public static MigrationResult Migrate(List<Models.DeviceConfig> devices)
            => new MigrationResult();
    }

    // ======================== Stub: Template 服务 ========================
    public class TemplateService
    {
        public static TemplateService Instance => _instance.Value;
        private static readonly Lazy<TemplateService> _instance = new Lazy<TemplateService>(() => new TemplateService());
        public List<Models.TemplateModels.TemplateInfo> GetAllTemplates() => new List<Models.TemplateModels.TemplateInfo>();
        public Models.DeviceConfig OverwriteFromDevice(string templateId, string deviceId)
            => new Models.DeviceConfig();
    }

    // ======================== Stub: Oracle/TDengine 适配器 ========================
    public class OracleAdapter : DbAdapters.IDbAdapter
    {
        public string AdapterType => "Oracle";
        public System.Data.IDbConnection CreateConnection(Models.DataSourceConnection source, string port) => null;
        public string GetListTablesSql() => "SELECT 1 FROM DUAL";
        public string GetDescribeTableSql(string tableName) => "SELECT 1 FROM DUAL";
    }

    public class TDengineAdapter : DbAdapters.IDbAdapter
    {
        public string AdapterType => "TDengine";
        public System.Data.IDbConnection CreateConnection(Models.DataSourceConnection source, string port) => new TdengineConnection();
        public string GetListTablesSql() => "SHOW STABLES";
        public string GetDescribeTableSql(string tableName) => $"DESCRIBE {tableName}";
    }

    public class TdengineConnection : System.Data.IDbConnection
    {
        public TdengineConnection() { }
        public TdengineConnection(string server, int port, string database, string user, string password) { }
        public string ConnectionString { get; set; }
        public int ConnectionTimeout => 30;
        public string Database => "";
        public System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => null;
        public System.Data.IDbTransaction BeginTransaction() => null;
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public System.Data.IDbCommand CreateCommand() => null;
        public void Open() { }
        public void Dispose() { }
    }
}

namespace IndustrialDataCollection.Models
{
    // ======================== Stub: 枚举类型 ========================
    public enum NodeStatus { Online, Offline, Stopped, Deleted }
    public static class SemanticEventType
    {
        public const string Start = "启动";
        public const string Stop = "停止";
        public const string Alarm = "报警";
        public const string Fault = "故障";
        public const string Recovery = "恢复";
        public const string ParamChange = "参数修改";
        public const string CommLost = "通讯中断";
        public const string Maintenance = "维护保养";
        public const string AIAnalysis = "AI分析";
    }

    // ======================== Stub: Tag 模型 ========================
    public class Tag
    {
        public string TagId { get; set; }
        public string TagCn { get; set; }
        public string VariableId { get; set; }
    }

    namespace TemplateModels
    {
        public class TemplateInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }
    }

    // Semantic models
    public class SemanticNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
    public class SemanticTag { }
    public class SemanticVariableEvent
    {
        public string NodeId { get; set; }
        public string EventType { get; set; }
        public string Description { get; set; }
        public string ProcessingMethod { get; set; }
    }
    public class SemanticVariableRelation { }
    public class FabricModels { }
    public class EventProcessingModels { }
}

namespace IndustrialDataCollection.Forms
{
    // ======================== Stub: 企业版窗体（社区版不可用） ========================
    internal static class CommunityEdition
    {
        public static void ShowNotAvailable(string feature)
        {
            MessageBox.Show($"ｫ{feature}ｻ为企业版功能。\n请访问 https://industrialdata.cn 了解详情。",
                "社区版限制", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    public class LoginForm : Form
    {
        public bool LoginSuccess => true;
        public LoginForm() { }
    }

    public class ActivationForm : Form
    {
        public bool ActivationSuccess => true;
        public ActivationForm() { }
    }

    public class SemanticManagementForm : Form
    {
        public SemanticManagementForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("语义管理");
        public void ForceClose() { }
        public void ApplyLanguage() { }
    }

    public class DashboardForm : Form
    {
        public DashboardForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("数据看板");
        public void ForceClose() { }
        public bool IsDisposed => true;
    }

    public class TemplateManagerForm : Form
    {
        public TemplateManagerForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("配置模板");
        public dynamic SelectedTemplate { get; set; }
    }
    public class TemplateApplyForm : Form
    {
        public TemplateApplyForm() { }
        public TemplateApplyForm(string deviceId, string deviceName) { }
        public new void Show() => CommunityEdition.ShowNotAvailable("模板应用");
    }
    public class TemplateGeneratorForm : Form
    {
        public TemplateGeneratorForm() { }
        public TemplateGeneratorForm(string deviceId, string deviceName, string driverType) { }
        public new void Show() => CommunityEdition.ShowNotAvailable("模板生成");
    }
    public class DeviceCloneForm : Form
    {
        public DeviceCloneForm(Models.DeviceConfig dev) { }
        public DeviceCloneForm(Models.DeviceConfig dev, bool isCollecting) { }
        public new void Show() => CommunityEdition.ShowNotAvailable("设备克隆");
        public dynamic Result => new { Success = false, NewDeviceId = "", NewDeviceName = "" };
    }
    public class SemanticNodePickerForm : Form
    {
        public SemanticNodePickerForm() { }
    }
    public class PointEditFormEdge : Form
    {
        public PointEditFormEdge() { }
    }
    public class PointEditForm_Edge : Form
    {
        public PointEditForm_Edge() { }
        public PointEditForm_Edge(Models.DataPoint point, Models.DeviceConfig parentDevice) { }
        public bool IsSaved => false;
        public Models.DataPoint DataPoint => null;
    }
    public class ApiServiceConfigForm : Form
    {
        public ApiServiceConfigForm(Services.RestApiService apiService) { }
        public ApiServiceConfigForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("REST API 服务配置");
    }

    public class TunnelEditDialog : Form
    {
        public TunnelEditDialog() { }
        public bool DialogResult_Ok => false;
        public dynamic SavedTunnel => new { Id = "", Name = "" };
    }

    public class NavigationHelper
    {
        public static NavigationHelper Instance => _instance.Value;
        private static readonly Lazy<NavigationHelper> _instance = new Lazy<NavigationHelper>(() => new NavigationHelper());
        public static DashboardForm Dashboard { get; set; }
        public SemanticManagementForm Semantic { get; set; }
        public static void NavigateToDashboard() => CommunityEdition.ShowNotAvailable("数据看板");
        public void NavigateToSemantic() => CommunityEdition.ShowNotAvailable("语义管理");
        public void NavigateToMainForm() { }
        public static void ShowOrCreateMainForm() { }
    }
}

namespace IndustrialDataCollection.Services.Mcp
{
    public class McpFabricEngine
    {
        public static McpFabricEngine Instance => _instance.Value;
        private static readonly Lazy<McpFabricEngine> _instance = new Lazy<McpFabricEngine>(() => new McpFabricEngine());
        public Task<object> ExecuteFabricAsync(string operatorType, JObject args)
            => Task.FromResult<object>(new { error = "Fabric 引擎为企业版功能" });
    }

    public class EventProcessingService
    {
        public static EventProcessingService Instance => _instance.Value;
        private static readonly Lazy<EventProcessingService> _instance = new Lazy<EventProcessingService>(() => new EventProcessingService());
        public void Init() { }
        public void Process(Models.SemanticVariableEvent evt) { }
    }
}

namespace IndustrialDataCollection.Services.Sinks
{
    public interface ISink
    {
        Task WriteAsync(Models.DataPacket batch);
    }
    public class MqttSink : ISink
    {
        public Task WriteAsync(Models.DataPacket batch) => Task.CompletedTask;
    }
    public class DatabaseSink : ISink
    {
        public Task WriteAsync(Models.DataPacket batch) => Task.CompletedTask;
    }
}

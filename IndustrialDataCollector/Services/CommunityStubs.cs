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
    }

    public class LicenseService
    {
        public static LicenseService Instance => _instance.Value;
        private static readonly Lazy<LicenseService> _instance = new Lazy<LicenseService>(() => new LicenseService());
        public bool IsActivated() => true;
    }

    public class ApplicationLifecycle
    {
        public static ApplicationLifecycle Instance => _instance.Value;
        private static readonly Lazy<ApplicationLifecycle> _instance = new Lazy<ApplicationLifecycle>(() => new ApplicationLifecycle());
        public void Init() { }
    }

    // ======================== Stub: 语义层（企业版专属） ========================
    public class SemanticService
    {
        public static SemanticService Instance => _instance.Value;
        private static readonly Lazy<SemanticService> _instance = new Lazy<SemanticService>(() => new SemanticService());
        public void Init() { }
        public void SyncFromDeviceConfigs() { }
    }

    // ======================== Stub: 离线缓存 ========================
    public class OfflineCacheService
    {
        public static OfflineCacheService Instance => _instance.Value;
        private static readonly Lazy<OfflineCacheService> _instance = new Lazy<OfflineCacheService>(() => new OfflineCacheService());
        public void Init() { }
        public void Start() { }
        public void Stop() { }
        public void WriteHeartbeat() { }
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
    }
}

namespace IndustrialDataCollection.Models
{
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
    public class SemanticVariableEvent { }
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
            MessageBox.Show($"「{feature}」为企业版功能。\n请访问 https://industrialdata.cn 了解详情。",
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
    }
    public class TemplateApplyForm : Form
    {
        public TemplateApplyForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("模板应用");
    }
    public class TemplateGeneratorForm : Form
    {
        public TemplateGeneratorForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("模板生成");
    }
    public class DeviceCloneForm : Form
    {
        public DeviceCloneForm(Models.DeviceConfig dev) { }
        public new void Show() => CommunityEdition.ShowNotAvailable("设备克隆");
    }
    public class SemanticNodePickerForm : Form
    {
        public SemanticNodePickerForm() { }
    }
    public class PointEditFormEdge : Form
    {
        public PointEditFormEdge() { }
    }
    public class ApiServiceConfigForm : Form
    {
        public ApiServiceConfigForm() { }
        public new void Show() => CommunityEdition.ShowNotAvailable("REST API 服务配置");
    }

    public class NavigationHelper
    {
        public static NavigationHelper Instance => _instance.Value;
        private static readonly Lazy<NavigationHelper> _instance = new Lazy<NavigationHelper>(() => new NavigationHelper());
        public DashboardForm Dashboard { get; set; }
        public SemanticManagementForm Semantic { get; set; }
        public void NavigateToDashboard() => CommunityEdition.ShowNotAvailable("数据看板");
        public void NavigateToSemantic() => CommunityEdition.ShowNotAvailable("语义管理");
        public void NavigateToMainForm() { }
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

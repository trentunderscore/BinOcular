using BinOcular.Api.Analysis;
using BinOcular.Api.Services;
using BinOcular.Api.Tools;

namespace BinOcular.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the comprehensive binary analysis tools and their structured formatters.
    /// </summary>
    public static IServiceCollection AddAnalysisPipeline(this IServiceCollection services)
    {
        // Core Runner
        services.AddTransient<ToolRunner>();

        // Formatters
        services.AddTransient<IToolResultFormatter, FileFormatFormatter>();
        services.AddTransient<IToolResultFormatter, CryptoHashesFormatter>();
        services.AddTransient<IToolResultFormatter, Sha256Formatter>();
        services.AddTransient<IToolResultFormatter, BinarySectionsFormatter>();
        services.AddTransient<IToolResultFormatter, CompilerAndTimestampFormatter>();
        services.AddTransient<IToolResultFormatter, ImportsExportsFormatter>();
        services.AddTransient<IToolResultFormatter, StringsFormatter>();
        services.AddTransient<IToolResultFormatter, CryptoScannerFormatter>();
        services.AddTransient<IToolResultFormatter, CertificateAndSecretsFormatter>();
        services.AddTransient<IToolResultFormatter, EmbeddedAssetsFormatter>();
        services.AddTransient<IToolResultFormatter, OpcodePatternFormatter>();
        services.AddTransient<IToolResultFormatter, NetworkEndpointFormatter>();
        services.AddTransient<IToolResultFormatter, MitreAttackFormatter>();
        services.AddTransient<IToolResultFormatter, HeuristicRiskScorerFormatter>();
        services.AddTransient<IToolResultFormatter, DIEToolResultFormatter>();

        // Tools (executed sequentially in pipeline)
        services.AddTransient<IAnalysisTool, FileFormatTool>();
        services.AddTransient<IAnalysisTool, CryptoHashesTool>();
        services.AddTransient<IAnalysisTool, Sha256Tool>();
        services.AddTransient<IAnalysisTool, BinarySectionsTool>();
        services.AddTransient<IAnalysisTool, CompilerAndTimestampTool>();
        services.AddTransient<IAnalysisTool, ImportsExportsTool>();
        services.AddTransient<IAnalysisTool, StringsTool>();
        services.AddTransient<IAnalysisTool, CryptoScannerTool>();
        services.AddTransient<IAnalysisTool, CertificateAndSecretsTool>();
        services.AddTransient<IAnalysisTool, EmbeddedAssetsTool>();
        services.AddTransient<IAnalysisTool, OpcodePatternTool>();
        services.AddTransient<IAnalysisTool, NetworkEndpointTool>();
        services.AddTransient<IAnalysisTool, MitreAttackMapperTool>();
        services.AddTransient<IAnalysisTool, HeuristicRiskScorerTool>();
        services.AddTransient<IAnalysisTool, DIETool>();

        return services;
    }
}

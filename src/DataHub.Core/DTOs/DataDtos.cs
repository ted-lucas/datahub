namespace DataHub.Core.DTOs.Data;

public record CreateDataSourceRequest(string Name, string Type, string? Description, string? ConfigJson);
public record UpdateDataSourceRequest(string Name, string Type, string? Description, string? ConfigJson);

public record CreateDataEntryRequest(
    Guid? DataSourceId,
    string Category,
    string? Tags,
    string PayloadJson
);

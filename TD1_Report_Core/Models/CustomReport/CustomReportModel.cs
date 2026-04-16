namespace TD1_Report_Core.Models.CustomReport;

#region 資料表定義

/// <summary>
/// ProcedureConfig 主表
/// </summary>
public class ProcedureConfig
{
    /// <summary> 主鍵 Id </summary>
    public long? Id { get; set; } = null;

    /// <summary> 使用者 Id </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary> Schema 名稱 </summary>
    public string SchemaName { get; set; } = string.Empty;

    /// <summary> Procedure 名稱 </summary>
    public string ProcedureName { get; set; } = string.Empty;

    /// <summary> 顯示名稱 </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> 簡稱 </summary>
    public string? ShortName { get; set; } = null;

    /// <summary> 是否啟用(預留欄位,暫不使用) </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// ProcedureParameterConfig 明細表
/// </summary>
public class ProcedureParameterConfig
{
    /// <summary> 主鍵 Id </summary>
    public long Id { get; set; }
    /// <summary> 對應 ProcedureConfig Id </summary>
    public long? ProcedureConfigId { get; set; }
    /// <summary> 參數名稱 </summary>
    public string ParameterName { get; set; } = string.Empty;
    /// <summary> 參數型別 </summary>
    public string? DataType { get; set; }
    /// <summary> Procedure 預設值 </summary>
    public string? ProcedureDefaultValue { get; set; }
    /// <summary> 是否允許 NULL </summary>
    public bool IsNullable { get; set; }
    /// <summary> 自訂預設值 </summary>
    public string? CustomDefaultValue { get; set; }
    /// <summary> 顯示名稱 </summary>
    public string? DisplayName { get; set; }
    /// <summary> 建立時間 </summary>
    public DateTime CreateAt { get; set; }
}

#endregion

/// <summary>
/// 自定義Procedure
/// </summary>
public class CustomProcedure
{
    /// <summary> Procedure名稱 </summary>
    public string? ProcedureName { get; set; }

    ///<summary> Procedure使用Schema </summary>
    public string? SchemaName { get; set; }
}


/// <summary>
/// Stored Procedure 原始參數資訊
/// </summary>
public class ProcedureOriginalParameter
{
    /// <summary> 參數名稱 </summary>
    public string ParameterName { get; set; } = string.Empty;
    /// <summary> MSSQL 原始型別 </summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary> 最大長度 </summary>
    public short MaxLength { get; set; }
    /// <summary> 精度 </summary>
    public byte Precision { get; set; }
    /// <summary> 小數位數 </summary>
    public byte Scale { get; set; }
}

/// <summary>
/// Stored Procedure 參數資訊 (包含預設值與是否為輸出參數)
/// </summary>
public class ProcedureParameterInfo
{
    /// <summary> 參數名稱 </summary>
    public string ParameterName { get; set; } = string.Empty;
    /// <summary> MSSQL 原始型別 </summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary> 預設值 </summary>
    public string? DefaultValue { get; set; }
    /// <summary> 是否為輸出參數 </summary>
    public bool IsOutput { get; set; }
}

/// <summary> 
/// Stored Procedure 參數資訊 
/// </summary>
public class ProcedureParameter
{
    /// <summary> 參數名稱 </summary>
    public string ParameterName { get; set; } = string.Empty;
    /// <summary> MSSQL 顯示型別 </summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary> 預設值 </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Procedure Config 清單
/// </summary>
public class ProcedureConfigList
{
    /// <summary> Config Id </summary>
    public long Id { get; set; }

    /// <summary> Procedure 名稱 </summary>
    public string ProcedureName { get; set; } = string.Empty;
    /// <summary> 顯示名稱 </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary> 簡稱 </summary>
    public string? ShortName { get; set; } = null;
}


/// <summary>
/// Procedure Config 明細
/// </summary>
public class ProcedureConfigDetail
{
    /// <summary> Config Id </summary>
    public long Id { get; set; }
    /// <summary> Procedure 名稱 </summary>
    public string ProcedureName { get; set; } = string.Empty;
    /// <summary> 顯示名稱 </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary> 簡稱 </summary>
    public string? ShortName { get; set; } = null;
    /// <summary> 參數細節 </summary>
    public List<ProcedureConfigParameterItem> Parameters { get; set; } = new();
}

/// <summary>
/// Procedure Config 參數項目
/// </summary>
public class ProcedureConfigParameterItem
{
    /// <summary> 參數名稱</summary>
    public string ParameterName { get; set; } = string.Empty;
    /// <summary> 參數型別 </summary>
    public string? DataType { get; set; }
    /// <summary> Procedure 預設值 </summary>
    public string? ProcedureDefaultValue { get; set; }
    /// <summary>  是否允許 NULL</summary>
    public bool IsNullable { get; set; }
    /// <summary> 自訂預設值 </summary>
    public string? CustomDefaultValue { get; set; }
    /// <summary> 顯示名稱 </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// 已設定好的報表清單項目
/// </summary>
public class ProcedureReports
{
    /// <summary> ProcedureConfig Id </summary>
    public long Id { get; set; }

    /// <summary> 報表名稱 </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 報表參數顯示資訊
/// </summary>
public class ProcedureReportParameter
{
    /// <summary> 參數名稱 </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary> 顯示名稱 </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary> 型別 </summary>
    public string? DataType { get; set; }

    /// <summary> 是否可為空 </summary>
    public bool IsNullable { get; set; }

    /// <summary> 顯示值 </summary>
    public string? Value { get; set; }
}

/// <summary>
/// Stored Procedure 執行結果
/// </summary>
public class ProcedureExecutionResult
{
    /// <summary> 欄位名稱清單 </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary> 資料列 </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
}

/// <summary>
/// 報表預覽結果
/// </summary>
public class ProcedureReportResult
{
    /// <summary> 報表名稱 </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary> 報表副標題 </summary>
    public string? ShortName { get; set; }

    /// <summary> 報表參數 </summary>
    public List<ProcedureReportParameter> Parameters { get; set; } = new();

    /// <summary> SP 回傳欄位名稱 </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary> SP 回傳資料 </summary>
    public List<Dictionary<string, object?>> Data { get; set; } = new();
}

/// <summary>
/// Procedure 參數解析結果
/// </summary>
public class ResolvedParameter
{
    /// <summary> 是否要帶入 SP </summary>
    public bool ShouldBind { get; set; }

    /// <summary> 實際值 </summary>
    public object? Value { get; set; }
}
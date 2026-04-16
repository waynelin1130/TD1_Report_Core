using System.Dynamic;
using TD1_DB.Database;
using TD1_Report_Core.Core.CustomReport;
using TD1_Report_Core.Models.CustomReport;

namespace TD1_Report_Core.Core.ProcedureExecute;

/// <summary>
/// Stored Procedure 執行核心
/// </summary>
public class ProcedureExecutionCore
{
    /// <summary>
    /// 資料庫連接
    /// </summary>
    private readonly Database _database;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="database">資料庫連接</param>
    public ProcedureExecutionCore(Database database)
    {
        _database = database;
    }

    /// <summary>
    /// 執行指定 Stored Procedure
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <param name="parameters">Procedure 參數設定</param>
    /// <returns>執行結果</returns>
    public ProcedureExecutionResult ExecuteProcedure(string schemaName, string procedureName, List<ProcedureParameterConfig> parameters)
    {
        string fullProcedureName = BuildFullProcedureName(schemaName, procedureName);

        List<string> sqlParameterAssignments = new();
        IDictionary<string, object?> dbParameters = new ExpandoObject();

        foreach (ProcedureParameterConfig parameter in parameters)
        {
            ResolvedParameter resolved = ResolveProcedureParameterValue(parameter);

            // 使用者未提供值，就不帶這個參數
            if (!resolved.ShouldBind)
            {
                continue;
            }

            string nameWithoutAt = parameter.ParameterName.TrimStart('@');
            string placeholder = $"@{nameWithoutAt}";

            sqlParameterAssignments.Add($"{parameter.ParameterName} = {placeholder}");
            dbParameters[nameWithoutAt] = resolved.Value;
        }

        string sql = sqlParameterAssignments.Count == 0
            ? $"EXEC {fullProcedureName};"
            : $"EXEC {fullProcedureName} {string.Join(", ", sqlParameterAssignments)};";

        List<dynamic> rawRows = _database.Query(sql, dbParameters).ToList();

        List<Dictionary<string, object?>> rows = rawRows
            .Select(ConvertDynamicRowToDictionary)
            .ToList();

        List<string> columns = rows.FirstOrDefault()?.Keys.ToList() ?? new();

        return new ProcedureExecutionResult
        {
            Columns = columns,
            Rows = rows
        };
    }

    /// <summary>
    /// 將 Dapper dynamic row 轉為 Dictionary
    /// </summary>
    /// <param name="row">資料列</param>
    /// <returns>Dictionary 格式資料列</returns>
    private static Dictionary<string, object?> ConvertDynamicRowToDictionary(dynamic row)
    {
        if (row is IDictionary<string, object?> nullableDictionary)
        {
            return nullableDictionary.ToDictionary(
            item => item.Key,
            // 報表特殊邏輯：如果是 DateTime 就轉成 "yyyy-MM-dd" 字串，避免前端格式化顯示問題
            item => item.Value is DateTime dt ? dt.ToString("yyyy-MM-dd")
         : item.Value);
        }

        if (row is IDictionary<string, object> dictionary)
        {
            return dictionary.ToDictionary(
                item => item.Key,
                item => (object?)item.Value);
        }

        throw new Exception("無法將查詢結果轉換為 Dictionary<string, object?>");
    }

    /// <summary>
    /// 建立完整的 Procedure 名稱
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>完整名稱</returns>
    private static string BuildFullProcedureName(string schemaName, string procedureName)
    {
        return $"[{schemaName}].[{procedureName}]";
    }

    /// <summary>
    /// 解析 Procedure 參數值
    /// 規則：
    /// 1. CustomDefaultValue 優先
    /// 2. null / 空字串 / "NULL" => 不帶參數
    /// 3. "__NULL__" => 傳 DBNull.Value
    /// 4. "__Today__" => 傳今天
    /// </summary>
    private static ResolvedParameter ResolveProcedureParameterValue(ProcedureParameterConfig parameter)
    {
        string? rawValue = !string.IsNullOrWhiteSpace(parameter.CustomDefaultValue)
            ? parameter.CustomDefaultValue
            : parameter.ProcedureDefaultValue;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new ResolvedParameter { ShouldBind = false };
        }

        string value = NormalizeDefaultValue(rawValue);

        if (string.IsNullOrWhiteSpace(value))
        {
            return new ResolvedParameter { ShouldBind = false };
        }

        // 視為不帶參數
        if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedParameter { ShouldBind = false };
        }

        // 支援特殊關鍵字 轉成特定預設值
        string keyword = value.ToUpperInvariant();

        if (CustomReportConstants.SupportedKeywords.Contains(keyword))
        {
            switch (keyword)
            {
                // TODO: 如果之後有更多特殊關鍵字，可以在這裡擴充
                case CustomReportConstants.TodayKeyword:
                    return new ResolvedParameter
                    {
                        ShouldBind = true,
                        Value = DateTime.Today
                    };
            }
        }

        return new ResolvedParameter
        {
            ShouldBind = true,
            Value = value
        };
    }

    /// <summary>
    /// 標準化預設值，去除 N'xxx' 或 'xxx' 的包裹
    /// </summary>
    /// <param name="value">原始值</param>
    /// <returns>標準化後的值</returns>
    private static string NormalizeDefaultValue(string value)
    {
        value = value.Trim();

        if (value.Length >= 3 &&
            value.StartsWith("N'", StringComparison.OrdinalIgnoreCase) &&
            value.EndsWith("'"))
        {
            string content = value.Substring(2, value.Length - 3);
            return content.Replace("''", "'");
        }

        if (value.Length >= 2 &&
            value.StartsWith("'") &&
            value.EndsWith("'"))
        {
            string content = value.Substring(1, value.Length - 2);
            return content.Replace("''", "'");
        }

        return value;
    }
}
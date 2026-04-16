using System.Data;
using System.Text.RegularExpressions;
using TD1_DB.Database;
using TD1_Report_Core.Core.ProcedureExecute;
using TD1_Report_Core.CustomReport;
using TD1_Report_Core.Models.CustomReport;

namespace TD1_Report_Core.Core.CustomReport;

/// <summary>
/// 自定義報表核心類別
/// </summary>
public class CustomReportCore
{
    /// <summary>
    /// 資料庫連接
    /// </summary>
    private readonly Database _database;

    /// <summary>
    /// Stored Procedure 執行核心
    /// </summary>
    private readonly ProcedureExecutionCore _procedureExecutionCore;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="database">資料庫連接</param>
    public CustomReportCore(Database database)
    {
        _database = database;
        _procedureExecutionCore = new ProcedureExecutionCore(database);
    }

    /// <summary>
    /// 取得指定 Schema 下的所有 Stored Procedure 的自定義報表清單
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <returns>自定義報表列表</returns>
    public List<CustomProcedure> GetCustomProcedures(string schemaName)
    {
        string sql = @"
        SELECT
            p.name AS ProcedureName,
            s.name AS SchemaName
        FROM sys.procedures p
        JOIN sys.schemas s ON p.schema_id = s.schema_id
        WHERE s.name = @SchemaName
        ORDER BY p.create_date DESC";

        return _database.Query<CustomProcedure>(sql, new { SchemaName = schemaName }).ToList();
    }

    /// <summary>
    /// 取得指定 Schema 下「尚未設定 ProcedureConfig」的 Stored Procedure 清單
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="userId">使用者 ID(預留)</param>
    /// <returns>可設定的 Procedure 清單</returns>
    public List<CustomProcedure> GetAvailableProcedures(string schemaName, string userId = "")
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";

        string sql = $@"
        SELECT
            p.name AS ProcedureName,
            s.name AS SchemaName
        FROM sys.procedures p
        INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
        WHERE s.name = @SchemaName
        AND NOT EXISTS
        (
            SELECT 1
            FROM {procedureConfigTable} pc
            WHERE pc.SchemaName = s.name
                AND pc.ProcedureName = p.name
                AND pc.UserId = @UserId
        )
        ORDER BY p.create_date DESC";

        return _database.Query<CustomProcedure>(
            sql,
            new { SchemaName = schemaName, UserId = userId }
        ).ToList();
    }

    /// <summary>
    /// 取得指定 Procedure 的 SQL 內容
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>Procedure 內容</returns>
    public string? GetProcedureContent(string schemaName, string procedureName)
    {
        string sql = @"
        SELECT 
            m.definition AS Definition
        FROM sys.procedures p
        JOIN sys.schemas s ON p.schema_id = s.schema_id
        JOIN sys.sql_modules m ON p.object_id = m.object_id
        WHERE s.name = @SchemaName AND p.name = @ProcedureName";

        string? content = _database.Query<string>(sql, new
        {
            SchemaName = schemaName,
            ProcedureName = procedureName
        }).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return NormalizeProcedureContent(schemaName, procedureName, content);
    }

    /// <summary>
    /// 刪除 Procedure
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    public void DeleteProcedure(string schemaName, string procedureName)
    {
        string fullProcedureName = BuildFullProcedureName(schemaName, procedureName);

        _database.ExecuteInTransaction(tx =>
        {
            DeleteProcedureConfigByProcedure(tx, schemaName, procedureName);

            string sql = $@"DROP PROCEDURE {fullProcedureName};";
            tx.Execute(sql);
        });
    }

    /// <summary>
    /// 儲存 Procedure 內容，並清除對應的 Procedure Config 與參數設定
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <param name="procedureContent">Procedure SQL 內容</param>
    public void SaveProcedureContent(string schemaName, string procedureName, string procedureContent)
    {
        if (!ValidateProcedureName(schemaName))
        {
            throw new ArgumentException("Invalid schema name.", nameof(schemaName));
        }

        if (!ValidateProcedureName(procedureName))
        {
            throw new ArgumentException("Invalid procedure name.", nameof(procedureName));
        }

        if (!IsValidProcedureContent(procedureContent))
        {
            throw new ArgumentException(
                "Procedure content must include a valid CREATE/ALTER PROCEDURE header.",
                nameof(procedureContent));
        }

        if (!IsProcedureHeaderMatch(schemaName, procedureName, procedureContent))
        {
            throw new ArgumentException(
                "Procedure header name does not match procedureName.",
                nameof(procedureContent));
        }

        string normalizedContent = NormalizeProcedureContent(schemaName, procedureName, procedureContent);

        _database.ExecuteInTransaction(tx =>
        {
            tx.Execute(normalizedContent);

            DeleteProcedureConfigByProcedure(tx, schemaName, procedureName);
        });
    }

    /// <summary>
    /// 檢查 Procedure 是否存在
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>是否存在</returns>
    public bool IsProcedureExists(string schemaName, string procedureName)
    {
        string sql = @"
        SELECT
            COUNT(1)
        FROM sys.procedures p
        JOIN sys.schemas s ON p.schema_id = s.schema_id
        WHERE s.name = @SchemaName AND p.name = @ProcedureName";

        int count = _database.Query<int>(sql, new
        {
            SchemaName = schemaName,
            ProcedureName = procedureName
        }).FirstOrDefault();

        return count > 0;
    }

    /// <summary>
    /// 建立完整的 Procedure 名稱
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>完整名稱</returns>
    public static string BuildFullProcedureName(string schemaName, string procedureName)
    {
        return $"[{schemaName}].[{procedureName}]";
    }

    /// <summary>
    /// 驗證 SQL 物件名稱 (如 Procedure 名稱) 是否有效
    /// </summary>
    /// <param name="value">物件名稱</param>
    /// <returns>是否有效</returns>
    public static bool ValidateProcedureName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // 僅允許 英文 / 數字 / _
        // 不可開頭數字
        // 長度限制 1~128 (SQL Server object name)
        const string pattern = @"^[A-Za-z_][A-Za-z0-9_]{0,127}$";

        if (!Regex.IsMatch(value, pattern))
        {
            return false;
        }

        // 避免 SQL keyword
        string[] keywords = { "SELECT", "DROP", "DELETE", "UPDATE", "INSERT" };

        return !keywords.Contains(value.ToUpper());
    }

    /// <summary>
    /// 檢查 Procedure Content 的 Header 是否與指定 schemaName / procedureName 一致
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <param name="procedureContent">Procedure SQL 內容</param>
    /// <returns>是否一致</returns>
    public static bool IsProcedureHeaderMatch(string schemaName, string procedureName, string procedureContent)
    {
        if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(procedureName) || string.IsNullOrWhiteSpace(procedureContent))
        {
            return false;
        }

        procedureContent = procedureContent.TrimStart('\uFEFF');

        const string pattern = @"
            ^\s*
            (?:
                --[^\r\n]*\r?\n\s* |
                /\*.*?\*/\s*
            )*
            (CREATE\s+OR\s+ALTER|CREATE|ALTER)
            \s+PROCEDURE\s+
            (?:
                \[?(?<schema>[A-Za-z0-9_]+)\]?\s*\.\s*
            )?
            \[?(?<procedure>[A-Za-z0-9_]+)\]?
        ";

        Match match = Regex.Match(
            procedureContent,
            pattern,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            return false;
        }

        string extractedSchema = match.Groups["schema"].Value;
        string extractedProcedure = match.Groups["procedure"].Value;

        if (string.IsNullOrWhiteSpace(extractedSchema) || string.IsNullOrWhiteSpace(extractedProcedure))
        {
            return false;
        }

        return string.Equals(extractedSchema, schemaName, StringComparison.OrdinalIgnoreCase) && string.Equals(extractedProcedure, procedureName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///  驗證 Procedure 內容是否包含有效的 CREATE/ALTER PROCEDURE header
    /// </summary>
    /// <param name="procedureContent">Procedure SQL 內容</param>
    /// <returns>是否有效</returns>
    public static bool IsValidProcedureContent(string procedureContent)
    {
        if (string.IsNullOrWhiteSpace(procedureContent))
        {
            return false;
        }

        procedureContent = procedureContent.TrimStart('\uFEFF');

        const string pattern = @"
            ^\s*
            (?:
                --[^\r\n]*\r?\n\s* |
                /\*.*?\*/\s*
            )*
            (CREATE\s+OR\s+ALTER|CREATE|ALTER)
            \s+PROCEDURE\s+
            (?:
                \[?[A-Za-z0-9_]+\]?\s*\.\s*
            )?
            \[?[A-Za-z0-9_]+\]?
            \s*
            (?:\([^)]*\))?
            .*?
            \bAS\b
        ";

        return Regex.IsMatch(
            procedureContent,
            pattern,
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline |
            RegexOptions.IgnorePatternWhitespace,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 取得指定 Procedure 的input 參數資訊（包含預設值與顯示型別）
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>Procedure 參數詳細資訊列表</returns>
    public List<ProcedureParameter> GetProcedureParameterDetails(string schemaName, string procedureName)
    {
        List<ProcedureOriginalParameter> dbParameters = GetProcedureParameters(schemaName, procedureName);

        string? procedureContent = GetProcedureContent(schemaName, procedureName);

        List<ProcedureParameterInfo> parsedParameters = string.IsNullOrWhiteSpace(procedureContent)
        ? new List<ProcedureParameterInfo>()
        : ProcedureParameterParser
            .ParseParameters(procedureContent)
            .Where(parameter => !parameter.IsOutput)
            .ToList();

        return MapProcedureParameters(dbParameters, parsedParameters);
    }

    /// <summary>
    /// 取得單筆 Procedure Config 明細
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="id">ProcedureConfig 主表 Id</param>
    public ProcedureConfigDetail? GetProcedureConfigDetail(string schemaName, long id)
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";
        string parameterConfigTable = $"[{schemaName}].[ProcedureParameterConfig]";

        string configSql = $@"
        SELECT
            Id,
            ProcedureName,
            Name,
            ShortName
        FROM {procedureConfigTable}
        WHERE Id = @Id;";

        ProcedureConfigDetail? config = _database.FirstOrDefault<ProcedureConfigDetail>(configSql, new { Id = id });

        if (config == null)
        {
            return null;
        }

        string paramSql = $@"
        SELECT
            ParameterName,
            DataType,
            ProcedureDefaultValue,
            IsNullable,
            CustomDefaultValue,
            DisplayName
        FROM {parameterConfigTable}
        WHERE ProcedureConfigId = @Id
        ORDER BY Id;";

        List<ProcedureConfigParameterItem> parameters = _database
            .Query<ProcedureConfigParameterItem>(paramSql, new { Id = id })
            .ToList();

        config.Parameters = parameters;

        return config;
    }

    /// <summary>
    /// 取得指定 Schema 下已儲存，且對應 Stored Procedure 仍存在的 Procedure Config 清單
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="userId">使用者 ID（預留）</param>
    /// <returns>Procedure Config 清單</returns>
    public List<ProcedureConfigList> GetProcedureConfigs(string schemaName, string userId = "")
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";

        string sql = $@"
        SELECT
            pc.Id,
            pc.ProcedureName,
            pc.Name,
            pc.ShortName
        FROM {procedureConfigTable} pc
        WHERE pc.SchemaName = @SchemaName
        AND pc.UserId = @UserId
        AND EXISTS
        (
            SELECT 1
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            WHERE s.name = pc.SchemaName AND p.name = pc.ProcedureName
        )
        ORDER BY pc.Id DESC";

        return _database.Query<ProcedureConfigList>(
            sql,
            new { SchemaName = schemaName, UserId = userId }
        ).ToList();
    }

    /// <summary>
    /// 儲存 ProcedureConfig（新增 / 更新）
    /// </summary>
    /// <param name="config">ProcedureConfig 主表資料</param>
    /// <param name="parameters">ProcedureParameterConfig 明細資料</param>
    /// <returns>儲存後的 ProcedureConfig 主表 Id</returns>
    public long SaveProcedureConfig(ProcedureConfig config, List<ProcedureParameterConfig>? parameters)
    {
        string procedureConfigTable = $"[{config.SchemaName}].[ProcedureConfig]";
        string parameterConfigTable = $"[{config.SchemaName}].[ProcedureParameterConfig]";

        parameters ??= new List<ProcedureParameterConfig>();

        return _database.ExecuteInTransaction(tx =>
        {
            long configId;

            if (config.Id == null)
            {
                string insertSql = $@"
            INSERT INTO {procedureConfigTable}
            (
                UserId,
                SchemaName,
                ProcedureName,
                Name,
                ShortName,
                IsEnabled
            )
            VALUES
            (
                @UserId,
                @SchemaName,
                @ProcedureName,
                @Name,
                @ShortName,
                @IsEnabled
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                configId = tx.ExecuteScalar<long>(insertSql, config);
            }
            else
            {
                configId = UpdateProcedureConfig(tx, procedureConfigTable, config);
            }

            ReplaceProcedureParameterConfig(tx, parameterConfigTable, configId, parameters);

            return configId;
        });
    }

    /// <summary>
    /// 刪除指定 Procedure Config（含參數）
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="id">ProcedureConfig 主表 Id</param>
    /// <param name="userId">使用者 ID(預留)</param>
    public void DeleteProcedureConfig(string schemaName, long id, string userId = "")
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";
        string parameterConfigTable = $"[{schemaName}].[ProcedureParameterConfig]";

        _database.ExecuteInTransaction(tx =>
        {
            string deleteParamSql = $@"
            DELETE FROM {parameterConfigTable}
            WHERE ProcedureConfigId = @Id;";

            tx.Execute(deleteParamSql, new { Id = id });

            string deleteConfigSql = $@"
            DELETE FROM {procedureConfigTable}
            WHERE Id = @Id
            AND UserId = @UserId;";

            int affectedRows = tx.Execute(deleteConfigSql, new
            {
                Id = id,
                UserId = userId
            });

            if (affectedRows == 0)
            {
                throw new Exception("ProcedureConfig 主表 不存在或無權限刪除");
            }
        });
    }

    /// <summary>
    /// 取得指定 Schema 下已設定好的報表清單（僅包含對應 SP 仍存在者）
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="userId">使用者 ID(預留)</param>
    /// <returns>報表清單(Id + Name)</returns>
    public List<ProcedureReports> GetConfigReports(string schemaName, string userId = "")
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";

        string sql = $@"
        SELECT
            pc.Id,
            pc.Name
        FROM {procedureConfigTable} pc
        WHERE pc.SchemaName = @SchemaName
            AND pc.UserId = @UserId
            AND pc.IsEnabled = @IsEnabled
            AND EXISTS
            (
                SELECT 1
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
                WHERE s.name = pc.SchemaName AND p.name = pc.ProcedureName
            )
        ORDER BY pc.Id DESC";

        return _database.Query<ProcedureReports>(
            sql,
            new
            {
                SchemaName = schemaName,
                UserId = userId,
                IsEnabled = true
            }).ToList();
    }

    /// <summary>
    /// 根據 Procedure Config Id 讀取設定並以預設值執行對應報表
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureConfigId">ProcedureConfig Id</param>
    /// <param name="userId">使用者 ID(預留)</param>
    /// <returns>報表預覽結果</returns>
    public ProcedureReportResult ExecuteProcedureConfigReport(string schemaName, long procedureConfigId, string userId = "")
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";
        string parameterConfigTable = $"[{schemaName}].[ProcedureParameterConfig]";

        string configSql = $@"
        SELECT
            Id,
            SchemaName,
            ProcedureName,
            Name,
            ShortName,
            IsEnabled,
            UserId
        FROM {procedureConfigTable}
        WHERE Id = @Id
            AND SchemaName = @SchemaName
            AND UserId = @UserId
            AND IsEnabled = @IsEnabled;";

        ProcedureConfig? config = _database.FirstOrDefault<ProcedureConfig>(
            configSql,
            new
            {
                Id = procedureConfigId,
                SchemaName = schemaName,
                UserId = userId,
                IsEnabled = true
            });

        if (config == null)
        {
            throw new Exception("找不到對應的 ProcedureConfig");
        }

        string parameterSql = $@"
        SELECT
            Id,
            ProcedureConfigId,
            ParameterName,
            DataType,
            ProcedureDefaultValue,
            IsNullable,
            CustomDefaultValue,
            DisplayName,
            CreateAt
        FROM {parameterConfigTable}
        WHERE ProcedureConfigId = @ProcedureConfigId
        ORDER BY Id;";

        List<ProcedureParameterConfig> parameterConfigs = _database
            .Query<ProcedureParameterConfig>(
                parameterSql,
                new { ProcedureConfigId = procedureConfigId })
            .ToList();

        List<ProcedureReportParameter> reportParameters = parameterConfigs
            .Select(parameter => new ProcedureReportParameter
            {
                ParameterName = parameter.ParameterName,
                DisplayName = GetParameterDisplayName(parameter),
                DataType = parameter.DataType,
                IsNullable = parameter.IsNullable,
                Value = ResolveExecutionValue(parameter)
            })
            .ToList();

        ProcedureExecutionResult executionResult = _procedureExecutionCore.ExecuteProcedure(
            config.SchemaName,
            config.ProcedureName,
            parameterConfigs);

        return new ProcedureReportResult
        {
            Name = config.Name,
            ShortName = config.ShortName,
            Parameters = reportParameters,
            Columns = executionResult.Columns,
            Data = executionResult.Rows
        };
    }

    /// <summary>
    /// 更新 ProcedureConfig 主檔
    /// </summary>
    private long UpdateProcedureConfig(DatabaseTransaction tx, string procedureConfigTable, ProcedureConfig config)
    {
        string updateSql = $@"
        UPDATE {procedureConfigTable}
        SET
            Name = @Name,
            ShortName = @ShortName,
            IsEnabled = @IsEnabled
        WHERE Id = @Id
        AND UserId = @UserId;";

        int affectedRows = tx.Execute(updateSql, config);

        if (affectedRows == 0)
        {
            throw new Exception("ProcedureConfig 不存在或無權限更新");
        }

        if (config.Id == null)
        {
            throw new Exception("更新 ProcedureConfig 時 Id 不可為 null");
        }

        return config.Id.Value;
    }

    /// <summary>
    /// 重新建立 ProcedureParameterConfig（先刪後新增）
    /// </summary>
    private void ReplaceProcedureParameterConfig(DatabaseTransaction tx, string parameterConfigTable, long configId, List<ProcedureParameterConfig> parameters)
    {
        string deleteSql = $@"
        DELETE FROM {parameterConfigTable}
        WHERE ProcedureConfigId = @ProcedureConfigId;";

        tx.Execute(deleteSql, new { ProcedureConfigId = configId });

        if (parameters.Count == 0)
        {
            return;
        }

        string insertSql = $@"
        INSERT INTO {parameterConfigTable}
        (
            ProcedureConfigId,
            ParameterName,
            DataType,
            ProcedureDefaultValue,
            IsNullable,
            CustomDefaultValue,
            DisplayName
        )
        VALUES
        (
            @ProcedureConfigId,
            @ParameterName,
            @DataType,
            @ProcedureDefaultValue,
            @IsNullable,
            @CustomDefaultValue,
            @DisplayName
        );";

        foreach (ProcedureParameterConfig param in parameters)
        {
            param.ProcedureConfigId = configId;
        }

        tx.Execute(insertSql, parameters);
    }

    /// <summary>
    /// 取得指定 Procedure 的參數資訊
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <returns>Procedure 參數資訊列表</returns>
    private List<ProcedureOriginalParameter> GetProcedureParameters(string schemaName, string procedureName)
    {
        string sql = @"
        SELECT 
            p.name AS ParameterName,
            t.name AS DataType,
            p.max_length AS MaxLength,
            p.precision AS Precision,
            p.scale AS Scale
        FROM sys.parameters p
        JOIN sys.procedures sp ON p.object_id = sp.object_id
        JOIN sys.schemas s ON sp.schema_id = s.schema_id
        JOIN sys.types t ON p.user_type_id = t.user_type_id
        WHERE s.name = @SchemaName AND sp.name = @ProcedureName
        ORDER BY p.parameter_id";

        return _database.Query<ProcedureOriginalParameter>(sql, new
        {
            SchemaName = schemaName,
            ProcedureName = procedureName
        }).ToList();
    }

    /// <summary>
    /// 將資料庫參數型別轉換為 MSSQL 顯示型別
    /// </summary>
    /// <param name="dataType">原始型別</param>
    /// <param name="maxLength">最大長度</param>
    /// <param name="precision">精度</param>
    /// <param name="scale">小數位數</param>
    /// <returns>MSSQL 顯示型別</returns>
    private static string ConvertToSqlDisplayDataType(string dataType, short maxLength, byte precision, byte scale)
    {
        if (string.IsNullOrWhiteSpace(dataType))
        {
            return string.Empty;
        }

        string normalizedType = dataType.Trim().ToLowerInvariant();

        switch (normalizedType)
        {
            case "varchar":
            case "char":
            case "binary":
            case "varbinary":
                return maxLength == -1
                    ? $"{normalizedType}(max)"
                    : $"{normalizedType}({maxLength})";

            case "nvarchar":
            case "nchar":
                return maxLength == -1
                    ? $"{normalizedType}(max)"
                    : $"{normalizedType}({maxLength / 2})";

            case "decimal":
            case "numeric":
                return $"{normalizedType}({precision},{scale})";

            case "datetime2":
            case "datetimeoffset":
            case "time":
                return $"{normalizedType}({scale})";

            default:
                return normalizedType;
        }
    }

    /// <summary>
    /// 將 Procedure 原始參數資訊與解析出的預設值進行對應
    /// </summary>
    /// <param name="parameters">資料庫查詢取得的參數列表</param>
    /// <param name="parsedParameters">Procedure 內容解析出的參數列表</param>
    /// <returns>整合後的參數顯示資訊</returns>
    private static List<ProcedureParameter> MapProcedureParameters(List<ProcedureOriginalParameter> parameters, List<ProcedureParameterInfo> parsedParameters)
    {
        Dictionary<string, string?> defaultValueMap = parsedParameters
            .GroupBy(parameter => parameter.ParameterName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().DefaultValue,
                StringComparer.OrdinalIgnoreCase);

        List<ProcedureParameter> result = new();

        foreach (ProcedureOriginalParameter parameter in parameters)
        {
            defaultValueMap.TryGetValue(parameter.ParameterName, out string? defaultValue);

            result.Add(new ProcedureParameter
            {
                ParameterName = parameter.ParameterName,
                DataType = ConvertToSqlDisplayDataType(
                    parameter.DataType,
                    parameter.MaxLength,
                    parameter.Precision,
                    parameter.Scale),
                DefaultValue = NormalizeDefaultValue(defaultValue)
            });
        }

        return result;
    }

    /// <summary>
    /// 標準化預設值，去除 N'xxx' 或 'xxx' 的包裹，保留原始值
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string? NormalizeDefaultValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = value.Trim();

        // 處理 N'xxx'
        if (value.Length >= 3 && value.StartsWith("N'", StringComparison.OrdinalIgnoreCase) && value.EndsWith("'"))
        {
            string content = value.Substring(2, value.Length - 3);
            return content.Replace("''", "'");
        }

        // 處理 'xxx'
        if (value.Length >= 2 && value.StartsWith("'") && value.EndsWith("'"))
        {
            string content = value.Substring(1, value.Length - 2);
            return content.Replace("''", "'");
        }

        return value;
    }

    /// <summary>
    /// 標準化 Procedure 內容，確保包含正確的 CREATE OR ALTER PROCEDURE header
    /// </summary>
    /// <param name="schemaName">Schema 名稱</param>
    /// <param name="procedureName">Procedure 名稱</param>
    /// <param name="content">Procedure SQL 內容</param>
    /// <returns>標準化後的 Procedure 內容</returns>
    private static string NormalizeProcedureContent(string schemaName, string procedureName, string content)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name is required.", nameof(schemaName));
        }

        if (string.IsNullOrWhiteSpace(procedureName))
        {
            throw new ArgumentException("Procedure name is required.", nameof(procedureName));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Procedure content is required.", nameof(content));
        }

        content = content.TrimStart('\uFEFF');

        content = Regex.Replace(
            content,
            @"\r?\n[ \t]*GO[ \t]*(?:--.*)?\s*$",
            string.Empty,
            RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1));

        string fullName = $"[{schemaName}].[{procedureName}]";
        string replacement = $"CREATE OR ALTER PROCEDURE {fullName}";

        const string pattern = @"
            ^[ \t]*
            (CREATE\s+OR\s+ALTER|CREATE|ALTER)
            \s+PROCEDURE\s+
            (?:
                \[?[A-Za-z0-9_]+\]?\s*\.\s*
            )?
            \[?[A-Za-z0-9_]+\]?
        ";

        Match match = Regex.Match(
            content,
            pattern,
            RegexOptions.IgnoreCase |
            RegexOptions.IgnorePatternWhitespace |
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            throw new ArgumentException(
                "Procedure content must include a valid CREATE/ALTER PROCEDURE header.",
                nameof(content));
        }

        return string.Concat(
            content.Substring(0, match.Index),
            replacement,
            content.Substring(match.Index + match.Length)
        );
    }

    /// <summary>
    /// 取得參數顯示名稱
    /// </summary>
    /// <param name="parameter">參數設定</param>
    /// <returns>顯示名稱</returns>
    private static string GetParameterDisplayName(ProcedureParameterConfig parameter)
    {
        return string.IsNullOrWhiteSpace(parameter.DisplayName)
            ? parameter.ParameterName
            : parameter.DisplayName;
    }

    /// <summary>
    /// 解析要執行 SP 的參數值
    /// 優先順序：
    /// 1. CustomDefaultValue
    /// 2. ProcedureDefaultValue
    /// 3. null
    /// </summary>
    /// <param name="parameter">參數設定</param>
    /// <returns>最終執行值</returns>
    private static string? ResolveExecutionValue(ProcedureParameterConfig parameter)
    {
        string? value = !string.IsNullOrWhiteSpace(parameter.CustomDefaultValue)
            ? parameter.CustomDefaultValue
            : parameter.ProcedureDefaultValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeDefaultValue(value);
    }

    /// <summary>
    /// 刪除指定 Procedure 對應的所有 Config（含參數）
    /// </summary>
    private static void DeleteProcedureConfigByProcedure(DatabaseTransaction tx, string schemaName, string procedureName)
    {
        string procedureConfigTable = $"[{schemaName}].[ProcedureConfig]";
        string parameterConfigTable = $"[{schemaName}].[ProcedureParameterConfig]";

        string getIdsSql = $@"
        SELECT Id
        FROM {procedureConfigTable}
        WHERE SchemaName = @SchemaName
        AND ProcedureName = @ProcedureName;";

        List<long> configIds = tx.Select<long>(getIdsSql, new
        {
            SchemaName = schemaName,
            ProcedureName = procedureName
        }).ToList();

        if (configIds.Count == 0)
        {
            return;
        }

        string deleteParamSql = $@"
        DELETE FROM {parameterConfigTable}
        WHERE ProcedureConfigId IN @Ids;";

        tx.Execute(deleteParamSql, new { Ids = configIds });

        string deleteConfigSql = $@"
        DELETE FROM {procedureConfigTable}
        WHERE Id IN @Ids;";

        tx.Execute(deleteConfigSql, new { Ids = configIds });
    }
}

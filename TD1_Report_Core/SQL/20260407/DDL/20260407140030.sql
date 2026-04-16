-- 建立 rpt schema（如果不存在的話）
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rpt')
BEGIN
    EXEC('CREATE SCHEMA [rpt]');
    PRINT('[rpt] schema 已建立');
END
ELSE
BEGIN
    PRINT('[rpt] schema 已存在，跳過建立');
END

-- 建立 ProcedureConfig 資料表
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ProcedureConfig' AND s.name = 'rpt')
BEGIN
    CREATE TABLE [rpt].[ProcedureConfig]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [UserId] NVARCHAR(100) NOT NULL,
        [SchemaName] NVARCHAR(128) NOT NULL,
        [ProcedureName] NVARCHAR(128) NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [ShortName] NVARCHAR(100) NULL,
        [IsEnabled] BIT NOT NULL CONSTRAINT [DF_ProcedureConfig_IsEnabled] DEFAULT (1),

        CONSTRAINT [PK_ProcedureConfig] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_ProcedureConfig_User_Schema_Procedure]  UNIQUE ([UserId], [SchemaName], [ProcedureName])
    );
    PRINT('新增 [rpt].[ProcedureConfig] 資料表 完成');
END
ELSE
BEGIN
    PRINT('[rpt].[ProcedureConfig] 資料表已存在，跳過建立');
END

-- 建立 ProcedureParameterConfig 資料表
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'ProcedureParameterConfig' AND s.name = 'rpt')
BEGIN
    CREATE TABLE [rpt].[ProcedureParameterConfig]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL,
        [ProcedureConfigId] BIGINT NOT NULL,
        [ParameterName] NVARCHAR(128) NOT NULL,
        [DataType] NVARCHAR(100) NULL,
        [ProcedureDefaultValue] NVARCHAR(500) NULL,
        [IsNullable] BIT NOT NULL,
        [CustomDefaultValue] NVARCHAR(500) NULL,
        [DisplayName] NVARCHAR(200) NULL,
        [CreateAt] DATETIME2(3) NOT NULL CONSTRAINT [DF_ProcedureParameterConfig_CreateAt] DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT [PK_ProcedureParameterConfig] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_ProcedureParameterConfig_ProcedureConfig] FOREIGN KEY ([ProcedureConfigId]) REFERENCES [rpt].[ProcedureConfig]([Id]),
        CONSTRAINT [UQ_ProcedureParameterConfig_Config_Parameter] UNIQUE ([ProcedureConfigId], [ParameterName])
    );
    PRINT('新增 [rpt].[ProcedureParameterConfig] 資料表 完成');
END
ELSE
BEGIN
    PRINT('[rpt].[ProcedureParameterConfig] 資料表已存在，跳過建立');
END
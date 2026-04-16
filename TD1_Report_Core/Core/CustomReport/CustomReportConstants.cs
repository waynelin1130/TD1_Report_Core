namespace TD1_Report_Core.Core.CustomReport;

/// <summary>
/// 自定義報表相關常數(之後可以抽到資料庫中) 
/// </summary>
public static class CustomReportConstants
{
    /// <summary> 代表今天日期的特殊關鍵字 </summary>
    public const string TodayKeyword = "__TODAY__";

    /// <summary> 支援的特殊關鍵字集合 </summary>
    public static readonly HashSet<string> SupportedKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            TodayKeyword
        };
}
using System.Text;
using TD1_Report_Core.Models.CustomReport;

namespace TD1_Report_Core.CustomReport;

/// <summary>
/// 用於解析 Stored Procedure 內容，提取參數資訊與預設值
/// </summary>
public static class ProcedureParameterParser
{
    /// <summary>
    /// 解析 Stored Procedure SQL 內容，取得參數清單
    /// </summary>
    /// <param name="procedureContent">Stored Procedure 完整 SQL 內容</param>
    /// <returns>參數資訊列表</returns>
    public static List<ProcedureParameterInfo> ParseParameters(string procedureContent)
    {
        if (string.IsNullOrWhiteSpace(procedureContent))
        {
            return new List<ProcedureParameterInfo>();
        }

        // 移除註解，避免干擾解析
        string cleanedContent = RemoveComments(procedureContent);

        // 擷取參數宣告區段
        string parameterSection = ExtractParameterSection(cleanedContent);

        if (string.IsNullOrWhiteSpace(parameterSection))
        {
            return new List<ProcedureParameterInfo>();
        }

        // 將參數切割為單筆
        List<string> parameterItems = SplitParameters(parameterSection);

        List<ProcedureParameterInfo> result = new();

        foreach (string item in parameterItems)
        {
            ProcedureParameterInfo? parameter = ParseSingleParameter(item);

            if (parameter != null)
            {
                result.Add(parameter);
            }
        }

        return result;
    }

    /// <summary>
    /// 移除 SQL 中的單行註解（--）與區塊註解（/* */）
    /// </summary>
    /// <param name="input">原始 SQL 內容</param>
    /// <returns>移除註解後的 SQL</returns>
    private static string RemoveComments(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        StringBuilder result = new();
        bool inSingleQuote = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            char next = i + 1 < input.Length ? input[i + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\r' || current == '\n')
                {
                    inLineComment = false;
                    result.Append(current);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }

                continue;
            }

            if (!inSingleQuote)
            {
                if (current == '-' && next == '-')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }
            }

            if (current == '\'')
            {
                result.Append(current);

                if (inSingleQuote)
                {
                    if (next == '\'')
                    {
                        result.Append(next);
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
                else
                {
                    inSingleQuote = true;
                }

                continue;
            }

            result.Append(current);
        }

        return result.ToString();
    }

    /// <summary>
    /// 擷取 Stored Procedure 中參數宣告區段
    /// </summary>
    /// <param name="content">已清除註解的 SQL</param>
    /// <returns>參數宣告字串</returns>
    private static string ExtractParameterSection(string content)
    {
        int procedureIndex = IndexOfKeyword(content, "PROCEDURE");
        if (procedureIndex < 0)
        {
            return string.Empty;
        }

        int asIndex = FindAsKeywordOutsideQuotes(content, procedureIndex);
        if (asIndex < 0)
        {
            return string.Empty;
        }

        string headerPart = content.Substring(procedureIndex, asIndex - procedureIndex).Trim();

        int firstParameterIndex = headerPart.IndexOf('@');
        if (firstParameterIndex < 0)
        {
            return string.Empty;
        }

        string parameterSection = headerPart.Substring(firstParameterIndex).Trim();

        int openParenIndex = headerPart.IndexOf('(');
        if (openParenIndex >= 0 && openParenIndex < firstParameterIndex)
        {
            int closeParenIndex = FindMatchingClosingParenthesis(headerPart, openParenIndex);
            if (closeParenIndex > openParenIndex)
            {
                return headerPart.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
            }
        }

        return parameterSection;
    }

    /// <summary>
    /// 尋找指定左括號對應的右括號位置（忽略字串內括號）
    /// </summary>
    private static int FindMatchingClosingParenthesis(string text, int openParenIndex)
    {
        bool inSingleQuote = false;
        int depth = 0;

        for (int i = openParenIndex; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (current == '\'')
            {
                if (inSingleQuote)
                {
                    if (next == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
                else
                {
                    inSingleQuote = true;
                }

                continue;
            }

            if (inSingleQuote)
            {
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 忽略大小寫搜尋關鍵字位置
    /// </summary>
    private static int IndexOfKeyword(string input, string keyword)
    {
        return input.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 尋找 AS 關鍵字（忽略字串與括號內）
    /// </summary>
    private static int FindAsKeywordOutsideQuotes(string input, int startIndex)
    {
        bool inSingleQuote = false;
        int parenthesesDepth = 0;

        for (int i = startIndex; i < input.Length - 1; i++)
        {
            char current = input[i];
            char next = input[i + 1];

            if (current == '\'')
            {
                if (inSingleQuote)
                {
                    if (next == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
                else
                {
                    inSingleQuote = true;
                }

                continue;
            }

            if (inSingleQuote)
            {
                continue;
            }

            if (current == '(')
            {
                parenthesesDepth++;
            }
            else if (current == ')')
            {
                parenthesesDepth--;
            }

            if (parenthesesDepth == 0)
            {
                if ((current == 'A' || current == 'a') &&
                    (next == 'S' || next == 's'))
                {
                    bool leftBoundary = i == 0 || char.IsWhiteSpace(input[i - 1]);
                    bool rightBoundary = i + 2 >= input.Length || char.IsWhiteSpace(input[i + 2]);

                    if (leftBoundary && rightBoundary)
                    {
                        return i;
                    }
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// 將參數區段切割成單一參數（避免切壞 DECIMAL(18,2)）
    /// </summary>
    private static List<string> SplitParameters(string parameterSection)
    {
        List<string> result = new();
        StringBuilder current = new();

        bool inSingleQuote = false;
        int parenthesesDepth = 0;

        for (int i = 0; i < parameterSection.Length; i++)
        {
            char c = parameterSection[i];
            char next = i + 1 < parameterSection.Length ? parameterSection[i + 1] : '\0';

            if (c == '\'')
            {
                current.Append(c);

                if (inSingleQuote)
                {
                    if (next == '\'')
                    {
                        current.Append(next);
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
                else
                {
                    inSingleQuote = true;
                }

                continue;
            }

            if (!inSingleQuote)
            {
                if (c == '(')
                {
                    parenthesesDepth++;
                }
                else if (c == ')')
                {
                    parenthesesDepth--;
                }
                else if (c == ',' && parenthesesDepth == 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }

    /// <summary>
    /// 解析單一參數字串，取得名稱、型別、預設值與是否為 OUTPUT
    /// </summary>
    private static ProcedureParameterInfo? ParseSingleParameter(string parameterText)
    {
        if (string.IsNullOrWhiteSpace(parameterText))
        {
            return null;
        }

        string text = parameterText.Trim();

        if (!text.StartsWith("@"))
        {
            return null;
        }

        int firstSpaceIndex = FindFirstWhitespaceOutsideBrackets(text);
        if (firstSpaceIndex < 0)
        {
            return null;
        }

        string parameterName = text.Substring(0, firstSpaceIndex).Trim();
        string remaining = text.Substring(firstSpaceIndex).Trim();

        int equalIndex = FindEqualsOutsideQuotesAndParentheses(remaining);

        string typePart;
        string? defaultValue = null;

        if (equalIndex >= 0)
        {
            typePart = remaining.Substring(0, equalIndex).Trim();
            defaultValue = remaining.Substring(equalIndex + 1).Trim();
        }
        else
        {
            typePart = remaining.Trim();
        }

        bool isOutput = false;

        if (typePart.EndsWith(" OUTPUT", StringComparison.OrdinalIgnoreCase))
        {
            isOutput = true;
            typePart = typePart[..^7].TrimEnd();
        }
        else if (typePart.EndsWith(" OUT", StringComparison.OrdinalIgnoreCase))
        {
            isOutput = true;
            typePart = typePart[..^4].TrimEnd();
        }

        return new ProcedureParameterInfo
        {
            ParameterName = parameterName,
            DataType = typePart,
            DefaultValue = NormalizeDefaultValue(defaultValue),
            IsOutput = isOutput
        };
    }

    /// <summary>
    /// 找到第一個不在 [] 中的空白位置（用於切分參數名稱）
    /// </summary>
    private static int FindFirstWhitespaceOutsideBrackets(string text)
    {
        bool inSquareBracket = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '[')
            {
                inSquareBracket = true;
            }
            else if (c == ']')
            {
                inSquareBracket = false;
            }
            else if (!inSquareBracket && char.IsWhiteSpace(c))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 找到等號位置（忽略字串與括號）
    /// </summary>
    private static int FindEqualsOutsideQuotesAndParentheses(string text)
    {
        bool inSingleQuote = false;
        int parenthesesDepth = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (current == '\'')
            {
                if (inSingleQuote)
                {
                    if (next == '\'')
                    {
                        i++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
                else
                {
                    inSingleQuote = true;
                }

                continue;
            }

            if (inSingleQuote)
            {
                continue;
            }

            if (current == '(')
            {
                parenthesesDepth++;
            }
            else if (current == ')')
            {
                parenthesesDepth--;
            }

            if (parenthesesDepth == 0 && current == '=')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 正規化預設值（去除多餘逗號與空白）
    /// </summary>
    private static string? NormalizeDefaultValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string result = value.Trim();

        if (result.EndsWith(","))
        {
            result = result[..^1].TrimEnd();
        }

        return result;
    }
}

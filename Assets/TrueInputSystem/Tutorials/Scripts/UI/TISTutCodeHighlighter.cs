// ============================================================
//  TrueInputSystem Tutorials — TISTutCodeHighlighter.cs
//
//  Tô màu cú pháp C# đơn giản cho khung "Code mẫu" (TMP rich text).
//  Mọi đoạn chữ được bọc <noparse> → generic như GetComponent<T>() không
//  bị TMP hiểu nhầm là thẻ.
// ============================================================

using System.Collections.Generic;
using System.Text;

namespace GaMo.TrueInputSystem.Tutorials
{
    public static class TISTutCodeHighlighter
    {
        public const string ColComment = "#8FA39A";
        public const string ColString  = "#F7D58B";
        public const string ColKeyword = "#9CCBFF";
        public const string ColType    = "#D7F59B";
        public const string ColNumber  = "#FFB892";

        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "var", "new", "if", "else", "for", "foreach", "in", "while", "return", "void", "public",
            "private", "protected", "static", "class", "using", "namespace", "true", "false", "null",
            "bool", "float", "int", "string", "out", "ref", "this", "override", "readonly", "const",
            "switch", "case", "break", "default", "yield", "async", "await", "is", "as", "get", "set",
        };

        public static string Highlight(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var sb = new StringBuilder(code.Length * 2);
            int i = 0, n = code.Length;

            while (i < n)
            {
                char c = code[i];

                // // chú thích tới hết dòng
                if (c == '/' && i + 1 < n && code[i + 1] == '/')
                {
                    int e = code.IndexOf('\n', i);
                    if (e < 0) e = n;
                    Span(sb, code.Substring(i, e - i), ColComment);
                    i = e;
                    continue;
                }

                // "chuỗi" (kể cả $"...")
                if (c == '"' || (c == '$' && i + 1 < n && code[i + 1] == '"'))
                {
                    int s = i;
                    i += c == '$' ? 2 : 1;
                    while (i < n && code[i] != '"' && code[i] != '\n')
                    {
                        if (code[i] == '\\') i++;
                        i++;
                    }
                    if (i < n && code[i] == '"') i++;
                    Span(sb, code.Substring(s, i - s), ColString);
                    continue;
                }

                // số
                if (char.IsDigit(c))
                {
                    int s = i;
                    while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] == '.')) i++;
                    Span(sb, code.Substring(s, i - s), ColNumber);
                    continue;
                }

                // từ khoá / kiểu (chữ hoa đầu, đứng trước '<' hoặc ' ' + tên) / tên thường
                if (char.IsLetter(c) || c == '_')
                {
                    int s = i;
                    while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
                    string w = code.Substring(s, i - s);
                    if (Keywords.Contains(w))                 Span(sb, w, ColKeyword);
                    else if (w.StartsWith("TIS") || IsTypeLike(code, s, i, w)) Span(sb, w, ColType);
                    else                                      Plain(sb, w);
                    continue;
                }

                // ký tự khác: gom liên tiếp
                {
                    int s = i;
                    while (i < n && !char.IsLetterOrDigit(code[i]) && code[i] != '_' && code[i] != '"'
                           && code[i] != '$' && !(code[i] == '/' && i + 1 < n && code[i + 1] == '/'))
                        i++;
                    if (i == s) i++;
                    Plain(sb, code.Substring(s, i - s));
                }
            }
            return sb.ToString();
        }

        // Chữ hoa đầu + đứng ngay trước tên biến / dấu '<' / '.' tĩnh → coi là kiểu
        static bool IsTypeLike(string code, int s, int e, string w)
        {
            if (!char.IsUpper(w[0])) return false;
            bool afterDot = s > 0 && code[s - 1] == '.';
            if (afterDot) return false;
            return e < code.Length && (code[e] == '<' || code[e] == '.' || code[e] == ' ' || code[e] == '[');
        }

        static void Span(StringBuilder sb, string text, string color)
        {
            sb.Append("<color=").Append(color).Append("><noparse>").Append(text).Append("</noparse></color>");
        }

        static void Plain(StringBuilder sb, string text)
        {
            sb.Append("<noparse>").Append(text).Append("</noparse>");
        }
    }
}

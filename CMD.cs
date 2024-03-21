#nullable enable

#pragma warning disable IDE0251 // 将成员设为“readonly”
#pragma warning disable IDE0301 // 简化集合初始化
#pragma warning disable IDE0305 // 简化集合初始化

using System.Diagnostics.CodeAnalysis;
using System.Text;

// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeNamespaceBody
// ReSharper disable MemberCanBePrivate.Global

namespace Command
{
    /// <summary>
    /// 命令字串解析
    /// <para>
    /// 命令格式:
    /// <code> command [params ...] [[#paramName namedParams ...] ...] </code>
    /// 其中:
    ///     <para>
    ///     command 为命令名, 位于命令最前端, 格式为任意不含空格类字符且不以 '#' 或 '"' 开头的字符串;
    ///     #paramName 为参数名, 格式为任意不以 '#' 开头的字符串; param 为参数值, 格式为任意不以 '#'
    ///     开头的字符串; 一个参数名下可以存在多个参数, 以空格分割. 若要输入包含空格的字符串作为参数,
    ///     可将参数用 '"' 包围起来, 被包围的字符串适用转义字符 '\'
    ///     <code> command 0 2.1 #paramName 20000129 3.14 str "str with space" #t "2023/06/30 \n 2" </code>
    ///     若未指明参数名, 则参数被认为是直接参数, 被收归在 DirParams 字段中.
    ///     <code> command 19491001 beijing </code>
    ///     若参数名为空字串, 则其参数被丢弃 (下例中参数 str 和 str with space 被丢弃):
    ///     <code> command 长太息以掩涕兮 # str "str with space" #t aaa 2.71828 </code>
    ///     若出现相同参数名, 新的会覆盖旧的 (下例中 #t 的值为 aaa 1.414):
    ///     <code> command abc 0.618 #t "str i" e=a #t aaa 1.414 </code>
    ///     在命令匹配了命令名或参数名前的直接参数会被丢弃 (下例中的 "a" 被丢弃):
    ///     <code> "a" cmd #t aa bb </code>
    ///     若在出现第一个参数名前没有合法的命令名, 则命令名为 string.Empty
    ///     <code> "xxx" #t aaa cmd aa "aaa" </code>
    ///     </para>
    /// </para>
    /// </summary>
    public readonly struct CMD
    {
        public string? Command { get; }

        public string? Text { get; }
        public IReadOnlyList<string> DirParams => _dirParams ?? Array.Empty<string>();
        public IReadOnlyList<string> this[string? paramName] =>
            _params == null || paramName == null ? Array.Empty<string>() :
            _params.TryGetValue(paramName, out var v) ? v : Array.Empty<string>();

        private readonly string[]? _dirParams;
        private readonly SortedList<string, string[]>? _params;

        private const char ParamChar = '#';
        private const char StrSpanChar = '\"';
        private const char EscapeChar = '\\';

        #region Core

        public CMD(string? cmd, StringBuilder? sBuilder = null)
            : this(cmd != null ? cmd.AsSpan() : ReadOnlySpan<char>.Empty, sBuilder) {}

        public CMD(string?[]? args, StringBuilder? sBuilder = null, StringBuilder? sBuilder2 = null)
        {
            _params = null;
            _dirParams = null;
            Text = string.Empty;
            Command = string.Empty;
            if (args is not { Length: > 0 }) return;

            try
            {
                sBuilder ??= new StringBuilder();
                sBuilder2 ??= new StringBuilder();
                sBuilder.Clear();
                sBuilder2.Clear();

                var tempList = new List<string>();
                var paramList = new SortedList<string, string[]>();

                string? curParamName = null;
                foreach (var v in args)
                {
                    if (v == null) continue;
                    if (curParamName == null && Command == string.Empty && v.Length > 0 && !v.StartsWith(StrSpanChar) && !v.StartsWith(ParamChar))
                    {
                        sBuilder.Clear();
                        var s = v.Trim();
                        if (s.Length > 0)
                        {
                            foreach (var c in s)
                            {
                                if (IsSpace(c))
                                {
                                    if (Command == string.Empty && sBuilder.Length > 0)
                                        sBuilder2.Append(Command = ApplyEscape(sBuilder.ToString(), sBuilder))
                                            .Append(' ');
                                    else if (Command != string.Empty)
                                    {
                                        var s2 = ApplyEscape(sBuilder.ToString(), sBuilder);
                                        tempList.Add(s2);
                                        sBuilder2.Append(StrSpanChar).Append(s2).Append(StrSpanChar).Append(' ');
                                    }

                                    sBuilder.Clear();
                                    continue;
                                }
                                sBuilder.Append(c);
                            }

                            if (Command == string.Empty && sBuilder.Length > 0)
                                sBuilder2.Append(Command = ApplyEscape(sBuilder.ToString(), sBuilder));
                            else if (sBuilder.Length > 0)
                            {
                                var s2 = ApplyEscape(sBuilder.ToString(), sBuilder);
                                tempList.Add(s2);
                                sBuilder2.Append(StrSpanChar).Append(s2).Append(StrSpanChar).Append(' ');
                            }
                        }
                        continue;
                    }

                    if (v.StartsWith(ParamChar))
                    {
                        if (!string.IsNullOrWhiteSpace(curParamName))
                        {
                            paramList[curParamName] = tempList.ToArray();
                            sBuilder2.Append(ParamChar).Append(curParamName).Append(' ');
                            foreach (var p in tempList) sBuilder2.Append(p).Append(' ');
                        }
                        else if (curParamName == null)
                        {
                            _dirParams = tempList.ToArray();
                            foreach (var p in tempList) sBuilder2.Append(p).Append(' ');
                        }

                        curParamName = v[1..];
                        tempList.Clear();
                    }
                    else tempList.Add(v);
                }

                if (tempList.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(curParamName))
                    {
                        paramList[curParamName] = tempList.ToArray();
                        sBuilder2.Append(ParamChar).Append(curParamName).Append(' ');
                        foreach (var p in tempList)
                            sBuilder2.Append(p).Append(' ');
                    }
                    else if (curParamName == null)
                    {
                        _dirParams = tempList.ToArray();
                        foreach (var p in tempList)
                            sBuilder2.Append(p).Append(' ');
                    }
                }

                TrimEnd(sBuilder2);
                Text = sBuilder2.ToString();
                _params = paramList;
            }
            catch (Exception)
            {
                _params = null;
                _dirParams = null;
                Command = string.Empty;
                throw;
            }
        }

        public CMD(ReadOnlySpan<char> cmd, StringBuilder? sBuilder = null)
        {
            const byte typeNone = 0;
            const byte typeString = 1;
            const byte typeParamName = 2;

            _params = null;
            _dirParams= null;
            Command = string.Empty;
            Text = cmd.ToString().TrimEnd();
            if (cmd.Length <= 0) return;

            sBuilder ??= new StringBuilder();
            sBuilder.Clear();

            try
            {
                var tempList = new List<string>();
                var paramList = new SortedList<string, string[]>();

                var anchor = 0;
                var isInWords = false;
                var isEscaping = false;
                var curMatchingType = typeNone;

                string? curParamName = null;
                for (var i = 0; i < cmd.Length; i++)
                {
                    if (curMatchingType != typeString && IsSpace(cmd[i]))
                    {
                        if (isInWords)
                        {
                            var s = cmd[anchor..i].ToString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                if (Command == string.Empty
                                    && curMatchingType != typeParamName
                                    && paramList.Count == 0
                                    && curParamName == null) { Command = s; tempList.Clear(); }
                                else if (curMatchingType != typeParamName) tempList.Add(s);
                                else curParamName = s[1..];
                            }
                            isInWords = false;
                            curMatchingType = typeNone;
                        }
                        continue;
                    }

                    if (!isInWords)
                    {
                        anchor = i;
                        switch (cmd[i])
                        {
                            case StrSpanChar:
                                curMatchingType = typeString;
                                break;
                            case ParamChar:
                                if (!string.IsNullOrWhiteSpace(curParamName))
                                    paramList[curParamName] = tempList.ToArray();
                                else if (curParamName == null)
                                    _dirParams = tempList.ToArray();

                                tempList.Clear();
                                curMatchingType = typeParamName;
                                curParamName = string.Empty;
                                break;
                        }
                        isInWords = true;
                        continue;
                    }

                    if (curMatchingType == typeString && cmd[i] == StrSpanChar && !isEscaping)
                    {
                        var slice = cmd.Slice(anchor + 1, i - anchor - 1);
                        var s = ApplyEscape(slice, sBuilder);
                        tempList.Add(s);
                        isInWords = false;
                        curMatchingType = typeNone;
                    }

                    if (curMatchingType == typeString && cmd[i] == EscapeChar && !isEscaping)
                    {
                        isEscaping = true;
                        continue;
                    }
                    isEscaping = false;
                }

                if (isInWords)
                {
                    if (curMatchingType == typeString)
                    {
                        if (cmd.Length - anchor - 1 > 2)
                        {
                            var slice = cmd.Slice(anchor + 1, cmd.Length - anchor - 1);
                            var s = ApplyEscape(slice, sBuilder);
                            tempList.Add(s);
                            curMatchingType = typeNone;
                        }
                    }
                    else
                    {
                        var s = cmd[anchor..].ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (Command == string.Empty
                                && curMatchingType != typeParamName
                                && paramList.Count == 0
                                && curParamName == null) { Command = s; tempList.Clear(); }
                            else if (curMatchingType != typeParamName) tempList.Add(s);
                            else curParamName = s[1..];
                        }
                    }
                }

                if (curMatchingType != typeString)
                {
                    if (!string.IsNullOrWhiteSpace(curParamName))
                        paramList[curParamName] = tempList.Count > 0
                            ? tempList.ToArray() : Array.Empty<string>();
                    else if (curParamName == null)
                        _dirParams = tempList.Count > 0 ? tempList.ToArray() : Array.Empty<string>();
                }

                tempList.Clear();
                _params = paramList;
            }
            catch (Exception)
            {
                _params = null;
                _dirParams = null;
                Command = string.Empty;
                throw;
            }
        }

        public bool Equals(CMD o) => o.Text == Text;
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is CMD cmd && Equals(cmd);
        public override int GetHashCode() => string.GetHashCode(Text);
        public override string ToString() => Text ?? string.Empty;
        public static bool operator !=(CMD left, CMD right) => !(left == right);
        public static bool operator ==(CMD left, CMD right) => left.Equals(right);

        #endregion

        #region Getter

        public bool ContainsParam(string? paramName) => _params != null && !string.IsNullOrEmpty(paramName) && _params.ContainsKey(paramName);

        public int Int(string? paramName, int idx, int defaultValue = default)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;
            var l = this[paramName];
            if (idx < 0 || idx >= l.Count) return defaultValue;
            return TryParseInt(l[idx], out var v) ? v : defaultValue;
        }

        public int Int(int idx, int defaultValue = default)
        {
            if (idx < 0 || idx >= DirParams.Count) return defaultValue;
            return TryParseInt(DirParams[idx], out var v) ? v : defaultValue;
        }

        public long Long(string? paramName, int idx, long defaultValue = default)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;
            var l = this[paramName];
            if (idx < 0 || idx >= l.Count) return defaultValue;
            return TryParseLong(l[idx], out var v) ? v : defaultValue;
        }

        public long Long(int idx, long defaultValue = default)
        {
            if (idx < 0 || idx >= DirParams.Count) return defaultValue;
            return TryParseLong(DirParams[idx], out var v) ? v : defaultValue;
        }

        public float Float(string? paramName, int idx, float defaultValue = default)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;
            var l = this[paramName];
            if (idx < 0 || idx >= l.Count) return defaultValue;
            return float.TryParse(l[idx], out var v) ? v : defaultValue;
        }

        public float Float(int idx, float defaultValue = default)
        {
            if (idx < 0 || idx >= DirParams.Count) return defaultValue;
            return float.TryParse(DirParams[idx], out var v) ? v : defaultValue;
        }

        public double Double(string? paramName, int idx, double defaultValue = default)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;
            var l = this[paramName];
            if (idx < 0 || idx >= l.Count) return defaultValue;
            return double.TryParse(l[idx], out var v) ? v : defaultValue;
        }

        public double Double(int idx, double defaultValue = default)
        {
            if (idx < 0 || idx >= DirParams.Count) return defaultValue;
            return double.TryParse(DirParams[idx], out var v) ? v : defaultValue;
        }

        public string? Str(string? paramName, int idx, string? defaultValue = null)
        {
            if (string.IsNullOrEmpty(paramName)) return defaultValue;
            var l = this[paramName];
            if (idx < 0 || idx >= l.Count) return defaultValue;
            return l[idx];
        }

        public string? Str(int idx, string? defaultValue = null)
        {
            if (idx < 0 || idx >= DirParams.Count) return defaultValue;
            return DirParams[idx];
        }

        #endregion

        #region CodeAnalyze

        public enum MatchType : byte
        {
            None = 0,
            Param = 1,
            ParamName = 2,
            Command = 3
        }

        public struct AnaInfo
        {
            public string MatchedCmdName;
            public string HighlightedCommand;
            public string LastMatch;
            public MatchType LastMatchType;
        }

        public interface IColorFormatter
        {
            public enum ColorType
            {
                Cmd,
                Param,
                String,
                ParamName,
                EscapeChar
            }

            public string ColorTail();
            public string ColorHead(ColorType type);
        }

        public static AnaInfo AnalyzeSyntax(string cmd, IColorFormatter color, StringBuilder? sb = null, StringBuilder? lastMatch = null)
        {
            static string ClearLastMatch(StringBuilder last, MatchType lastType, string cmdCache)
            {
                cmdCache = lastType == MatchType.Command ? last.ToString() : cmdCache;
                last.Clear();
                return cmdCache;
            }

            if (string.IsNullOrWhiteSpace(cmd))
                return new AnaInfo
                {
                    HighlightedCommand = cmd,
                    MatchedCmdName = string.Empty,
                    LastMatch = string.Empty,
                    LastMatchType = MatchType.None
                };

            try
            {
                var curCmd = string.Empty;
                sb ??= new StringBuilder();
                lastMatch ??= new StringBuilder();

                sb.Clear();
                lastMatch.Clear();

                const byte typeNone = 0;
                const byte typeString = 1;
                const byte typeParamName = 2;

                var lastMatchType = MatchType.None;
                var colBracket = 0;
                var hadCmd = false;
                var isInWords = false;
                var isEscaping = false;
                var curMatchingType = typeNone;

                foreach (var c in cmd)
                {
                    switch (isInWords)
                    {
                        case false when c == ' ':
                            sb.Append(c);
                            lastMatchType = MatchType.None;
                            continue;

                        case false:
                            isInWords = true;
                            curMatchingType = c switch
                            {
                                StrSpanChar => typeString,
                                ParamChar => typeParamName,
                                _ => typeNone
                            };

                            switch (c)
                            {
                                case StrSpanChar:
                                    curCmd = ClearLastMatch(lastMatch, lastMatchType, curCmd);
                                    lastMatchType = MatchType.Param;
                                    sb.Append(color.ColorHead(IColorFormatter.ColorType.String));
                                    break;
                                case ParamChar:
                                    curCmd = ClearLastMatch(lastMatch, lastMatchType, curCmd);
                                    lastMatchType = MatchType.ParamName;
                                    sb.Append(color.ColorHead(IColorFormatter.ColorType.ParamName));
                                    hadCmd = true;
                                    break;
                                default:
                                    if (!hadCmd)
                                    {
                                        curCmd = ClearLastMatch(lastMatch, lastMatchType, curCmd);
                                        lastMatchType = MatchType.Command;
                                        sb.Append(color.ColorHead(IColorFormatter.ColorType.Cmd));
                                        hadCmd = true;
                                    }
                                    else
                                    {
                                        curCmd = ClearLastMatch(lastMatch, lastMatchType, curCmd);
                                        lastMatchType = MatchType.Param;
                                        sb.Append(color.ColorHead(IColorFormatter.ColorType.Param));
                                    }
                                    break;
                            }
                            colBracket++;
                            sb.Append(c);
                            lastMatch.Append(c);
                            continue;
                    }

                    if (curMatchingType != typeString)
                    {
                        sb.Append(c);
                        if (c == ' ')
                        {
                            colBracket--;
                            isInWords = false;
                            curCmd = ClearLastMatch(lastMatch, lastMatchType, curCmd);
                            lastMatchType = MatchType.None;
                            sb.Append(color.ColorTail());
                        }
                        else lastMatch.Append(c);
                        continue;
                    }

                    switch (isEscaping)
                    {
                        case false when c == EscapeChar:
                            sb.Append(color.ColorHead(IColorFormatter.ColorType.EscapeChar));
                            sb.Append(c);
                            colBracket++;
                            isEscaping = true;
                            continue;
                        case true:
                            sb.Append(c == EscapeChar ? "\\\u200b" : c);
                            sb.Append(color.ColorTail());
                            colBracket--;
                            isEscaping = false;
                            continue;
                    }

                    if (c == StrSpanChar)
                    {
                        sb.Append(c);
                        colBracket--;
                        isInWords = false;
                        sb.Append(color.ColorTail());
                        continue;
                    }

                    sb.Append(c);
                    lastMatch.Append(c);
                }

                while(colBracket > 0)
                {
                    colBracket--;
                    if (sb[^1] == '\\') continue;
                    sb.Append(color.ColorTail());
                }

                return new AnaInfo
                {
                    HighlightedCommand = sb.ToString(),
                    LastMatch = lastMatch.ToString(),
                    LastMatchType = lastMatchType,
                    MatchedCmdName = curCmd
                };
            }
            catch (Exception)
            {
                return new AnaInfo
                {
                    HighlightedCommand = cmd,
                    MatchedCmdName = string.Empty,
                    LastMatch = string.Empty,
                    LastMatchType = MatchType.None
                };
            }
        }

        #endregion

        #region Util

        #region Parse Stream

        /// <summary>
        /// 多行命令字串解析
        /// 在行尾使用 '\' 字符可以连接上下两行; 使用 // 可以输入行尾注释
        /// </summary>
        /// <param name="content"></param>
        /// <param name="sBuilder"></param>
        /// <returns></returns>
        public static List<CMD> ToCMDs(StreamReader content, StringBuilder? sBuilder = null)
        {
            var result = new List<CMD>();
            sBuilder ??= new StringBuilder();
            sBuilder.Clear();

            while (!content.EndOfStream)
            {
                var line = content.ReadLine();
                if (line is not {Length: > 0}) continue;
                ParseFromStream(result, line, sBuilder);
            }

            if (sBuilder.Length <= 0) return result;
            var s = sBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(s))
                result.Add(new CMD(s, sBuilder));
            return result;
        }

        /// <summary>
        /// 多行命令字串解析
        /// 在行尾使用 '\' 字符可以连接上下两行; 使用 // 可以输入行尾注释
        /// </summary>
        /// <param name="content"></param>
        /// <param name="sBuilder"></param>
        /// <returns></returns>
        public static async Task<List<CMD>> ToCMDsAsync(StreamReader content, StringBuilder? sBuilder = null)
        {
            var result = new List<CMD>();
            sBuilder ??= new StringBuilder();
            sBuilder.Clear();

            while (!content.EndOfStream)
            {
                var line = await content.ReadLineAsync();
                if (line is not {Length: > 0}) continue;
                ParseFromStream(result, line, sBuilder);
            }

            if (sBuilder.Length <= 0) return result;
            var s = sBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(s))
                result.Add(new CMD(s, sBuilder));
            return result;
        }

        private static void ParseFromStream(ICollection<CMD> target, string line, StringBuilder sBuilder)
        {
            var joinNextLine = false;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            switch (comment)
            {
                case > 0:
                {
                    var isEmptyOrSpace = true;

                    for (var i = comment - 1; i >= 0; i--)
                    {
                        if (i == comment - 1)
                        {
                            if (line[i] == '\\') joinNextLine = true;
                            else break;
                            continue;
                        }

                        if (line[i] != '\\') break;
                        joinNextLine = !joinNextLine;
                    }
                    if (joinNextLine) comment--;
                    if (comment <= 0) return;

                    sBuilder.Append(' ');
                    for (var i = 0; i < comment; i++)
                    {
                        if (isEmptyOrSpace && IsSpace(line[i])) continue;
                        sBuilder.Append(line[i]);
                        isEmptyOrSpace = false;
                    }
                    if (isEmptyOrSpace) return;
                    break;
                }
                case 0: return;
                default:
                {
                    for (var i = line.Length - 1; i >= 0; i--)
                    {
                        if (i == line.Length - 1)
                        {
                            if (line[i] == '\\') joinNextLine = true;
                            else break;
                            continue;
                        }

                        if (line[i] != '\\') break;
                        joinNextLine = !joinNextLine;
                    }
                    if (joinNextLine && line.Length <= 1) return;

                    sBuilder.Append(' ').Append(line);
                    if (joinNextLine) sBuilder.Length--;
                    break;
                }
            }

            if (joinNextLine) return;
            target.Add(new CMD(sBuilder.ToString(), sBuilder));
            sBuilder.Clear();
        }

        #endregion

        public string FormattedText
        {
            get
            {
                var sBuilder = new StringBuilder();
                if (!string.IsNullOrEmpty(Command)) sBuilder.Append(Command).Append(' ');
                if (_dirParams != null) foreach (var p in _dirParams) AppendStringParam(sBuilder, p).Append(' ');
                if (_params == null) return sBuilder.ToString();
                {
                    foreach (var (k, v) in _params)
                    {
                        sBuilder.Append(ParamChar).Append(k).Append(' ');
                        foreach (var p in v) AppendStringParam(sBuilder, p).Append(' ');
                    }
                }
                return sBuilder.ToString();

                static StringBuilder AppendStringParam(StringBuilder sb, string p)
                {
                    sb.Append(StrSpanChar);
                    foreach (var c in p)
                        sb.Append(c switch
                        {
                            '"' => "\\\"",
                            '\\' => "\\\\",
                            '\n' => "\\n",
                            '\t' => "\\t",
                            '\0' => "\\0",
                            '\a' => "\\a",
                            '\b' => "\\b",
                            '\v' => "\\v",
                            '\f' => "\\f",
                            '\r' => "\\r",
                            _ => c.ToString(),
                        });
                    return sb.Append(StrSpanChar);
                }
            }
        }

        private static string ApplyEscape(ReadOnlySpan<char> str, StringBuilder sBuilder)
        {
            sBuilder.Clear();
            var escaping = false;

            foreach (var c in str)
            {
                if (c != EscapeChar || escaping)
                {
                    if (!escaping)
                    {
                        sBuilder.Append(c);
                        continue;
                    }
                    sBuilder.Append(c switch
                    {
                        '0' => '\0',
                        'a' => '\a',
                        'b' => '\b',
                        't' => '\t',
                        'n' => '\n',
                        'v' => '\v',
                        'f' => '\f',
                        'r' => '\r',
                        _ => c
                    });
                    escaping = false;
                }
                else escaping = true;
            }

            return sBuilder.ToString();
        }

        private static bool TryParseInt(string s, out int v)
        {
            v = 0;
            if (s.StartsWith("0x"))
            {
                try
                {
                    v = Convert.ToInt32(s[2..], 16);
                    return true;
                }
                catch (Exception) { return false; }
            }

            if (s.StartsWith("0c"))
            {
                try
                {
                    v = Convert.ToInt32(s[2..], 8);
                    return true;
                }
                catch (Exception) { return false; }
            }

            if (!s.StartsWith("0b")) return int.TryParse(s, out v);

            try
            {
                v = Convert.ToInt32(s[2..], 2);
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool TryParseLong(string s, out long v)
        {
            v = 0;
            if (s.StartsWith("0x"))
            {
                try
                {
                    v = Convert.ToInt64(s[2..], 16);
                    return true;
                }
                catch (Exception) { return false; }
            }

            if (s.StartsWith("0c"))
            {
                try
                {
                    v = Convert.ToInt64(s[2..], 8);
                    return true;
                }
                catch (Exception) { return false; }
            }

            if (!s.StartsWith("0b")) return long.TryParse(s, out v);
            try
            {
                v = Convert.ToInt64(s[2..], 2);
                return true;
            }
            catch (Exception) { return false; }
        }

        private static void TrimEnd(StringBuilder sb)
        {
            for (var i = sb.Length - 1; i >= 0; i--)
            {
                if (IsSpace(sb[i])) continue;
                sb.Length = i + 1;
                return;
            }
            sb.Clear();
        }

        private static bool IsSpace(char c) => char.IsWhiteSpace(c);

        #endregion
    }

}

#pragma warning restore IDE0305 // 简化集合初始化
#pragma warning restore IDE0301 // 简化集合初始化
#pragma warning restore IDE0251 // 将成员设为“readonly”
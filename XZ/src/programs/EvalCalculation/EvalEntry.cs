using System;
using System.Collections.Generic;
using System.Globalization;
namespace XZ;

public static class ExpressionEvaluator
{
    public static double Evaluate(string expr)
    {
        var tokens = Tokenize(expr);
        var rpn = ToRpn(tokens);
        return EvaluateRpn(rpn);
    }
    private enum TokType { Number, Op, LParen, RParen }
    private record Token(TokType Type, string Text);
    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (char.IsDigit(s[i]) || (s[i] == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int start = i;
                bool hasDot = false;
                while (i < s.Length && (char.IsDigit(s[i]) || (!hasDot && s[i] == '.')))
                {
                    if (s[i] == '.') hasDot = true;
                    i++;
                }
                tokens.Add(new Token(TokType.Number, s.Substring(start, i - start)));
                continue;
            }
            if (i + 1 < s.Length)
            {
                var two = s.Substring(i, 2);
                if (two == "//")
                {
                    tokens.Add(new Token(TokType.Op, "//"));
                    i += 2;
                    continue;
                }
            }
            char c = s[i];
            if (c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '^')
            {
                tokens.Add(new Token(TokType.Op, c.ToString()));
                i++;
                continue;
            }
            if (c == '(')
            {
                tokens.Add(new Token(TokType.LParen, "("));
                i++;
                continue;
            }
            if (c == ')')
            {
                tokens.Add(new Token(TokType.RParen, ")"));
                i++;
                continue;
            }
            throw new ArgumentException($"予期しない文字: '{c}' at pos {i}");
        }
        var outTokens = new List<Token>();
        for (int j = 0; j < tokens.Count; j++)
        {
            var t = tokens[j];
            if (t.Type == TokType.Op && (t.Text == "+" || t.Text == "-"))
            {
                bool isUnary = (j == 0) ||
                    (tokens[j - 1].Type == TokType.Op) ||
                    (tokens[j - 1].Type == TokType.LParen);
                if (isUnary)
                {
                    outTokens.Add(new Token(TokType.Op, t.Text == "+" ? "u+" : "u-"));
                    continue;
                }
            }
            outTokens.Add(t);
        }
        return outTokens;
    }
    private static readonly Dictionary<string, (int prec, bool rightAssoc)> OpInfo = new()
    {
        ["u+"] = (5, true),
        ["u-"] = (5, true),
        ["^"] = (4, true),
        ["*"] = (3, false),
        ["/"] = (3, false),
        ["//"] = (3, false),
        ["%"] = (3, false),
        ["+"] = (2, false),
        ["-"] = (2, false),
    };
    private static List<Token> ToRpn(List<Token> tokens)
    {
        var output = new List<Token>();
        var ops = new Stack<Token>();
        foreach (var t in tokens)
        {
            if (t.Type == TokType.Number)
            {
                output.Add(t);
            }
            else if (t.Type == TokType.Op)
            {
                while (ops.Count > 0 && ops.Peek().Type == TokType.Op)
                {
                    var top = ops.Peek();
                    var curInfo = OpInfo[t.Text];
                    var topInfo = OpInfo[top.Text];
                    if ((!curInfo.rightAssoc && curInfo.prec <= topInfo.prec) ||
                        (curInfo.rightAssoc && curInfo.prec < topInfo.prec))
                    {
                        output.Add(ops.Pop());
                        continue;
                    }
                    break;
                }
                ops.Push(t);
            }
            else if (t.Type == TokType.LParen)
            {
                ops.Push(t);
            }
            else if (t.Type == TokType.RParen)
            {
                bool found = false;
                while (ops.Count > 0)
                {
                    var top = ops.Pop();
                    if (top.Type == TokType.LParen) { found = true; break; }
                    output.Add(top);
                }
                if (!found) throw new ArgumentException("括弧が一致しません。");
            }
        }
        while (ops.Count > 0)
        {
            var top = ops.Pop();
            if (top.Type == TokType.LParen || top.Type == TokType.RParen) throw new ArgumentException("括弧が一致しません。");
            output.Add(top);
        }
        return output;
    }
    private static double EvaluateRpn(List<Token> rpn)
    {
        var st = new Stack<(bool isInt, long iVal, double dVal)>();
        foreach (var t in rpn)
        {
            if (t.Type == TokType.Number)
            {
                if (t.Text.Contains("."))
                {
                    double d = double.Parse(t.Text, CultureInfo.InvariantCulture);
                    st.Push((false, 0, d));
                }
                else
                {
                    long li = long.Parse(t.Text, CultureInfo.InvariantCulture);
                    st.Push((true, li, li));
                }
                continue;
            }
            if (t.Type == TokType.Op)
            {
                if (t.Text == "u+" || t.Text == "u-")
                {
                    if (st.Count < 1) throw new ArgumentException("オペランド不足 (unary).");
                    var a = st.Pop();
                    if (t.Text == "u+")
                    {
                        st.Push(a);
                    }
                    else // u-
                    {
                        if (a.isInt) st.Push((true, -a.iVal, -a.dVal));
                        else st.Push((false, 0, -a.dVal));
                    }
                    continue;
                }
                if (st.Count < 2) throw new ArgumentException("オペランド不足 (binary).");
                var b = st.Pop();
                var a2 = st.Pop();
                bool bothInt = a2.isInt && b.isInt;
                switch (t.Text)
                {
                    case "+":
                        if (bothInt) st.Push((true, a2.iVal + b.iVal, a2.iVal + b.iVal));
                        else st.Push((false, 0, a2.dVal + b.dVal));
                        break;
                    case "-":
                        if (bothInt) st.Push((true, a2.iVal - b.iVal, a2.iVal - b.iVal));
                        else st.Push((false, 0, a2.dVal - b.dVal));
                        break;
                    case "*":
                        if (bothInt) st.Push((true, a2.iVal * b.iVal, a2.iVal * b.iVal));
                        else st.Push((false, 0, a2.dVal * b.dVal));
                        break;
                    case "/":
                        st.Push((false, 0, a2.dVal / b.dVal));
                        break;
                    case "//":
                        if (!bothInt)
                        {
                            long la = (long)a2.dVal;
                            long lb = (long)b.dVal;
                            if (lb == 0) throw new DivideByZeroException();
                            st.Push((true, la / lb, (double)(la / lb)));
                        }
                        else
                        {
                            if (b.iVal == 0) throw new DivideByZeroException();
                            st.Push((true, a2.iVal / b.iVal, (double)(a2.iVal / b.iVal)));
                        }
                        break;
                    case "%":
                        if (bothInt)
                        {
                            if (b.iVal == 0) throw new DivideByZeroException();
                            st.Push((true, a2.iVal % b.iVal, a2.iVal % b.iVal));
                        }
                        else
                        {
                            st.Push((false, 0, a2.dVal % b.dVal));
                        }
                        break;
                    case "^":
                        {
                            double res = Math.Pow(a2.dVal, b.dVal);
                            st.Push((false, 0, res));
                            break;
                        }
                    default:
                        throw new ArgumentException($"未サポートの演算子: {t.Text}");
                }
            }
        }
        if (st.Count != 1) throw new ArgumentException("式の評価失敗。");
        var final = st.Pop();
        return final.dVal;
    }
}
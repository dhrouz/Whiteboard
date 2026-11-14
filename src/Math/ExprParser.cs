// ExprParser: tiny expression compiler for function plotting.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WhiteboardApp
{
    delegate double MathFn(double x);

    class ExprParser
    {
        string s; int p;

        public static MathFn Compile(string src)
        {
            try
            {
                string t = (src ?? "").Replace("\u03c0", "pi").Replace("\u00d7", "*").Replace("\u00f7", "/").Replace("\u2212", "-").Replace("\u00b2", "^2").Replace("\u00b3", "^3").Replace(" ", "").ToLowerInvariant();
                if (t.StartsWith("y=")) t = t.Substring(2);
                else if (t.StartsWith("f(x)=")) t = t.Substring(5);
                if (t.Length == 0) return null;
                ExprParser ps = new ExprParser();
                ps.s = t; ps.p = 0;
                MathFn f = ps.ParseExpr();
                if (ps.p != ps.s.Length) return null;
                f(1.0);
                return f;
            }
            catch (Exception) { return null; }
        }

        char Peek() { return p < s.Length ? s[p] : '\0'; }

        MathFn ParseExpr()
        {
            MathFn left = ParseTerm();
            while (true)
            {
                char c = Peek();
                if (c == '+') { p++; MathFn l = left; MathFn r = ParseTerm(); left = delegate (double x) { return l(x) + r(x); }; }
                else if (c == '-') { p++; MathFn l = left; MathFn r = ParseTerm(); left = delegate (double x) { return l(x) - r(x); }; }
                else break;
            }
            return left;
        }

        MathFn ParseTerm()
        {
            MathFn left = ParseUnary();
            while (true)
            {
                char c = Peek();
                if (c == '*') { p++; MathFn l = left; MathFn r = ParseUnary(); left = delegate (double x) { return l(x) * r(x); }; }
                else if (c == '/') { p++; MathFn l = left; MathFn r = ParseUnary(); left = delegate (double x) { return l(x) / r(x); }; }
                else if (char.IsDigit(c) || c == '.' || c == '(' || char.IsLetter(c)) { MathFn l = left; MathFn r = ParseUnary(); left = delegate (double x) { return l(x) * r(x); }; }
                else break;
            }
            return left;
        }

        MathFn ParseUnary()
        {
            char c = Peek();
            if (c == '-') { p++; MathFn f = ParseUnary(); return delegate (double x) { return -f(x); }; }
            if (c == '+') { p++; return ParseUnary(); }
            return ParsePower();
        }

        MathFn ParsePower()
        {
            MathFn b = ParseAtom();
            if (Peek() == '^') { p++; MathFn e = ParseUnary(); return delegate (double x) { return Math.Pow(b(x), e(x)); }; }
            return b;
        }

        MathFn ParseAtom()
        {
            char c = Peek();
            if (c == '(')
            {
                p++; MathFn e = ParseExpr();
                if (Peek() != ')') throw new Exception("paren");
                p++; return e;
            }
            if (char.IsDigit(c) || c == '.')
            {
                int st = p;
                while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.')) p++;
                double v = double.Parse(s.Substring(st, p - st), CultureInfo.InvariantCulture);
                return delegate (double x) { return v; };
            }
            if (char.IsLetter(c))
            {
                int st = p;
                while (p < s.Length && char.IsLetter(s[p])) p++;
                string id = s.Substring(st, p - st);
                if (id == "log" && p + 1 < s.Length && s[p] == '1' && s[p + 1] == '0') { p += 2; id = "log10"; }
                if (id == "x") return delegate (double x) { return x; };
                if (id == "e") return delegate (double x) { return Math.E; };
                if (id == "pi") return delegate (double x) { return Math.PI; };
                if (Peek() != '(') throw new Exception("fn");
                p++;
                MathFn a = ParseExpr();
                if (Peek() != ')') throw new Exception("paren");
                p++;
                switch (id)
                {
                    case "sin": return delegate (double x) { return Math.Sin(a(x)); };
                    case "cos": return delegate (double x) { return Math.Cos(a(x)); };
                    case "tan": return delegate (double x) { return Math.Tan(a(x)); };
                    case "asin": return delegate (double x) { return Math.Asin(a(x)); };
                    case "acos": return delegate (double x) { return Math.Acos(a(x)); };
                    case "atan": return delegate (double x) { return Math.Atan(a(x)); };
                    case "sqrt": return delegate (double x) { return Math.Sqrt(a(x)); };
                    case "abs": return delegate (double x) { return Math.Abs(a(x)); };
                    case "ln": return delegate (double x) { return Math.Log(a(x)); };
                    case "log": return delegate (double x) { return Math.Log10(a(x)); };
                    case "log10": return delegate (double x) { return Math.Log10(a(x)); };
                    case "exp": return delegate (double x) { return Math.Exp(a(x)); };
                    case "floor": return delegate (double x) { return Math.Floor(a(x)); };
                    case "ceil": return delegate (double x) { return Math.Ceiling(a(x)); };
                }
                throw new Exception("unknown function");
            }
            throw new Exception("syntax");
        }
    }
}

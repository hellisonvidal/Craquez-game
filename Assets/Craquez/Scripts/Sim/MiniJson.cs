using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Craquez.Sim
{
    /// <summary>
    /// Leitor/escritor JSON mínimo (objetos -> Dictionary&lt;string, object&gt;,
    /// listas -> List&lt;object&gt;, números -> double). O JsonUtility do Unity não
    /// lê dicionários com chaves livres, que o banco de ligas e o option file usam.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON vazio");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i);
                i++; // :
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // }
                return d;
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ]
                return l;
            }
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            i++;
            return sb.ToString();
        }

        public static string Write(object v, bool pretty = true)
        {
            var sb = new StringBuilder();
            W(sb, v, pretty, 0);
            return sb.ToString();
        }

        static void W(StringBuilder sb, object v, bool pretty, int ind)
        {
            string nl = pretty ? "\n" + new string(' ', ind * 2) : "";
            string nl2 = pretty ? "\n" + new string(' ', (ind + 1) * 2) : "";
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string str:
                    sb.Append('"');
                    foreach (char c in str)
                    {
                        if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                        else if (c == '\n') sb.Append("\\n");
                        else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                    }
                    sb.Append('"');
                    break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case IDictionary<string, object> d:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in d)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(nl2);
                        W(sb, kv.Key, pretty, ind + 1);
                        sb.Append(pretty ? ": " : ":");
                        W(sb, kv.Value, pretty, ind + 1);
                    }
                    if (!first) sb.Append(nl);
                    sb.Append('}');
                    break;
                case System.Collections.IList l:
                    sb.Append('[');
                    for (int k = 0; k < l.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        sb.Append(nl2);
                        W(sb, l[k], pretty, ind + 1);
                    }
                    if (l.Count > 0) sb.Append(nl);
                    sb.Append(']');
                    break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double dd: sb.Append(dd.ToString("R", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n); break;
                default: sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        // ---- acesso tolerante
        public static Dictionary<string, object> D(object o, string k)
            => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> L(object o, string k)
            => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as List<object> : null;

        public static string S(object o, string k, string def = "")
            => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : def;

        public static float F(object o, string k, float def = 0f)
            => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is double d ? (float)d : def;

        public static int I(object o, string k, int def = 0)
            => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is double d ? (int)d : def;
    }
}

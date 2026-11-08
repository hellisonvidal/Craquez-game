using System; using System.IO; using System.Reflection; using System.Linq;
public static class R { public static int Main(string[] a) {
  Craquez.Sim.Teams.SourceOverride = File.ReadAllText(a[0]);
  var asm = Assembly.LoadFrom("Tests.dll"); int fail = 0, ok = 0;
  foreach (var t in asm.GetTypes()) foreach (var m in t.GetMethods()) {
    var cases = m.GetCustomAttributes().Where(x => x.GetType().Name == "TestCaseAttribute").ToList();
    bool isTest = m.GetCustomAttributes().Any(x => x.GetType().Name == "TestAttribute");
    var argsets = cases.Count > 0 ? cases.Select(c => (object[])c.GetType().GetProperty("Arguments").GetValue(c)).ToList() : (isTest ? new System.Collections.Generic.List<object[]>{ null } : null);
    if (argsets == null) continue;
    var only = Environment.GetEnvironmentVariable("ONLY");
    if (!string.IsNullOrEmpty(only) && !m.Name.Contains(only)) continue;
    foreach (var args in argsets) {
      try { m.Invoke(Activator.CreateInstance(t), args); ok++; Console.WriteLine("OK   " + m.Name + (args != null ? "(" + string.Join(",", args) + ")" : "")); }
      catch (TargetInvocationException e) { fail++; Console.WriteLine("FAIL " + m.Name + ": " + e.InnerException.Message.Split('\n')[0]); }
    } }
  Console.WriteLine($"{ok} ok, {fail} falhas"); return fail; } }

using System.Management.Automation;
using System.Management.Automation.Language;
// Перевірка збирача: синтаксис і логіка, яку можна виконати без Windows.
var file = args[0];
var ast = Parser.ParseFile(file, out _, out var errors);
foreach (var e in errors) Console.WriteLine($"ПОМИЛКА {e.Extent.StartLineNumber}: {e.Message}");
if (errors.Length > 0) return 1;
var funcs = ast.FindAll(a => a is FunctionDefinitionAst, true).Cast<FunctionDefinitionAst>().Select(f => f.Extent.Text);
var script = string.Join("\n", funcs) + "\n" + File.ReadAllText(args[1]);
using var ps = PowerShell.Create();
ps.AddScript(script);
try { foreach (var o in ps.Invoke()) Console.WriteLine(o); }
catch (Exception ex) { Console.WriteLine("ВИНЯТОК: " + ex.Message); return 2; }
foreach (var e in ps.Streams.Error) Console.WriteLine("ПОМИЛКА: " + e);
foreach (var i in ps.Streams.Information) Console.WriteLine(i);
// Перехоплені в самій перевірці винятки (негативні випадки) – не помилка; помилка – лише те, що дійшло до потоку помилок.
return ps.Streams.Error.Count > 0 ? 3 : 0;

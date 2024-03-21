// See https://aka.ms/new-console-template for more information

using Command;

try
{
    using var file = File.OpenText("./../../../cmdTest.txt");
    var cmds = CMD.ToCMDs(file);

    foreach (var c in cmds) Console.WriteLine(c);

    // var cmd = new CMD();
    // var s = "command 01 #t t1 \"\\ns\" ";
    // Console.WriteLine(CMD.AnalyzeSyntax(s, new ColorFormatter()).HighlightedCommand);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

public class ColorFormatter : CMD.IColorFormatter
{
    public string ColorTail() => "[/color]";
    public string ColorHead(CMD.IColorFormatter.ColorType type) => $"[color = {type}]";
}
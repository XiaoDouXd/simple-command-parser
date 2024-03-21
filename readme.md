# Simple Command Parser

#### Command format

```
command [params ...] [[#paramName namedParams ...] ...]
```

#### Detailed rules

##### Single line

`command` is the command's name which is a string located at the forefront of the command, **and** does not contain any space-like char, **and** not start with **'#'** or **'"'**;
`params` is the value of params. it's a string that does not start with **'#'**;
`#paramName` is the name of params. A paramName can hold lot of params, which is separated by space, also we can write params with space by enclosed with **'"'**. And the escaping is suported in the string encloed with **'"'**.

```
command 0 2.1 #paramName 20000129 3.14 str "str with space" #t "2023/06/09\t\"16:46\""\n"
```

If the a param is defined before the first #paramName, it would be  regarded as a **DirParams**.

```
command 20240321 chengdu #event resignation
```

The params in a empty paramName would be discarded. 

```
command 长太息以掩涕兮 # discarded "alse would be discarded" #t aaa 2.71828
```

If encounter a same paramName, the new param will overwrite the old ones.

```
command abc 0.618 #t be overwrited #t new params xxx
```

The dirParam which is matched before the command will be discarded.

```
"be discared str" cmd #p xxx
```

If no any valid command can be found before the first paramName, the command would be `string.Empty`

```
"str" #t aaa bbb ccc "ddd"
```

##### Multiple line

You can concat two line by using **'\\'** in **the END of** line, and write line-end comments using **"//"**. When a line can not be parse to any part of command, it wound be discarded, and it would not interrupt the concatenating.

```
command paramA #paramName paramB\
    paramC \
    // this line is not any part of command.
    // so it will not interrupt the concatenating.
    paramD \
paramE
```

-----

#### Use Cases

```c#
// parse single command
var cmd = new CMD("command 20240321 chengdu #event resignation 01 0xFF");

var cmdName = cmd.Command; 			// string: command
var param1 = cmd.DirParams[0]; 		// string: 20240321
var param2 = cmd.DirParams[1]; 		// string: chengdu
var paramOfEvent = cmd["event"][0] 	// string: resignation
    
var param1Int = cmd.Int(0);			// int: 20240321
var paramOfEventInt = cmd.Int("event", 2)	// int: 0xFF
var srcStr = cmd.ToString();		// string: command "20240321" "chengdu" #event "resignation" "01" "0xFF"

// parse multiple line command
var cmds = CMD.ToCMDs(file);		// cmd=>List<CMD>; file=>StreanReader
```

#### Util

Here also provide a simple syntax highlighting and checking tool. You can use it by inheriting the **CMD.IColorFormatter** interface, and put it in the **CMD.AnalyzeSyntax** function.

```c#
public class ColorFormatter : CMD.IColorFormatter
{
    public string ColorTail() => "[/color]";
    public string ColorHead(CMD.IColorFormatter.ColorType type) => $"[color = {type}]";
}
```

```c#
var s = "command 01 #t t1 \"\\ns\" ";
Console.WriteLine(CMD.AnalyzeSyntax(s, new ColorFormatter()).HighlightedCommand);
// output:
// [color = Cmd]command [/color][color = Param]01 [/color][color = ParamName]#t [/color][color = Param]t1 [/color][color = String]"[color = EscapeChar]\n[/color]s"[/color]
```
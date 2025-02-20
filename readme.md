# Simple Command Parser

#### Command 格式

```
command [params ...] [[#paramName namedParams ...] ...]
```

#### 语法规则

##### 单行

`command` 为命令名，命令名不包含任何空格字符或 **'#'** 或 **'"'** 字符。
`params` 为参数：

- `#paramName` 是参数名，其以 **'#'** 字符串开头，在其后可以跟所多个参数，参数间使用空格分隔，对于需要包含空格的字符串参数，我们也可以使用 **'"'** 将其包围：

```
command 0 2.1 #paramName 20000129 3.14 str "str with space" #t "2023/06/09\t\"16:46\""\n"
```

- 定义在第一个参数名前的参数会被计入 DirParam（直接参数）列表中：

```
command 20240321 chengdu #event resignation
```

- 如果一个参数列表的参数名为空，则其后的参数会被丢弃：

```
command 长太息以掩涕兮 # discarded "alse would be discarded" #t aaa 2.71828
```

- 如果定义了两个相同的参数名，后面参数名所跟随的参数列表会将前面的覆盖：

```
command abc 0.618 #t be overwrited #t new params xxx
```

- 在命令名被捕获到前所捕获到的“直接参数”会被丢弃：

```
"be discared str" cmd #p xxx
```

- 如果一条命令没有任何可以捕获的命令名，则命令名为 `string.Empty`：

```
"str" #t aaa bbb ccc "ddd"
```

##### 多行

可以在**行尾** 使用 **'\\'** 来连接多行指令，可以使用 **"//"** 来书写行尾注释。当一行中的内容无法被解析成命令的任何部分，它会被丢弃而不是中断连接。

```
command paramA #paramName paramB\
    paramC \
    // this line is not any part of command.
    // so it will not interrupt the concatenating.
    paramD \
paramE
```

-----

#### 使用例

```c#
// parse single command
var cmd = new CMD("command 20240321 chengdu #event resignation 01 0xFF");

var cmdName = cmd.Command; 			// string: command
var param1 = cmd.DirParams[0]; 		// string: 20240321
var param2 = cmd.DirParams[1]; 		// string: chengdu
var paramOfEvent = cmd["event"][0] 	// string: resignation
    
var param1Int = cmd.Int(0);			// int: 20240321
var paramOfEventInt = cmd.Int("event", 2)	// int: 0xFF
var srcStr = cmd.ToString();		// string: command 20240321 chengdu #event resignation 01 0xFF

// parse multiple line command
var cmds = CMD.ToCMDs(file);		// cmd: List<CMD>; file: StreamReader
```

#### 分析工具

这里提供了一个简易的语法检查和高亮的工具类。你可以继承 **CMD.IColorFormatter**  接口，并将其放入 **CMD.AnalyzeSyntax** 方法中：

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
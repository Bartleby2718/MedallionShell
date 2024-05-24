@madelson Creating another thread for discussion.

1. If I run `get-command npm` on PowerShell, I get:
```
CommandType     Name                                               Version    Source
-----------     ----                                               -------    ------
Application     npm.cmd                                            0.0.0.0    C:\Program Files\nodejs\npm.cmd
```

Because `C:\Program Files\nodejs\` is on my PATH environment variable, it makes sense that `npm --version` runs successfully on PowerShell and on command line prompt.

2. If I run `where npm` on command line prompt, I get:
```
C:\Program Files\nodejs\npm
C:\Program Files\nodejs\npm.cmd
```

Therefore, it makes sense that `"C:\Program Files\nodejs\npm" --version` (on command line prompt) correctly returns the version of npm installed.

3. However, it doesn't make sense to me that I cannot reproduce this in .NET. In theory, I *should* be able to run `npm --version` by running the following C# code:
```cs
Process.Start(new ProcessStartInfo
{
	FileName = @"C:\Program Files\nodejs\npm",
	Arguments = "--version",
	WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows), // added for reproducibility
}).StandardOutput
```

However, I get an error:
> Win32Exception: An error occurred trying to start process 'C:\Program Files\nodejs\npm' with working directory 'C:\WINDOWS'. The specified executable is not a valid application for this OS platform.

4. ChatGPT let me know that this is because `C:\Program Files\nodejs\npm` is actually a shell script. As a matter of fact, it starts with:
```sh
#!/usr/bin/env bash

# This is used by the Node.js installer, which expects the cygwin/mingw
# shell script to already be present in the npm dependency folder.
```

5. ChatGPT also explained why this works even though the file doesn't end with one of the extensions in the `PATHEXT` environment variable.
> When you install Git Bash, it adds a layer that allows Windows to understand shebangs to some extent. When you run a script through Git Bash, it will look at the shebang and use the appropriate interpreter.
> 
> So, in your case, when you run npm in Git Bash, it sees the #!/usr/bin/env bash shebang and knows to use bash to interpret the script. But when you try to run npm directly in Windows (like in your C# code), Windows doesn’t know what to do with it because it doesn’t have a .cmd or .exe extension.

I'm inclined towards opting out of this additional Git Bash logic by using only the files with the extension that's in the `PATHEXT` environment variable (e.g. `.exe`, `.bat`, `.cmd`). That'll still allow `C:\Program Files\nodejs\npm.cmd` to be picked up and run `npm --version` successfully. Let me know if
# Unity MCP helpers (for the assistant)

The editor's MCP for Unity session runs its own HTTP server on `http://127.0.0.1:8080/mcp`.
When the assistant's MCP client is on stdio it sees "no Unity instances", so these scripts
talk to 8080 directly. In Unity first: *Window > MCP for Unity > Start Session*.

```powershell
# list tools
.\Tools\UnityMcp\mcp.ps1 list
# call any tool with a JSON arguments file
.\Tools\UnityMcp\mcp.ps1 read_console args.json        # {"action":"get","types":["error"],"count":"20"}
.\Tools\UnityMcp\mcp.ps1 manage_editor play.json       # {"action":"play"}  / {"action":"stop"}
.\Tools\UnityMcp\mcp.ps1 refresh_unity refresh.json    # {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}
# run a C# snippet (method body, `return` a string) inside the editor
.\Tools\UnityMcp\run_cs.ps1 snippet.cs
```

Play mode only advances while the editor is focused; from automation run
`Application.runInBackground = true;` (via run_cs) after entering Play.
Menu items that open a DisplayDialog block the editor - call the method behind them instead.

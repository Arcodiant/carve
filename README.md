# Carve

Carve is a 3D modelling tool for Claude agents, served over MCP. An agent
defines a mesh by coordinates and looks at it through offscreen renders.

## Use

Add this to the `.mcp.json` of the project whose agent should model (change the
path to where this repo is checked out):

```json
{
  "mcpServers": {
    "carve": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/carve/src/Carve.Server", "--no-launch-profile"]
    }
  }
}
```

It needs the .NET SDK, the Empyrean package feed (the local feed `empyrean-local` in
`NuGet.config`, `http://localhost:5555/v3/index.json`; without it `dotnet run` cannot restore),
and a Vulkan device (a GPU with a current driver); there
is no window, every look is an offscreen render. If no device is found the
server stays up and the `render` tool says so.

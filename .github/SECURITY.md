# Security policy

Carve is a local tool: an MCP server that an agent on your own machine starts
over stdio. It opens no network port, and it is not a sandbox: it runs with your
permissions and reads and writes whatever files the agent names (a relative path
is taken from the folder it was started in, an absolute path is used as given),
writes a log to the temp folder, and loads and runs the extension assemblies its
configuration lists.

## Reporting a vulnerability

Please report security problems privately through GitHub's
**Security → Report a vulnerability** on this repository, not in a public issue.
Include what you ran, what happened, and the Carve commit you ran it on.

Only the latest commit on `main` is supported; fixes land there.

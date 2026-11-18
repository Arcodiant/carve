# Contributing to Carve

Thanks for your interest. Carve is a small project with one maintainer, so please
**open an issue before a pull request** for anything beyond a typo or a small fix;
that way we can agree on the change before you spend time on it.

## Building and testing

You need the .NET 10 SDK. Every package comes from nuget.org.

```
dotnet build Carve.slnx
dotnet test tests/Carve.Core.Tests --filter "Tier!=regression"
dotnet test tests/Carve.Server.Tests --filter "Tier!=regression"
```

The build treats warnings as errors. The regression tier and the scenarios under
`scenarios/` render through Vulkan and need a GPU; `tests.json` lists every suite
and its command, and `scripts/verify.ps1` runs the full check. CI runs only the
two unit suites above, so say in your pull request whether you ran the GPU suites.

## Conventions

- The engine version is pinned once, as `EmpyreanVersion` in
  `Directory.Build.props`, never as a literal in a `.csproj`.
- Add tests with the change: a unit test for kernel and tool behaviour, and a
  scenario for anything a render should show.
- `docs/` is the project's work-item tracker (tasks, bugs, milestones, design
  docs, reviews). You don't need to edit it; the maintainer records your change
  there.

By contributing you agree that your contribution is licensed under the
repository's MIT license.

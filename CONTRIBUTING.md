# Contributing

Thanks for helping make borderlessMax better!

- **Found a bug or want a feature?** Open an [issue](../../issues/new/choose); the form asks for what's needed.
- **Question or idea?** Start a [discussion](../../discussions).
- **Security issue?** Please don't open an issue, see [SECURITY.md](SECURITY.md).

## Pull requests

1. Fork the repo and create a branch from `main`.
2. Run it locally (needs the [.NET 10 SDK](https://dotnet.microsoft.com/download)):
   ```
   dotnet run
   dotnet run -- --selftest   # logic checks, exit code 0 = all passed
   ```
3. Keep changes small and focused, and match the style of the surrounding code.
4. Open a pull request against `main`. CI builds, self-tests and smoke-tests the app; it must be green before the PR
   can be merged. Builds for pull requests from forks start once a maintainer approves them.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).

# Codebase Finder for Flow Launcher

Quickly find and open your codebases in VS Code or Cursor using [Everything](https://www.voidtools.com/) search.

## Requirements

- [Everything](https://www.voidtools.com/) must be installed and running
- `es.exe` (Everything command-line interface) must be in your PATH or configured in settings

## Usage

Type `code` followed by your search query to find repositories:

```
code myproject
code lang:rust
code
```

Results include:
- Git repositories (`.git` folders)
- VS Code workspace files (`.code-workspace`)

### Context Menu

Right-click on any result to:
- Open in File Explorer
- Rebuild language cache

## Supported Languages

TypeScript, JavaScript, Python, Rust, Go, C#, Java, Kotlin, Ruby, PHP, Swift, Dart, C++, C, Elixir, Shell, Lua

# AI assistant guide

For an overview of what curatarr is, see [`README.md`](README.md).

## Terminology

The two media trees this project reconciles are **source** (the *arr-managed originals) and **destination** (the re-encoded copy the media server serves). Use these names in code, UI, and prose — not "backend" / "frontend".

Those words are fine when discussing the curator app's own architecture (e.g. "the ASP.NET backend", "the Blazor frontend"), just not for media.

## Working with the code

- Solution: `Curatarr.slnx` at the repo root
- Build / run: `dotnet build`, `dotnet run --project src/Curatarr`
- EF Core CLI is installed as a **local** tool — invoke as `dotnet ef ...` from the repo root. Do not install it globally.
- Nullable reference types are enabled across the project.
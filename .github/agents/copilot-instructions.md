# NDI-for-Android Development Guidelines
<!-- Last updated: 2026-06-07 | This app is .NET MAUI — NOT Kotlin/Android Studio -->

> **Start here**: Read `.github/KNOWLEDGE-BASE.md` for the full authoritative reference.
> This file is a quick-start summary only.

## Tech Stack
- **.NET MAUI** targeting `net10.0-android` — C# 12, nullable enabled
- **MVVM**: CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`)
- **DI**: `Microsoft.Extensions.DependencyInjection` via `src/MauiApp/MauiProgram.cs`
- **Persistence**: SQLite-net-pcl (async API only)
- **NDI**: P/Invoke against `libndi.so` (arm64-v8a + armeabi-v7a)
- **Tests**: xUnit 2.x + Moq — `dotnet test tests/MauiApp.Tests`
- **CI**: GitHub Actions — see `.github/workflows/`

## Build & Test Commands
```powershell
dotnet build src/NdiForAndroid.sln    # verify after every task
dotnet test tests/MauiApp.Tests       # must pass before PR merge
```

## Project Structure
```
src/
  Core/        <- Domain contracts: interfaces, models, ViewModels
  MauiApp/     <- MAUI app: views, implementations, DI, Android platform
tests/
  MauiApp.Tests/     <- xUnit unit tests (no native NDI)
  MauiApp.UITests/   <- Appium UI smoke tests
docs/
  constitution.md    <- authoritative tech/architecture decisions
  architecture.md    <- module map and dependency rules
.github/
  KNOWLEDGE-BASE.md  <- consolidated agent reference (read this first!)
```

## Code Style
- C# 12, nullable reference types enabled — no `!` suppressions without a comment
- XAML for all UI — no code-behind logic
- Conventional commits: `feat(<layer>): description` with `Task: T###` and `Issue: #N` trailers

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

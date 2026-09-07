namespace NdiForAndroid.Services;

/// <summary>Auto-accepts every confirmation. Used on build targets with no Shell page to anchor a
/// dialog to (mirrors the other Android/Noop service pairs in <c>MauiProgram</c>).</summary>
internal sealed class NoopUserPromptService : IUserPromptService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Task.FromResult(true);
}

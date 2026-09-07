namespace NdiForAndroid.Services;

/// <summary>
/// Confirms via <see cref="Page.DisplayAlert(string, string, string, string)"/> on the current
/// Shell page. <c>Page.DisplayAlert</c> is cross-platform MAUI Essentials, not an Android-specific
/// API, so — like <see cref="MauiScreenReaderAnnouncer"/> would — this lives beside
/// DefaultMainThreadDispatcher rather than under Platforms/Android (#347).
/// </summary>
internal sealed class MauiUserPromptService : IUserPromptService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var tcs = new TaskCompletionSource<bool>();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var page = Shell.Current?.CurrentPage;
                if (page is null)
                {
                    // No page to anchor the dialog to (e.g. very early in startup) — err on the
                    // side of NOT performing the destructive action the caller is guarding.
                    tcs.TrySetResult(false);
                    return;
                }

                var confirmed = await page.DisplayAlert(title, message, accept, cancel);
                tcs.TrySetResult(confirmed);
            }
            catch (Exception ex)
            {
                // A prompt that can't be shown must never crash the app or silently perform the
                // destructive action it was meant to guard.
                System.Diagnostics.Debug.WriteLine($"Confirm prompt failed: {ex.Message}");
                tcs.TrySetResult(false);
            }
        });

        return tcs.Task;
    }
}

namespace NdiForAndroid.Services;

/// <summary>
/// Modal yes/no confirmation shown to the user before a destructive, irreversible action commits
/// (e.g. deleting a configured discovery server, #347). ViewModels stay MAUI-free by depending on
/// this rather than <c>Page.DisplayAlert</c> directly — mirrors the <see cref="IMainThreadDispatcher"/>
/// seam.
/// </summary>
public interface IUserPromptService
{
    /// <summary>
    /// Shows a confirmation dialog and returns <c>true</c> when the user picked
    /// <paramref name="accept"/>, <c>false</c> for <paramref name="cancel"/> (including the dialog
    /// being dismissed without an explicit choice).
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

/// <summary>
/// Records every confirmation request and returns a preset response — for unit tests. Defaults to
/// accepting (<see cref="NextResult"/> = <c>true</c>) so a test that does not care about the prompt
/// still exercises the accepted path.
/// </summary>
public sealed class FakeUserPromptService : IUserPromptService
{
    public bool NextResult { get; set; } = true;

    public List<(string Title, string Message, string Accept, string Cancel)> Requests { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Requests.Add((title, message, accept, cancel));
        return Task.FromResult(NextResult);
    }
}

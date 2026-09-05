namespace ViscaMockCamera;

internal sealed class ViscaResult
{
    public ViscaResult(string description, params byte[][] replies)
    {
        Description = description;
        Replies = replies;
    }

    public string Description { get; }

    public byte[][] Replies { get; }
}

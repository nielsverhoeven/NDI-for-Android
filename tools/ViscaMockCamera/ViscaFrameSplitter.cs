namespace ViscaMockCamera;

internal static class ViscaFrameSplitter
{
    public static IEnumerable<byte[]> Extract(List<byte> buffer)
    {
        while (true)
        {
            var terminatorIndex = buffer.IndexOf(0xFF);
            if (terminatorIndex < 0)
            {
                yield break;
            }

            var frame = buffer.GetRange(0, terminatorIndex + 1).ToArray();
            buffer.RemoveRange(0, terminatorIndex + 1);
            yield return frame;
        }
    }
}

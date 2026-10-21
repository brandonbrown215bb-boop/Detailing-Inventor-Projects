namespace UnitConstructionVerifier.Extraction
{
    /// <summary>Decodes Inventor attribute values, which may be stored as byte arrays or strings.</summary>
    public static class AttributeText
    {
        public static string Decode(object? value)
        {
            if (value is byte[] bytes)
                return System.Text.Encoding.UTF8.GetString(bytes);

            if (value is System.Array arr)
            {
                byte[] byteArr = new byte[arr.Length];
                System.Array.Copy(arr, byteArr, arr.Length);
                return System.Text.Encoding.UTF8.GetString(byteArr);
            }

            return value?.ToString() ?? string.Empty;
        }
    }
}

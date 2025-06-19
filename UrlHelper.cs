public class UrlHelper
{
    public static string ExtractClient(string url)
    {
        const string prefix = "--";

        // Busca la posición del prefijo en la URL
        int startIndex = url.IndexOf(prefix);
        if (startIndex == -1) return ""; // No encontró el prefijo

        // Salta el prefijo
        startIndex += prefix.Length;

        // Busca el siguiente "/" después del prefijo para delimitar el final del cliente
        int endIndex = url.IndexOf("/", startIndex);
        if (endIndex == -1) endIndex = url.Length;

        // Extrae el nombre del cliente
        string client = url.Substring(startIndex, endIndex - startIndex);

        return client;
    }
}

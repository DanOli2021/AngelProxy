public class UrlHelper
{
    public static string ExtractClient(string url)
    {
        // Busca la posición del prefijo "-cliente" en la URL
        int startIndex = url.IndexOf("-") + 1;

        if (startIndex <= 0) return ""; // Si no encuentra el prefijo, regresa null

        // Busca el siguiente "/" después del prefijo para encontrar el final del cliente
        int endIndex = url.IndexOf("/", startIndex);

        // Si no hay "/" después del prefijo, toma el final del string
        if (endIndex == -1) endIndex = url.Length;

        // Extrae el nombre del cliente entre el prefijo y el "/"
        string client = url.Substring(startIndex, endIndex - startIndex);

        return client;
    }
}
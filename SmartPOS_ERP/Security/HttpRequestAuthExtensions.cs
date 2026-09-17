namespace SmartPOS_ERP.Security
{
    public static class HttpRequestAuthExtensions
    {
        public static bool IsJsonRequest(this HttpRequest request)
        {
            var contentType = request.ContentType ?? string.Empty;
            var accept = request.Headers.Accept.ToString();
            return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}

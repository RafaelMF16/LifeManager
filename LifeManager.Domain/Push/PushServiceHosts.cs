namespace LifeManager.Domain.Push
{
    /// <summary>
    /// The push services a subscription may point to. The server posts to the endpoint the browser handed over, so
    /// anything else (an internal address, someone's webhook) is refused: otherwise the reminders job would be a way to
    /// make the server call arbitrary URLs.
    /// </summary>
    public static class PushServiceHosts
    {
        /// <summary>Exact hosts: Chrome/Edge-on-Android/Opera (FCM) and Safari.</summary>
        private static readonly string[] Hosts = ["fcm.googleapis.com", "web.push.apple.com"];

        /// <summary>Domains whose subdomains are the service's: Firefox, Apple and Windows (Edge).</summary>
        private static readonly string[] Domains = ["push.services.mozilla.com", "push.apple.com", "notify.windows.com"];

        public static bool IsAllowed(string? endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > PushSubscription.EndpointMaxLength)
                return false;

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.IsDefaultPort
                || uri.UserInfo.Length > 0)
                return false;

            var host = uri.IdnHost.ToLowerInvariant();

            return Hosts.Contains(host)
                || Domains.Any(domain => host.EndsWith("." + domain, StringComparison.Ordinal));
        }
    }
}

using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services
{
    public class UserSessionInfo
    {
        public string SessionToken { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    }

    public interface ISessionManager
    {
        UserSessionInfo CreateSession(User user, HttpContext httpContext);
        UserSessionInfo? GetCurrentSession(HttpContext httpContext);
        void EndSession(HttpContext httpContext);
        bool IsInRole(HttpContext httpContext, string roleName);
    }

    public class SessionManager : ISessionManager
    {
        private static readonly ConcurrentDictionary<string, UserSessionInfo> _sessions = new();

        public const string SessionTokenKey = "SessionToken";
        public const string SessionUserIdKey = "UserId";
        public const string SessionUserNameKey = "UserName";
        public const string SessionUserEmailKey = "UserEmail";
        public const string SessionUserRoleKey = "UserRole";
        public const string SessionRoleIdKey = "RoleId";

        public const string HeaderTokenKey = "X-Session-Token";

        public UserSessionInfo CreateSession(User user, HttpContext httpContext)
        {
            var token = Guid.NewGuid().ToString("N");
            var roleName = user.Role?.RoleName ?? (user.RoleID == 1 ? "Organiser" : "Participant");

            var sessionInfo = new UserSessionInfo
            {
                SessionToken = token,
                UserID = user.UserID,
                Name = user.Name,
                Email = user.Email,
                RoleID = user.RoleID,
                RoleName = roleName,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _sessions[token] = sessionInfo;

            // Store in server-side HttpContext.Session
            try
            {
                httpContext.Session.SetString(SessionTokenKey, token);
                httpContext.Session.SetInt32(SessionUserIdKey, user.UserID);
                httpContext.Session.SetString(SessionUserNameKey, user.Name);
                httpContext.Session.SetString(SessionUserEmailKey, user.Email);
                httpContext.Session.SetString(SessionUserRoleKey, roleName);
                httpContext.Session.SetInt32(SessionRoleIdKey, user.RoleID);
            }
            catch
            {
                // In some test scenarios HttpContext.Session may not be configured,
                // the concurrent dictionary provides fallback.
            }

            return sessionInfo;
        }

        public UserSessionInfo? GetCurrentSession(HttpContext httpContext)
        {
            // 1. Check HTTP Header first (ideal for Swagger and API clients)
            if (httpContext.Request.Headers.TryGetValue(HeaderTokenKey, out var headerToken) &&
                !string.IsNullOrWhiteSpace(headerToken))
            {
                if (_sessions.TryGetValue(headerToken.ToString(), out var headerSession))
                {
                    headerSession.LastAccessedAt = DateTime.UtcNow;
                    return headerSession;
                }
            }

            // Also support standard Bearer token header
            if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authStr = authHeader.ToString();
                if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var bearerToken = authStr.Substring(7).Trim();
                    if (_sessions.TryGetValue(bearerToken, out var bearerSession))
                    {
                        bearerSession.LastAccessedAt = DateTime.UtcNow;
                        return bearerSession;
                    }
                }
            }

            // 2. Check HttpContext.Session (server-side session cookie)
            try
            {
                var sessionToken = httpContext.Session.GetString(SessionTokenKey);
                if (!string.IsNullOrEmpty(sessionToken) && _sessions.TryGetValue(sessionToken, out var sessionFromStore))
                {
                    sessionFromStore.LastAccessedAt = DateTime.UtcNow;
                    return sessionFromStore;
                }

                var userId = httpContext.Session.GetInt32(SessionUserIdKey);
                var role = httpContext.Session.GetString(SessionUserRoleKey);
                var name = httpContext.Session.GetString(SessionUserNameKey);
                var email = httpContext.Session.GetString(SessionUserEmailKey);
                var roleId = httpContext.Session.GetInt32(SessionRoleIdKey) ?? 0;

                if (userId.HasValue && !string.IsNullOrEmpty(role))
                {
                    return new UserSessionInfo
                    {
                        SessionToken = sessionToken ?? Guid.NewGuid().ToString("N"),
                        UserID = userId.Value,
                        Name = name ?? string.Empty,
                        Email = email ?? string.Empty,
                        RoleID = roleId,
                        RoleName = role
                    };
                }
            }
            catch
            {
                // Fallback handled
            }

            return null;
        }

        public void EndSession(HttpContext httpContext)
        {
            var session = GetCurrentSession(httpContext);
            if (session != null)
            {
                _sessions.TryRemove(session.SessionToken, out _);
            }

            try
            {
                httpContext.Session.Clear();
            }
            catch
            {
            }
        }

        public bool IsInRole(HttpContext httpContext, string roleName)
        {
            var session = GetCurrentSession(httpContext);
            if (session == null)
            {
                return false;
            }

            return string.Equals(session.RoleName, roleName, StringComparison.OrdinalIgnoreCase);
        }
    }
}

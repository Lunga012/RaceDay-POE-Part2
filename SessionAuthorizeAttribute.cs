using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Services;

namespace RaceDay.Api.Attributes
{
    /// <summary>
    /// Enforces session-based authentication and role-based access control.
    /// Returns 401 Unauthorized if not authenticated.
    /// Returns 403 Forbidden if user lacks the required role.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public string[] AllowedRoles { get; }

        public SessionAuthorizeAttribute(params string[] roles)
        {
            AllowedRoles = roles ?? Array.Empty<string>();
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var sessionManager = context.HttpContext.RequestServices.GetRequiredService<ISessionManager>();
            var session = sessionManager.GetCurrentSession(context.HttpContext);

            if (session == null)
            {
                context.Result = new ObjectResult(new
                {
                    error = "Unauthorized",
                    message = "Authentication required. Please log in using /api/auth/login to establish a session."
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            // Stash user session in HttpContext.Items for controller access
            context.HttpContext.Items["UserSession"] = session;

            // Check role if specified
            if (AllowedRoles.Length > 0)
            {
                var hasRole = AllowedRoles.Any(r => string.Equals(r, session.RoleName, StringComparison.OrdinalIgnoreCase));
                if (!hasRole)
                {
                    context.Result = new ObjectResult(new
                    {
                        error = "Forbidden",
                        message = $"Access denied. This endpoint requires one of the following roles: {string.Join(", ", AllowedRoles)}. Your current role is '{session.RoleName}'."
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                    return;
                }
            }
        }
    }
}

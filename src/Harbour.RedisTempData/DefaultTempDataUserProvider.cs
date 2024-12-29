#region usings

using System;
using System.Web;
using System.Web.Mvc;
using System.Web.SessionState;

#endregion

namespace Harbour.RedisTempData
{
    /// <summary>
    ///     Provides a default mechanism for identifying the current user.
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class DefaultTempDataUserProvider : ITempDataUserProvider
    {
        private const string DefaultFallbackCookieName = "aid";
        private const string CachedHttpContextKey = "__DefaultTempDataUserProvider.User";

        private readonly string _fallbackCookieName;
        private readonly ISessionIDManager _sessionIdManager;

        public DefaultTempDataUserProvider()
            : this(DefaultFallbackCookieName)
        {
        }

        private DefaultTempDataUserProvider(string fallbackCookieName)
            : this(fallbackCookieName, new SessionIDManager())
        {
        }

        // For testing.
        private DefaultTempDataUserProvider(string fallbackCookieName, ISessionIDManager sessionIdManager)
        {
            this._fallbackCookieName = fallbackCookieName;
            this._sessionIdManager = sessionIdManager;
        }

        public string GetUser(ControllerContext context)
        {
            var httpContext = context.HttpContext;
            var request = httpContext.Request;
            var response = httpContext.Response;

            // Use the same user for the duration of the request.
            if (httpContext.Items.Contains(CachedHttpContextKey))
                return (string)httpContext.Items[CachedHttpContextKey];

            var fallbackCookie = request.Cookies[_fallbackCookieName];

            string user;

            if (httpContext.Request.IsAuthenticated)
            {
                // The user has gone from being an anonymous user to an 
                // authenticated user.
                if (httpContext.Request.AnonymousID != null)
                {
                    user = httpContext.Request.AnonymousID;
                }
                else if (IsValidCookie(fallbackCookie))
                {
                    // Even though we're authenticated, the anonymous ID is
                    // used for this request because we want to grab the temp
                    // data from the previous request (when the user was 
                    // unauthenticated).
                    user = fallbackCookie?.Value;

                    // Expire the cookie since don't need the cookie anymore.
                    response.Cookies.Add(new HttpCookie(_fallbackCookieName)
                    {
                        Expires = DateTime.UtcNow.AddYears(-1)
                    });
                }
                else
                {
                    user = GetUserFromContext(httpContext);
                }
            }
            else if (httpContext.Request.AnonymousID != null)
            {
                user = httpContext.Request.AnonymousID;
            }
            // Fallback to the current session ID only when it hasn't changed
            // since new sessions are generated until the session is actually
            // *used*. However, if you're going this route, you should probably
            // be using the default SessionStateTempDataProvider in MVC :).
            else if (!httpContext.Session.IsNewSession)
            {
                user = httpContext.Session.SessionID;
            }
            else if (!IsValidCookie(fallbackCookie))
            {
                // The session ID manager is used to generate a secure ID that
                // is valid for a cookie (no reason to reinvent the wheel!).
                user = _sessionIdManager.CreateSessionID(httpContext.ApplicationInstance.Context);

                // Issue a new cookie identifying the anonymous user.
                response.Cookies.Add(new HttpCookie(_fallbackCookieName, user)
                {
                    HttpOnly = true
                });
            }
            else
            {
                user = fallbackCookie?.Value;
            }

            httpContext.Items[CachedHttpContextKey] = user;
            return user;
        }

        private bool IsValidCookie(HttpCookie cookie)
        {
            return cookie != null && _sessionIdManager.Validate(cookie.Value);
        }

        protected virtual string GetUserFromContext(HttpContextBase httpContext)
        {
            return httpContext.User.Identity.Name;
        }
    }
}
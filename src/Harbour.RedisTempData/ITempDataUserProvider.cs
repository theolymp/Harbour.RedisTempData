#region usings

using System.Web.Mvc;

#endregion

namespace Harbour.RedisTempData
{
    public interface ITempDataUserProvider
    {
        /// <summary>
        ///     Returns a value to identify the current user. This value is used
        ///     as part of the key in Redis.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        string GetUser(ControllerContext context);
    }
}
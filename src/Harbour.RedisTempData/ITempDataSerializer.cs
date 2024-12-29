#region usings

using StackExchange.Redis;

#endregion

namespace Harbour.RedisTempData
{
    public interface ITempDataSerializer
    {
        /// <summary>
        ///     Deserialize an object that was stored in Redis.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        object Deserialize(RedisValue value);

        /// <summary>
        ///     Serialize an object to be stored in Redis.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        RedisValue Serialize(object value);
    }
}
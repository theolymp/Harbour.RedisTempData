﻿#region usings

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using StackExchange.Redis;

#endregion

namespace Harbour.RedisTempData
{
    /// <summary>
    ///     An <see cref="ITempDataProvider" /> for Redis.
    /// </summary>
    // ReSharper disable once UnusedType.Global
    public class RedisTempDataProvider : ITempDataProvider
    {
        private static readonly LuaScript LoadScript = LuaScript.Prepare(@"
local result = redis.call('hgetall', @tempDataKey)
redis.call('del', @tempDataKey)
return result
");

        // Because we need raw access to the array of arguments, we can't
        // use @-parameters. Also, it's important to step by 2 because
        // the key/values are interleaved in the ARGV array (which is of
        // size 2*N).
        private static readonly LuaScript SaveScript = LuaScript.Prepare(@"
redis.call('del', KEYS[1])

for i=1,table.getn(ARGV),2 do
    redis.call('hset', KEYS[1], ARGV[i], ARGV[i + 1])
end
");

        private readonly RedisTempDataProviderOptions _options;
        private readonly IDatabase _redis;

        public RedisTempDataProvider(IDatabase database)
            : this(new RedisTempDataProviderOptions(), database)
        {
        }

        public RedisTempDataProvider(RedisTempDataProviderOptions options, IDatabase redis)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Copy so that references can't be modified outside of this class.
            this._options = new RedisTempDataProviderOptions(options);
            this._redis = redis ?? throw new ArgumentNullException(nameof(redis));
        }

        public IDictionary<string, object> LoadTempData(ControllerContext controllerContext)
        {
            var tempDataKey = GetKey(controllerContext);
            var result = new Dictionary<string, object>();

            var rawResult = (RedisValue[])_redis.ScriptEvaluate(LoadScript, new
            {
                tempDataKey = (RedisKey)tempDataKey
            });

            // HGETALL returns an array of size 2*N where each key is followed
            // by its value. We would normally let the IDatabase do this for
            // us with HashGetAll(), but we're evaluating a script.
            if (rawResult == null) return result;
            for (var i = 0; i < rawResult.Length; i += 2)
            {
                var key = rawResult[i];
                var value = rawResult[i + 1];

                result[key] = Deserialize(value);
            }

            return result;
        }

        public void SaveTempData(ControllerContext controllerContext, IDictionary<string, object> values)
        {
            var hashKey = GetKey(controllerContext);

            if (values == null || values.Count == 0)
            {
                _redis.KeyDelete(hashKey, CommandFlags.FireAndForget);
            }
            else
            {
                // Because the script can't accept a map of "key -> value",
                // we need to interleave the keys/values as an array.
                var args = new RedisValue[values.Count * 2];
                var i = 0;
                foreach (var kvp in values)
                {
                    args[i] = kvp.Key;
                    var serialized = Serialize(kvp.Value);
                    args[i + 1] = serialized;
                    // Move to next key index.
                    i += 2;
                }

                _redis.ScriptEvaluate(SaveScript.ExecutableScript,
                    new[] { (RedisKey)hashKey },
                    args,
                    CommandFlags.FireAndForget
                );
            }
        }

        private object Deserialize(RedisValue value)
        {
            if (value.IsNull) return null;

            return _options.Serializer.Deserialize(value);
        }

        private RedisValue Serialize(object value)
        {
            return _options.Serializer.Serialize(value);
        }

        private string GetKey(ControllerContext controllerContext)
        {
            var user = _options.UserProvider.GetUser(controllerContext);
            return _options.KeyPrefix + _options.KeySeparator + user;
        }
    }
}
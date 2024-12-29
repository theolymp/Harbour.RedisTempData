#region usings

using System.Runtime.Serialization;

#endregion

namespace Harbour.RedisTempData
{
    public class NetDataContractTempDataSerializer : XmlTempDataSerializerBase
    {
        protected override XmlObjectSerializer CreateSerializer()
        {
            var serializer = new NetDataContractSerializer();
            return serializer;
        }
    }
}
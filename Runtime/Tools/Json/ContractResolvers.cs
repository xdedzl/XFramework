using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System;

namespace XFramework.Json
{
    public class WritablePropertiesOnlyResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);
            return properties.Where(p => p.Writable).ToList(); // 仅保留可写属性
        }
    }
}
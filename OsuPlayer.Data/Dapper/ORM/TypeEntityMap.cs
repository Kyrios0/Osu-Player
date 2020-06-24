using System;
using System.Linq;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class TypeEntityMap<TEntity> : EntityMap<TEntity> where TEntity : class
    {
        public PropertyMap MapProperty(PropertyInfo propInfo)
        {
            var propertyMap = GetPropertyMap(propInfo);
            ThrowIfDuplicateMapping(propertyMap);
            PropertyMaps.Add(propertyMap);
            return propertyMap;
            //ReflectionHelper.GetMemberInfo((LambdaExpression)Expression<Func<TEntity, object>>);
        }

        private void ThrowIfDuplicateMapping(IPropertyMap map)
        {
            if (PropertyMaps.Any(p => p.PropertyInfo.Name == map.PropertyInfo.Name))
                throw new Exception("Duplicate mapping detected. Property '" + map.PropertyInfo.Name +
                                    "' is already mapped to column '" + map.ColumnName + "'.");
        }
    }
}
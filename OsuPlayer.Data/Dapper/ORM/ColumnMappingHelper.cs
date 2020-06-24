using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Dapper.FluentMap;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class ColumnMappingHelper
    {
        public static ReadOnlyDictionary<Type, ReadOnlyDictionary<MemberInfo, string>> ColumnMapping => Lazy.Value;

        public static ReadOnlyDictionary<Type, MemberInfo> KeyMapping => Lazy2.Value;

        internal static Dictionary<Type, ReadOnlyDictionary<MemberInfo, string>> InternalColumnMapping { get; } =
            new Dictionary<Type, ReadOnlyDictionary<MemberInfo, string>>();

        internal static Dictionary<Type, MemberInfo> InternalKeyMapping { get; } = new Dictionary<Type, MemberInfo>();

        private static readonly Lazy<ReadOnlyDictionary<Type, ReadOnlyDictionary<MemberInfo, string>>> Lazy =
            new Lazy<ReadOnlyDictionary<Type, ReadOnlyDictionary<MemberInfo, string>>>(() =>
                new ReadOnlyDictionary<Type, ReadOnlyDictionary<MemberInfo, string>>(InternalColumnMapping));

        private static readonly Lazy<ReadOnlyDictionary<Type, MemberInfo>> Lazy2 =
            new Lazy<ReadOnlyDictionary<Type, MemberInfo>>(() =>
                new ReadOnlyDictionary<Type, MemberInfo>(InternalKeyMapping));

        public static (Type tType, ReadOnlyDictionary<MemberInfo, string> dic) ValidateModel<TEntity>() where TEntity : class
        {
            var tType = typeof(TEntity);
            if (InternalColumnMapping.ContainsKey(tType)) return (tType, InternalColumnMapping[tType]);

            var props = tType.GetProperties();
            var mapping = new TypeEntityMap<TEntity>();
            var dic = new Dictionary<MemberInfo, string>();

            foreach (var propertyInfo in props)
            {
                var computed = propertyInfo.GetCustomAttribute<ComputedAttribute>();
                if (computed != null) continue;
                var key = propertyInfo.GetCustomAttribute<KeyAttribute>();
                if (key != null) InternalKeyMapping.Add(tType, propertyInfo);

                var attr = propertyInfo.GetCustomAttribute<ColumnAttribute>();
                var columnName = attr?.ColumnName ?? propertyInfo.Name;
                mapping.MapProperty(propertyInfo).ToColumn(columnName);
                dic.Add(propertyInfo, columnName);
            }

            var readOnly = new ReadOnlyDictionary<MemberInfo, string>(dic);

            if (!FluentMapper.EntityMaps.ContainsKey(tType))
                FluentMapper.Initialize(t => t.AddMap(mapping));
            else
                Console.WriteLine("'" + tType.Name + "' 已经存在其他程序集的映射，可能出现不一致的情况。");
            InternalColumnMapping.Add(tType, readOnly);
            return (tType, readOnly);
        }
    }
}
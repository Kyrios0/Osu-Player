using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class TableMappingHelper
    {
        internal static Dictionary<Type, string> InternalTableMapping { get; } = new Dictionary<Type, string>();

        private static readonly Lazy<ReadOnlyDictionary<Type, string>> Lazy =
            new Lazy<ReadOnlyDictionary<Type, string>>(() =>
                new ReadOnlyDictionary<Type, string>(InternalTableMapping));

        public static ReadOnlyDictionary<Type, string> TableMapping => Lazy.Value;


        internal static (Type tType, string) ValidateModel<TEntity>() where TEntity : class
        {
            var tType = typeof(TEntity);
            if (InternalTableMapping.ContainsKey(tType)) return (tType, InternalTableMapping[tType]);

            var attr = tType.GetCustomAttribute<TableAttribute>();
            var tableName = attr?.TableName ?? tType.Name;

            InternalTableMapping.Add(tType, tableName);
            return (tType, tableName);
        }
    }
}
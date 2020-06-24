using Milky.OsuPlayer.Data.Dapper.Provider;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public static class ProviderExtension
    {
        public static bool AutoConvertDateTime { get; set; } = true;
        public static string SqlTimeFormat { get; } = "yyyy-MM-dd HH:mm:ss";

        public static async Task<IEnumerable<T>> QueryAsync<T>(this DbProviderBase provider,
            Expression<Predicate<T>> whereCondition = null,
            Action<ColumnSelector<T>> includeColumns = null,
            Action<ColumnSelector<T>> excludeColumns = null,
            Action<ColumnSelector<T>> orderColumns = null,
            int count = 0,
            bool ascending = true)
            where T : class
        {
            GetTableTypeColumnMapping<T>(out var columnMapping, out var table);
            var columns = GetSelectColumns(includeColumns, excludeColumns, columnMapping);

            var selector = new ColumnSelector<T>();
            orderColumns?.Invoke(selector);
            var orderColumn = selector.Members.FirstOrDefault();
            var orderColumnName = orderColumn == null ? null : columnMapping[orderColumn];

            var builder = provider.WhereBuilder;
            var (sql, p) = builder.Set(whereCondition);
            return await provider.QueryAsync<T>(table, sql, p, columns, orderColumnName, count, ascending);
        }

        public static async Task<int> DeleteAsync<T>(this DbProviderBase provider,
            Expression<Predicate<T>> whereCondition)
            where T : class
        {
            GetTableTypeColumnMapping<T>(out _, out var table);

            var builder = provider.WhereBuilder;
            var (sql, p) = builder.Set(whereCondition);
            return await provider.DeleteAsync(table, sql, p);
        }

        public static async Task<int> DeleteAsync<T>(this DbProviderBase provider, T model)
            where T : class
        {
            var tType = GetTableTypeColumnMapping<T>(out _, out var table);

            var whereCondition = GetDefaultWhereCondition(model, tType);
            return await provider.DeleteAsync(table, whereCondition);
        }

        public static async Task<int> UpdateAsync<T>(this DbProviderBase provider, T model,
            Expression<Predicate<T>> whereCondition,
            Action<ColumnSelector<T>> includeColumns = null,
            Action<ColumnSelector<T>> excludeColumns = null)
            where T : class
        {
            var table = GetTableAndColumnValueDictionary(model, includeColumns, excludeColumns, out var dic, out _);

            var builder = provider.WhereBuilder;
            var (sql, p) = builder.Set(whereCondition);
            return await provider.UpdateAsync(table, dic, sql, p);
        }

        public static async Task<int> UpdateAsync<T>(this DbProviderBase provider, T model,
            Action<ColumnSelector<T>> includeColumns = null,
            Action<ColumnSelector<T>> excludeColumns = null)
            where T : class
        {
            var table = GetTableAndColumnValueDictionary(model, includeColumns, excludeColumns, out var dic, out var tType);

            var whereCondition = GetDefaultWhereCondition(model, tType);
            return await provider.UpdateAsync(table, dic, whereCondition);
        }

        public static async Task<int> InsertAsync<T>(this DbProviderBase provider, T model,
            Action<ColumnSelector<T>> includeColumns = null,
            Action<ColumnSelector<T>> excludeColumns = null)
            where T : class
        {
            var table = GetTableAndColumnValueDictionary(model, includeColumns, excludeColumns, out var dic, out _);
            return await provider.InsertAsync(table, dic);
        }

        private static string GetTableAndColumnValueDictionary<T>(T model,
            Action<ColumnSelector<T>> includeColumns,
            Action<ColumnSelector<T>> excludeColumns,
            out Dictionary<string, object> dic,
            out Type type) where T : class
        {
            var tType = GetTableTypeColumnMapping<T>(out var columnMapping, out var table);

            var includeSelector = new ColumnSelector<T>();
            includeColumns?.Invoke(includeSelector);
            bool? selectorMode = null;

            ColumnSelector<T> excludeSelector = null;
            if (includeSelector.Members.Count > 0)
            {
                selectorMode = true;
            }
            else
            {
                excludeSelector = new ColumnSelector<T>();
                excludeColumns?.Invoke(excludeSelector);
                if (excludeSelector.Members.Count > 0)
                {
                    selectorMode = false;
                }
            }

            dic = new Dictionary<string, object>();

            if (selectorMode == true) // include
            {
                foreach (var includeColumn in includeSelector.Members)
                {
                    if (!(includeColumn is PropertyInfo propInfo))
                        throw new InvalidCastException();
                    var columnName = columnMapping[includeColumn];
                    var value = propInfo.GetValue(model);
                    if (value is DateTime dt && AutoConvertDateTime)
                    {
                        dic.Add(columnName, dt.ToString(SqlTimeFormat));
                    }
                    else
                    {
                        dic.Add(columnName, value);
                    }
                }
            }
            else
            {
                foreach (var kvp in columnMapping)
                {
                    var column = kvp.Key;
                    if (selectorMode == false && excludeSelector.Members.Contains(column)) continue;

                    var columnName = kvp.Value;
                    if (!(column is PropertyInfo propInfo))
                        throw new InvalidCastException();

                    var value = propInfo.GetValue(model);
                    dic.Add(columnName, value);
                }
            }

            type = tType;
            return table;
        }

        private static Type GetTableTypeColumnMapping<T>(out ReadOnlyDictionary<MemberInfo, string> columnMapping, out string table) where T : class
        {
            Type tType;
            (tType, columnMapping) = ColumnMappingHelper.ValidateModel<T>();
            (_, table) = TableMappingHelper.ValidateModel<T>();
            return tType;
        }

        private static Where GetDefaultWhereCondition<T>(T model, Type tType) where T : class
        {
            if (!ColumnMappingHelper.KeyMapping.ContainsKey(tType))
                throw new InvalidOperationException("Should be a model with key definition");
            var prop = (PropertyInfo)ColumnMappingHelper.KeyMapping[tType];
            var propName = ColumnMappingHelper.ColumnMapping[tType][prop];
            var value = prop.GetValue(model);
            if (value == default)
                throw new InvalidOperationException(
                    "Should be a db model with key value (An object query from database or an object set value for key column manually)");
            var whereCondition = new Where(propName, value);
            return whereCondition;
        }

        private static HashSet<string> GetSelectColumns<T>(Action<ColumnSelector<T>> includeColumns, Action<ColumnSelector<T>> excludeColumns, ReadOnlyDictionary<MemberInfo, string> columnMapping)
            where T : class
        {
            var includeSelector = new ColumnSelector<T>();
            includeColumns?.Invoke(includeSelector);
            bool? selectorMode = null;

            ColumnSelector<T> excludeSelector = null;
            if (includeSelector.Members.Count > 0)
            {
                selectorMode = true;
            }
            else
            {
                excludeSelector = new ColumnSelector<T>();
                excludeColumns?.Invoke(excludeSelector);
                if (excludeSelector.Members.Count > 0)
                {
                    selectorMode = false;
                }
            }

            var columns = new HashSet<string>();

            if (selectorMode == true) // include
            {
                foreach (var includeColumn in includeSelector.Members)
                {
                    var columnName = columnMapping[includeColumn];
                    columns.Add(columnName);
                }
            }
            else
            {
                foreach (var kvp in columnMapping)
                {
                    var column = kvp.Key;
                    if (selectorMode == false && excludeSelector.Members.Contains(column)) continue;

                    var columnName = kvp.Value;
                    columns.Add(columnName);
                }
            }

            return columns;
        }
    }
}

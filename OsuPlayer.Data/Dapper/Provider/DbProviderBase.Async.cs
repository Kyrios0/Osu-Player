using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Milky.OsuPlayer.Data.Dapper.Provider
{
    public abstract partial class DbProviderBase
    {
        #region Async methods

        public virtual async Task<bool> TestConnectionAsync()
        {
            try
            {
                await GetAllTablesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 查询数据表，并存入内存表中
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<DataTable> GetDataTableAsync(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await GetDataTableAsync(table, whereConditions: null, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存入内存表中
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<DataTable> GetDataTableAsync(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await GetDataTableAsync(table, new[] { whereCondition }, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存入内存表中
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<DataTable> GetDataTableAsync(
            string table,
            IReadOnlyCollection<Where> whereConditions,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            var (success, keyword) = await VerifyTableAndColumnAsync(table, columns, orderColumn, whereConditions);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            var cmdText = GetSelectCommandTemplate(table, columns, orderColumn, whereStr, count, ascending);
            try
            {
                IDataReader reader;
                //var dt = new DataTable();
                if (UseSingletonConnection)
                {
                    reader = @params == null
                        ? await SingletonConnection.ExecuteReaderAsync(cmdText)
                        : await SingletonConnection.ExecuteReaderAsync(cmdText, @params);
                    //var result = @params == null
                    //    ? SingletonConnection.Query(cmdText)
                    //    : SingletonConnection.Query(cmdText, @params);
                    //ManualToDataTable(result, dt);
                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        reader = @params == null
                            ? await dbConn.ExecuteReaderAsync(cmdText)
                            : await dbConn.ExecuteReaderAsync(cmdText, @params);
                        //var result = @params == null
                        //    ? SingletonConnection.Query(cmdText)
                        //    : SingletonConnection.Query(cmdText, @params);
                        //ManualToDataTable(result, dt);
                    }
                }

                var dt = new DataTable();
                dt.Load(reader);
                reader.Dispose();

                return dt;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{cmdText}", ex.InnerException ?? ex);
            }
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<T>> QueryAsync<T>(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await QueryAsync<T>(table, whereConditions: null, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<T>> QueryAsync<T>(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await QueryAsync<T>(table, new[] { whereCondition }, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<dynamic>> QueryAsync(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await QueryAsync<dynamic>(table, whereConditions: null, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<dynamic>> QueryAsync(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await QueryAsync<dynamic>(table, new[] { whereCondition }, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<dynamic>> QueryAsync(
            string table,
            IReadOnlyCollection<Where> whereConditions = null,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            return await QueryAsync<dynamic>(table, whereConditions, columns, orderColumn, count, ascending);
        }

        /// <summary>
        /// 查询数据表，并进行强类型映射为列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="ascending">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<T>> QueryAsync<T>(
            string table,
            IReadOnlyCollection<Where> whereConditions,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            var (success, keyword) = await VerifyTableAndColumnAsync(null, null, null, whereConditions);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            return await QueryAsync<T>(table, whereStr, @params, columns, orderColumn, count, ascending);
        }

        internal async Task<IEnumerable<T>> QueryAsync<T>(
            string table,
            string whereStr, DynamicParameters whereParams,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            var (success, keyword) = await VerifyTableAndColumnAsync(table, columns, orderColumn, null);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            var sql = GetSelectCommandTemplate(table, columns, orderColumn, whereStr, count, ascending);

            try
            {
                var result = await InnerQueryAsync<T>(whereParams, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        public virtual async Task<int> UpdateAsync(string table,
            Dictionary<string, object> updateColumns,
            Where whereCondition)
        {
            return await UpdateAsync(table, updateColumns, new[] { whereCondition });
        }

        public virtual async Task<int> UpdateAsync(string table,
            Dictionary<string, object> updateColumns,
            IReadOnlyCollection<Where> whereConditions,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            var (success, keyword) =
                await VerifyTableAndColumnAsync(null, null, null, whereConditions);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var whereParams);

            return await UpdateAsync(table, updateColumns, whereStr, whereParams, orderColumn, count, ascending);
        }

        internal async Task<int> UpdateAsync(string table,
            Dictionary<string, object> updateColumns,
            string whereStr, DynamicParameters whereParams,
            string orderColumn = null,
            int count = 0,
            bool ascending = true)
        {
            if (updateColumns == null || updateColumns.Count == 0)
                throw new ArgumentException("请提供更新字段");
            var (success, keyword) =
                await VerifyTableAndColumnAsync(table, updateColumns.Keys.ToList(), null, null);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetUpdateCommandTemplate(table, updateColumns, whereStr, whereParams, orderColumn, count, ascending, out var sql,
                out var @params);
            try
            {
                @params = ExpandObjects(whereParams, @params);
                var result = await InnerExecuteAsync(@params, sql);

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        public virtual async Task<int> InsertAsync(string table, Dictionary<string, object> insertColumns)
        {
            if (insertColumns == null || insertColumns.Count == 0)
                throw new Exception("请提供插入字段");
            var (success, keyword) = await VerifyTableAndColumnAsync(table, insertColumns.Keys.ToList(), null, null);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetInsertCommandTemplate(table, insertColumns, out var sql, out var @params);

            try
            {
                var result = await InnerExecuteAsync(@params, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        public virtual async Task<int> InsertAsync(string table, ICollection<Dictionary<string, object>> insertColumns)
        {
            int count = 0;
            if (SingletonConnection.State != ConnectionState.Open)
                SingletonConnection.Open();
            using (var transaction = SingletonConnection.BeginTransaction())
            {
                foreach (var insertColumn in insertColumns)
                {
                    if (insertColumn == null || insertColumn.Count == 0)
                        throw new Exception("请提供插入字段");
                    if (!VerifyTableAndColumn(table, insertColumn.Keys.ToList(), null, null, out var keyword))
                        throw new Exception($"不存在表或者相关列名：{keyword}");

                    GetInsertCommandTemplate(table, insertColumn, out var sql, out var @params);

                    int result;

                    try
                    {
                        result = @params == null
                            ? SingletonConnection.Execute(sql)
                            : SingletonConnection.Execute(sql, @params);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                            $"数据库执行语句：{sql}", ex);
                    }

                    count += result;
                }

                transaction.Commit();
            }

            return count;
        }

        public virtual async Task<int> DeleteAsync(string table, Where whereCondition)
        {
            return await DeleteAsync(table, new[] { whereCondition });
        }

        public virtual async Task<int> DeleteAsync(string table, IReadOnlyCollection<Where> whereConditions)
        {
            if (whereConditions == null || whereConditions.Count == 0)
                throw new Exception("请提供更新记录的Where依据");
            var (success, keyword) = await VerifyTableAndColumnAsync(null, null, null, whereConditions);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            return await DeleteAsync(table, whereStr, @params);
        }

        internal async Task<int> DeleteAsync(string table, string whereStr, DynamicParameters whereParams)
        {
            var (success, keyword) = await VerifyTableAndColumnAsync(table, null, null, null);
            if (!success) throw new Exception($"不存在表或者相关列名：{keyword}");

            string sql = GetDeleteCommandTemplate(table, whereStr);

            try
            {
                var result = await InnerExecuteAsync(whereParams, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        // 获取数据库中所有的表
        public async Task<List<string>> GetAllTablesAsync()
        {
            string sqlStr = null;
            try
            {
                List<string> innerGetAllTables;
                if (UseSingletonConnection)
                {
                    innerGetAllTables = await Task.Run(() => InnerGetAllTables(SingletonConnection, out sqlStr));
                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    var dbConn = GetNewDbConnection();
                    innerGetAllTables = await Task.Run(() => InnerGetAllTables(dbConn, out sqlStr));
                }

                return innerGetAllTables;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取数据库中表名出错（{sqlStr}）", ex.InnerException ?? ex);
            }
        }

        public async Task<List<string>> GetAllTablesAsync(bool useCache)
        {
            if (useCache && _cachedTables != null)
            {
                return _cachedTables;
            }

            var tables = await GetAllTablesAsync();
            _cachedTables = tables;
            return _cachedTables;
        }

        public async Task<List<string>> GetColumnsByTableAsync(string tableName)
        {
            string sqlStr = null;
            try
            {
                List<string> columns;
                if (UseSingletonConnection)
                {
                    columns = await Task.Run(() => InnerGetColumnsByTable(SingletonConnection, tableName, out sqlStr));
                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    var dbConn = GetNewDbConnection();
                    columns = await Task.Run(() => InnerGetColumnsByTable(dbConn, tableName, out sqlStr));
                }

                return columns;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取数据表{tableName}中字段名出错（{sqlStr}）", ex.InnerException ?? ex);
            }
        }

        public async Task<List<string>> GetColumnsByTableAsync(string tableName, bool useCache)
        {
            if (useCache && _cachedColDic.ContainsKey(tableName))
            {
                return _cachedColDic[tableName];
            }

            var cols = await GetColumnsByTableAsync(tableName);
            _cachedColDic.Add(tableName, cols);
            return cols;
        }

        private async Task<(bool, string)> VerifyTableAndColumnAsync(
            string table,
            IReadOnlyCollection<string> columns,
            string orderColumn,
            IEnumerable<Where> whereConditions)
        {
            var allColumns = whereConditions?.Select(k => k.ColumnName).ToList() ?? new List<string>();
            if (columns != null && columns.Count != 0)
            {
                allColumns.AddRange(columns);
            }

            if (orderColumn != null)
            {
                allColumns.Add(orderColumn);
            }

            (bool result, string keyword) = await VerifyTableAsync(table);
            if (!result) return (false, keyword);
            (result, keyword) = await VerifyColumnsAsync(table, allColumns);
            return (result, keyword);
        }

        private async Task<(bool, string)> VerifyTableAsync(string table)
        {
            if (table == null) return (true, null);

            if (_whiteListInfo.ContainsKey(table))
            {
                return (true, null);
            }

            var newTableList = await GetAllTablesAsync();

            var existTableList = _whiteListInfo.Select(k => k.Key).ToList();
            foreach (var existTable in existTableList)
            {
                if (!newTableList.Contains(existTable))
                {
                    _whiteListInfo[existTable].Clear();
                    _whiteListInfo[existTable] = null;
                    _whiteListInfo.Remove(existTable);
                }
            }

            foreach (var newTable in newTableList)
            {
                if (!_whiteListInfo.ContainsKey(newTable))
                {
                    _whiteListInfo.Add(newTable, new HashSet<string>());
                }
            }

            if (!newTableList.Contains(table, StringComparer.OrdinalIgnoreCase))
            //if (!newTableList.Contains(table))
            {
                return (false, table);
            }

            return (true, null);
        }

        private async Task<(bool, string)> VerifyColumnsAsync(string table, IEnumerable<string> columns)
        {
            var set = _whiteListInfo.FirstOrDefault(k => k.Key.Equals(table, StringComparison.OrdinalIgnoreCase)).Value;
            //var set = _whiteListInfo[table];
            var arrColumns = columns.ToArray();
            if (arrColumns.Any(k => !set.Contains(k)))
            {
                var newColumnList = await GetColumnsByTableAsync(table);

                _whiteListInfo[table] = new HashSet<string>(newColumnList);

                foreach (var column in arrColumns)
                {
                    if (newColumnList.Contains(column))
                    {
                        continue;
                    }

                    return (false, column);
                }
            }

            return (true, null);
        }

        private async Task<IEnumerable<T>> InnerQueryAsync<T>(DynamicParameters @params, string sql)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            try
            {
                IEnumerable<T> result;
                if (UseSingletonConnection)
                {
                    result = @params == null
                        ? await SingletonConnection.QueryAsync<T>(sql)
                        : await SingletonConnection.QueryAsync<T>(sql, @params);

                    if (!KeepSingletonConnectionOpen)
                        SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        result = @params == null
                            ? await dbConn.QueryAsync<T>(sql)
                            : await dbConn.QueryAsync<T>(sql, @params);
                    }
                }

                return result;
            }
            finally
            {
#if DEBUG
                if (@params == null || !@params.ParameterNames.Any())
                {
                    Console.WriteLine($"Queried SQL: '{sql}' in {sw.Elapsed.TotalMilliseconds} ms.");
                }
                else
                {
                    Console.WriteLine("Queried SQL: '" + sql + "', Params: {" + string.Join(",",
                        @params.ParameterNames.Select(k => k + ": " + @params.Get<object>(k))
                    ) + "} in " + sw.Elapsed.TotalMilliseconds + " ms.");
                }
#endif
            }
        }

        private async Task<int> InnerExecuteAsync(DynamicParameters @params, string sql)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            try
            {
                int result;
                if (UseSingletonConnection)
                {
                    result = @params == null
                        ? await SingletonConnection.ExecuteAsync(sql)
                        : await SingletonConnection.ExecuteAsync(sql, @params);

                    if (!KeepSingletonConnectionOpen)
                        SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        result = @params == null
                            ? await dbConn.ExecuteAsync(sql)
                            : await dbConn.ExecuteAsync(sql, @params);
                    }
                }

                return result;
            }
            finally
            {
#if DEBUG
                if (@params == null || !@params.ParameterNames.Any())
                {
                    Console.WriteLine($"Executed SQL: '{sql}' in {sw.Elapsed.TotalMilliseconds} ms.");
                }
                else
                {
                    Console.WriteLine("Executed SQL: '" + sql + "', Params: {" + string.Join(",",
                        @params.ParameterNames.Select(k => k + ": " + @params.Get<object>(k))
                    ) + "} in " + sw.Elapsed.TotalMilliseconds + " ms.");
                }
#endif
            }
        }

        #endregion
    }
}

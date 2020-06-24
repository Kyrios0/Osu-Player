using Dapper;
using Milky.OsuPlayer.Data.Dapper.ORM;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Milky.OsuPlayer.Data.Dapper.Provider
{
    public abstract partial class DbProviderBase : IDisposable
    {
        /// <summary>
        /// 当数据库状态更改时发生。
        /// </summary>
        public virtual event StateChangeEventHandler StateChange;

        /// <summary>
        /// 数据库类型。
        /// </summary>
        public abstract DataBaseType DbType { get; }

        /// <summary>
        /// 数据库连接字符串。
        /// </summary>
        public virtual string ConnectionString => string.IsNullOrWhiteSpace(_overrideConnectionString)
            ? DbConnectionStringBuilder.ConnectionString
            : _overrideConnectionString;

        public virtual WhereBuilder WhereBuilder { get; set; }

        /// <summary>
        /// 是否设置了连接字符串。
        /// </summary>
        public bool ConnectionStringConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

        /// <summary>
        /// 是否启用单例的DbConnection。
        /// 若单线程操作可为true，否则为false。
        /// （默认为true）
        /// </summary>
        public bool UseSingletonConnection { get; set; } = true;

        /// <summary>
        /// 当使用单例DbConnection时，是否保持长连接。
        /// （默认为true）
        /// </summary>
        public bool KeepSingletonConnectionOpen { get; set; } = true;

        /// <summary>
        /// 描述与数据源连接当前的状态。
        /// </summary>
        public virtual ConnectionState State => SingletonConnection.State;

        protected DbConnectionStringBuilder DbConnectionStringBuilder;

        private string _overrideConnectionString;

        private readonly Dictionary<string, HashSet<string>>
            _whiteListInfo = new Dictionary<string, HashSet<string>>();

        private DbConnection _dbConnection;

        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private List<string> _cachedTables;
        private readonly Dictionary<string, List<string>> _cachedColDic = new Dictionary<string, List<string>>();

        protected internal virtual DbConnection SingletonConnection // 单例DbConnection实例
        {
            get
            {
                try
                {
                    _rwLock.EnterReadLock();
                    if (!ConnectionStringConfigured)
                    {
                        throw new Exception($"{DbType}数据库连接字符串未设置");
                    }

                    return _dbConnection;
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
            set
            {
                try
                {
                    _rwLock.EnterWriteLock();
                    _dbConnection?.Close();
                    _dbConnection?.Dispose();
                    _dbConnection = value;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// 设置连接字符串
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public virtual DbProviderBase ConfigureConnectionString(Action<DbConnectionStringBuilder> action)
        {
            action.Invoke(DbConnectionStringBuilder);
            //ConnectionStringConfigured = true;
            SignUpDbConnection();
            return this;
        }

        /// <summary>
        /// 设置连接字符串（使用原生串覆盖）
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public virtual DbProviderBase ConfigureConnectionString(string connectionString)
        {
            _overrideConnectionString = connectionString;
            //ConnectionStringConfigured = true;
            SignUpDbConnection();
            return this;
        }

        /// <summary>
        /// 获取当前DbConnection的实例
        /// </summary>
        /// <returns></returns>
        public virtual DbConnection GetDbConnection()
        {
            if (UseSingletonConnection)
            {
                if (SingletonConnection == null)
                {
                    SignUpDbConnection();
                }

                return SingletonConnection;
            }
            else
            {
                return GetNewDbConnection();
            }
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(TestConnectionAsync) + "' instead")]
        public virtual bool TestConnection()
        {
            try
            {
                GetAllTables();
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
        /// <param name="asc">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(GetDataTableAsync) + "' instead")]
        public virtual DataTable GetDataTable(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return GetDataTable(table, whereConditions: null, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并存入内存表中
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(GetDataTableAsync) + "' instead")]
        public virtual DataTable GetDataTable(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return GetDataTable(table, new[] { whereCondition }, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并存入内存表中
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(GetDataTableAsync) + "' instead")]
        public virtual DataTable GetDataTable(
            string table,
            IReadOnlyCollection<Where> whereConditions,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            if (!VerifyTableAndColumn(table, columns, orderColumn, whereConditions, out var keyword))
            {
                throw new Exception($"验证参数时出错：不存在表或者相关列名：{keyword}");
            }

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            var cmdText = GetSelectCommandTemplate(table, columns, orderColumn, whereStr, count, asc);
            try
            {
                IDataReader reader;
                if (UseSingletonConnection)
                {
                    reader = @params == null
                        ? SingletonConnection.ExecuteReader(cmdText)
                        : SingletonConnection.ExecuteReader(cmdText, @params);
                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        reader = @params == null
                            ? dbConn.ExecuteReader(cmdText)
                            : dbConn.ExecuteReader(cmdText, @params);
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
        /// <param name="asc">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<T> Query<T>(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return Query<T>(table, whereConditions: null, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<T> Query<T>(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return Query<T>(table, new[] { whereCondition }, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并进行强类型映射为列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<T> Query<T>(
            string table,
            IReadOnlyCollection<Where> whereConditions,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            if (!VerifyTableAndColumn(table, columns, orderColumn, whereConditions, out var keyword))
            {
                throw new Exception($"不存在表或者相关列名：{keyword}");
            }

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            var sql = GetSelectCommandTemplate(table, columns, orderColumn, whereStr, count, asc);

            try
            {
                var result = InnerQuery<T>(@params, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<dynamic> Query(
            string table,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return Query<dynamic>(table, whereConditions: null, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereCondition">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<dynamic> Query(
            string table,
            Where whereCondition,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return Query<dynamic>(table, new[] { whereCondition }, columns, orderColumn, count, asc);
        }

        /// <summary>
        /// 查询数据表，并存为动态列表
        /// </summary>
        /// <param name="table">表名</param>
        /// <param name="columns">列名</param>
        /// <param name="orderColumn">排序依据列名</param>
        /// <param name="asc">是否升序</param>
        /// <param name="whereConditions">查询条件</param>
        /// <param name="count">查询条数（已被MaxDataCount所限）</param>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(QueryAsync) + "' instead")]
        public virtual IEnumerable<dynamic> Query(
            string table,
            IReadOnlyCollection<Where> whereConditions = null,
            IReadOnlyCollection<string> columns = null,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            return Query<dynamic>(table, whereConditions, columns, orderColumn, count, asc);
        }

        [Obsolete("Use async method '" + nameof(UpdateAsync) + "' instead")]
        public virtual int Update(string table,
            Dictionary<string, object> updateColumns,
            Where whereCondition)
        {
            return Update(table, updateColumns, new[] { whereCondition });
        }

        [Obsolete("Use async method '" + nameof(UpdateAsync) + "' instead")]
        public virtual int Update(string table,
            Dictionary<string, object> updateColumns,
            IReadOnlyCollection<Where> whereConditions,
            string orderColumn = null,
            int count = 0,
            bool asc = true)
        {
            if (updateColumns == null || updateColumns.Count == 0)
                throw new ArgumentException("请提供更新字段");
            if (whereConditions == null || whereConditions.Count == 0)
                throw new ArgumentException("请提供更新记录的Where依据");
            if (!VerifyTableAndColumn(table, updateColumns.Keys.ToList(), null, whereConditions, out var keyword))
                throw new ArgumentException($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var whereParams);
            GetUpdateCommandTemplate(table, updateColumns, whereStr, whereParams, orderColumn, count, asc, out var sql,
                out var @params);
            try
            {
                @params = ExpandObjects(whereParams, @params);
                var result = InnerExecute(@params, sql);

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        [Obsolete("Use async method '" + nameof(InsertAsync) + "' instead")]
        public virtual int Insert(string table, Dictionary<string, object> insertColumns)
        {
            if (insertColumns == null || insertColumns.Count == 0)
                throw new Exception("请提供插入字段");
            if (!VerifyTableAndColumn(table, insertColumns.Keys.ToList(), null, null, out var keyword))
                throw new Exception($"不存在表或者相关列名：{keyword}");

            GetInsertCommandTemplate(table, insertColumns, out var sql, out var @params);

            try
            {
                var result = InnerExecute(@params, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        [Obsolete("Use async method '" + nameof(DeleteAsync) + "' instead")]
        public virtual int Delete(string table, Where whereCondition)
        {
            return Delete(table, new[] { whereCondition });
        }

        [Obsolete("Use async method '" + nameof(DeleteAsync) + "' instead")]
        public virtual int Delete(string table, IReadOnlyCollection<Where> whereConditions)
        {
            if (whereConditions == null || whereConditions.Count == 0)
                throw new Exception("请提供更新记录的Where依据");
            if (!VerifyTableAndColumn(table, null, null, whereConditions, out var keyword))
                throw new Exception($"不存在表或者相关列名：{keyword}");

            GetWhereStrAndParameters(whereConditions, out var whereStr, out var @params);
            string sql = GetDeleteCommandTemplate(table, whereStr);

            try
            {
                var result = InnerExecute(@params, sql);
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"从{DbType}数据库中获取数据出错：\r\n" +
                                    $"数据库执行语句：{sql}", ex.InnerException ?? ex);
            }
        }

        /// <summary>
        /// 释放<see cref="DbProviderBase"/>的资源。
        /// </summary>
        public void Dispose()
        {
            SingletonConnection?.Dispose();
            _rwLock?.Dispose();
            StateChange = null;
            ProtectedDispose();
        }

        /// <summary>
        /// 获取数据库中所有的表
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use async method '" + nameof(GetAllTablesAsync) + "' instead")]
        public List<string> GetAllTables()
        {
            string sqlStr = null;
            try
            {
                List<string> innerGetAllTables;
                if (UseSingletonConnection)
                {
                    innerGetAllTables = InnerGetAllTables(SingletonConnection, out sqlStr);

                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    var dbConn = GetNewDbConnection();
                    innerGetAllTables = InnerGetAllTables(dbConn, out sqlStr);
                }

                return innerGetAllTables;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取数据库中表名出错（{sqlStr}）", ex.InnerException ?? ex);
            }
        }

        [Obsolete("Use async method '" + nameof(GetAllTablesAsync) + "' instead")]
        public List<string> GetAllTables(bool useCache)
        {
            if (useCache && _cachedTables != null)
            {
                return _cachedTables;
            }

            var tables = GetAllTables();
            _cachedTables = tables;
            return _cachedTables;
        }

        [Obsolete("Use async method '" + nameof(GetColumnsByTableAsync) + "' instead")]
        public List<string> GetColumnsByTable(string tableName)
        {
            string sqlStr = null;
            try
            {
                List<string> columns;
                if (UseSingletonConnection)
                {
                    columns = InnerGetColumnsByTable(SingletonConnection, tableName, out sqlStr);
                    if (!KeepSingletonConnectionOpen) SingletonConnection.Close();
                }
                else
                {
                    var dbConn = GetNewDbConnection();
                    columns = InnerGetColumnsByTable(dbConn, tableName, out sqlStr);
                }

                return columns;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取数据表{tableName}中字段名出错（{sqlStr}）", ex.InnerException ?? ex);
            }
        }

        [Obsolete("Use async method '" + nameof(GetColumnsByTableAsync) + "' instead")]
        public List<string> GetColumnsByTable(string tableName, bool useCache)
        {
            if (useCache && _cachedColDic.ContainsKey(tableName))
            {
                return _cachedColDic[tableName];
            }

            var cols = GetColumnsByTable(tableName);
            _cachedColDic.Add(tableName, cols);
            return cols;
        }

        protected DbProviderBase()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        protected internal abstract List<string> InnerGetAllTables(DbConnection dbConnection, out string sqlStr);

        protected internal abstract List<string> InnerGetColumnsByTable(DbConnection dbConnection, string tableName, out string sqlStr);

        protected virtual DbConnection GetNewDbConnection()
        {
            throw new NotImplementedException();
        }

        protected virtual string GetSelectCommandTemplate(string table, IReadOnlyCollection<string> columns,
            string orderColumn, string whereStr,
            int count, bool asc)
        {
            throw new NotImplementedException();
        }

        protected virtual void GetUpdateCommandTemplate(string table, Dictionary<string, object> updateColumns,
            string whereStr, DynamicParameters whereParams,
            string orderColumn,
            int count,
            bool asc, out string sql, out DynamicParameters @params)
        {
            throw new NotImplementedException();
        }

        protected virtual void GetInsertCommandTemplate(string table, Dictionary<string, object> insertColumns,
            out string sql, out DynamicParameters @params)
        {
            throw new NotImplementedException();
        }

        protected virtual string GetDeleteCommandTemplate(string table, string whereStr)
        {
            throw new NotImplementedException();
        }

        protected virtual void ProtectedDispose()
        {
        }

        private void SignUpDbConnection()
        {
            var dbConn = GetNewDbConnection();
            SingletonConnection = dbConn;
            SingletonConnection.StateChange += (sender, e) => StateChange?.Invoke(sender, e);
        }

        private bool VerifyTableAndColumn(
            string table,
            IReadOnlyCollection<string> columns,
            string orderColumn,
            IEnumerable<Where> whereConditions,
            out string keyword)
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

            return VerifyTable(table, out keyword) && VerifyColumns(table, allColumns, out keyword);
        }

        private void GetWhereStrAndParameters(IReadOnlyCollection<Where> whereConditions,
            out string whereStr,
            out DynamicParameters @params)
        {
            whereStr = "1 = 1";
            @params = null;

            if (whereConditions == null || whereConditions.Count <= 0)
            {
                return;
            }

            @params = new DynamicParameters();
            var existColumn = new HashSet<string>();

            foreach (var kvp in whereConditions)
            {
                if (kvp.Value is null) continue;

                if (!existColumn.Contains(kvp.ColumnName))
                {
                    existColumn.Add(kvp.ColumnName);
                }
                else
                {
                    int j = 0;
                    var newStr = kvp.ColumnName + j;
                    while (existColumn.Contains(newStr))
                    {
                        j++;
                        newStr = kvp.ColumnName + j;
                    }

                    kvp.ColumnNameOther = newStr;
                    existColumn.Add(newStr);
                }


                switch (kvp.WhereType)
                {
                    case WhereType.LikeAll:
                        @params.Add(kvp.ColumnNameOther, $"%{kvp.Value}%");
                        break;
                    case WhereType.LikeAtEnd:
                        @params.Add(kvp.ColumnNameOther, $"%{kvp.Value}");
                        break;
                    case WhereType.LikeAtBegin:
                        @params.Add(kvp.ColumnNameOther, $"{kvp.Value}%");
                        break;
                    default:
                        @params.Add(kvp.ColumnNameOther, kvp.Value);
                        break;
                }
            }

            var sb = new StringBuilder();
            int i = 0;
            foreach (var condition in whereConditions)
            {
                if (i != 0)
                {
                    sb.Append(" AND ");
                }

                var columnName = condition.ColumnName;
                var valueQuote = "";

                #region Not used

                //switch (DbType)
                //{
                //    case DataBaseType.Access:
                //        if (condition.ForceKeyType.HasValue)
                //        {
                //            valueQuote =
                //                (condition.ForceKeyType == System.Data.DbType.Time ||
                //                 condition.ForceKeyType == System.Data.DbType.DateTime ||
                //                 condition.ForceKeyType == System.Data.DbType.DateTime2 ||
                //                 condition.ForceKeyType == System.Data.DbType.DateTimeOffset)
                //                    ? ""
                //                    : "";
                //        }
                //        else
                //        {
                //            valueQuote =
                //                (condition.Value is DateTime ||
                //                 condition.Value is DateTimeOffset)
                //                    ? ""
                //                    : "";
                //        }

                //        break;
                //    case DataBaseType.Sqlite:
                //    case DataBaseType.SqlServer:
                //        if (condition.ForceKeyType.HasValue)
                //        {
                //            valueQuote =
                //                (condition.ForceKeyType == System.Data.DbType.String ||
                //                 condition.ForceKeyType == System.Data.DbType.StringFixedLength ||
                //                 condition.ForceKeyType == System.Data.DbType.AnsiString ||
                //                 condition.ForceKeyType == System.Data.DbType.AnsiStringFixedLength
                //                )
                //                    ? ""
                //                    : "";
                //        }
                //        else
                //        {
                //            valueQuote =
                //                (condition.Value is string ||
                //                 condition.Value is char)
                //                    ? ""
                //                    : "";
                //        }

                //        break;
                //    case DataBaseType.MySql:
                //        valueQuote = "";
                //        break;
                //    default:
                //        throw new ArgumentOutOfRangeException(nameof(DbType));
                //}

                #endregion

                if (condition.Value is null)
                {
                    switch (condition.WhereType)
                    {
                        case WhereType.Equal:
                            sb.Append(string.Format("{0} {1}",
                                columnName, "IS NULL")
                            );
                            break;
                        case WhereType.Unequal:
                            sb.Append(string.Format("{0} {1}",
                                columnName, "IS NOT NULL")
                            );
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    sb.Append(string.Format("{0} {1} {2}@{3}{4}",
                        columnName,
                        Where.GetTypeSymbol(condition.WhereType),
                        valueQuote,
                        condition.ColumnNameOther,
                        valueQuote));
                }

                i++;
            }

            whereStr = sb.ToString();
        }

        private bool VerifyColumns(string table, IEnumerable<string> columns, out string keyword)
        {
            var set = _whiteListInfo.FirstOrDefault(k => k.Key.Equals(table, StringComparison.OrdinalIgnoreCase)).Value;
            //var set = _whiteListInfo[table];
            var arrColumns = columns.ToArray();
            if (arrColumns.Any(k => !set.Contains(k)))
            {
                var newColumnList = GetColumnsByTable(table);

                _whiteListInfo[table] = new HashSet<string>(newColumnList);

                foreach (var column in arrColumns)
                {
                    if (newColumnList.Contains(column))
                    {
                        continue;
                    }

                    keyword = column;
                    return false;
                }
            }

            keyword = null;
            return true;
        }

        private bool VerifyTable(string table, out string keyword)
        {
            if (_whiteListInfo.ContainsKey(table))
            {
                keyword = null;
                return true;
            }

            var newTableList = GetAllTables();

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
                keyword = table;
                return false;
            }

            keyword = null;
            return true;
        }

        private IEnumerable<T> InnerQuery<T>(DynamicParameters @params, string sql)
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
                        ? SingletonConnection.Query<T>(sql)
                        : SingletonConnection.Query<T>(sql, @params);

                    if (!KeepSingletonConnectionOpen)
                        SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        result = @params == null
                            ? dbConn.Query<T>(sql)
                            : dbConn.Query<T>(sql, @params);
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

        private int InnerExecute(DynamicParameters @params, string sql)
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
                        ? SingletonConnection.Execute(sql)
                        : SingletonConnection.Execute(sql, @params);

                    if (!KeepSingletonConnectionOpen)
                        SingletonConnection.Close();
                }
                else
                {
                    using (var dbConn = GetNewDbConnection())
                    {
                        result = @params == null
                            ? dbConn.Execute(sql)
                            : dbConn.Execute(sql, @params);
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

        private static DynamicParameters ExpandObjects(params DynamicParameters[] @params)
        {
            if (@params.Length == 0)
                return new DynamicParameters();
            var source = new DynamicParameters();
            foreach (var o in @params)
            {
                foreach (var key in o.ParameterNames)
                {
                    var value = o.Get<object>(key);
                    if (!source.ParameterNames.Contains(key))
                    {
                        source.Add(key, value);
                    }
                    else
                    {
                        throw new Exception("对象存在重复键。");
                    }
                }
            }

            return source;
        }
    }
}
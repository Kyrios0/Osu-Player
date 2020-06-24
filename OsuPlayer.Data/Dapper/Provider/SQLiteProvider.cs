using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using Dapper;

namespace Milky.OsuPlayer.Data.Dapper.Provider
{
    // ReSharper disable once InconsistentNaming
    public sealed class SQLiteProvider : DbProviderBase
    {
        public SQLiteProvider()
        {
            DbConnectionStringBuilder = new SQLiteConnectionStringBuilder();
        }

        public override DataBaseType DbType => DataBaseType.Sqlite;

        protected override DbConnection GetNewDbConnection()
        {
            return new SQLiteConnection(ConnectionString);
        }

        protected internal override List<string> InnerGetAllTables(DbConnection dbConnection,
            out string sqlStr)
        {
            sqlStr = "SELECT name FROM sqlite_master WHERE type='table'";
            var list = dbConnection.Query<string>(sqlStr).ToList();
            return list;
        }

        protected internal override List<string> InnerGetColumnsByTable(DbConnection dbConnection,
            string tableName,
            out string sqlStr)
        {
            sqlStr = $"PRAGMA table_info(`{tableName}`)";
            var list = dbConnection.Query(sqlStr).ToList();

            return list.Select(o => (string)o.name).ToList();
        }

        protected override string GetSelectCommandTemplate(string table,
            IReadOnlyCollection<string> columns,
            string orderColumn,
            string whereStr,
            int count,
            bool asc)
        {
            var colStr = "*";
            if (columns != null && columns.Count > 0)
            {
                colStr = string.Join(",", columns.Select(k => $"`{k}`"));
            }

            return $"SELECT {colStr} " +
                   $"FROM `{table}` " +
                   $"WHERE {whereStr} " +
                   (orderColumn == null ? "" : $"ORDER BY `{orderColumn}` {(asc ? "ASC" : "DESC")} ") +
                   (count <= 0 ? "" : $"LIMIT {count} ");
        }

        protected override void GetUpdateCommandTemplate(string table,
            Dictionary<string, object> updateColumns,
            string whereStr,
            DynamicParameters whereParams,
            string orderColumn,
            int count,
            bool asc,
            out string sql,
            out DynamicParameters @params)
        {
            @params = new DynamicParameters();
            var existColumn = new HashSet<string>(whereParams.ParameterNames);
            foreach (var kvp in updateColumns)
            {
                if (!existColumn.Contains(kvp.Key))
                {
                    existColumn.Add(kvp.Key);
                    @params.Add($"upd_{kvp.Key}", kvp.Value);
                }
                else
                {
                    int j = 0;
                    var newStr = kvp.Key + j;
                    while (existColumn.Contains(newStr))
                    {
                        j++;
                        newStr = kvp.Key + j;
                    }

                    @params.Add($"upd_{newStr}", kvp.Value);
                    existColumn.Add(newStr);
                }
            }

            var newDic = new Dictionary<string, string>();
            var o1 = updateColumns.Keys.ToList();
            var o2 = @params.ParameterNames.ToList();
            for (int i = 0; i < o1.Count; i++)
            {
                newDic.Add(o1[i], o2[i]);
            }

            sql = $"UPDATE `{table}` SET " +
                  string.Join(",", newDic.Select(k => $"`{k.Key}`=@{k.Value}")) + " " +
                  $"WHERE {whereStr} ";
        }

        protected override void GetInsertCommandTemplate(string table,
            Dictionary<string, object> insertColumns,
            out string sql,
            out DynamicParameters @params)
        {
            @params = new DynamicParameters();
            foreach (var kvp in insertColumns)
            {
                @params.Add($"ins_{kvp.Key}", kvp.Value);
            }

            sql = $"INSERT INTO {table} (" +
                  string.Join(",", insertColumns.Keys.Select(k => $"`{k}`")) +
                  $") VALUES (" +
                  string.Join(",", insertColumns.Keys.Select(k => $"@ins_{k}")) +
                  $")";
        }

        protected override string GetDeleteCommandTemplate(string table, string whereStr)
        {
            return $"DELETE FROM {table} " +
                   $"WHERE {whereStr} ";
        }
    }
}
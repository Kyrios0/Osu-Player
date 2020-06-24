using System;
using System.Linq.Expressions;
using Dapper;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class WhereBuilder
    {
        public virtual (string sql, DynamicParameters parameters) Set<TEntity>(Expression<Predicate<TEntity>> column)
            where TEntity : class
        {
            var (sql, p) = WhereQueryHelper.GetSqlAndParameters(DataBaseType.MySql, column);
            return (sql, p);
        }
    }
}
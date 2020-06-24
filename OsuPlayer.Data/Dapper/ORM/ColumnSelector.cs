using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.FluentMap.Utils;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class ColumnSelector<TEntity> where TEntity : class
    {
        internal HashSet<MemberInfo> Members { get; } = new HashSet<MemberInfo>();

        public void Set(Expression<Func<TEntity, object>> column)
        {
            Members.Add(ReflectionHelper.GetMemberInfo(column));
        }
    }
}

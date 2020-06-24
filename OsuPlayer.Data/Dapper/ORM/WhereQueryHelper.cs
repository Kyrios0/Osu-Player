using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dapper;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class WhereQueryHelper
    {
        private static MethodInfo _containsMethod;
        private static MethodInfo _startWithMethod;
        private static MethodInfo _endWithMethod;

        static WhereQueryHelper()
        {
            var t = typeof(string);
            _containsMethod = t.GetMethods().First(k => k.Name == nameof(string.Contains));
            _startWithMethod = t.GetMethods().First(k => k.Name == nameof(string.StartsWith));
            _endWithMethod = t.GetMethods().First(k => k.Name == nameof(string.EndsWith));
        }

        public static (string, DynamicParameters) GetSqlAndParameters<TEntity>(DataBaseType dbType, Expression<Predicate<TEntity>> @where) where TEntity : class
        {
            var o = @where.Body;
            int count = 0;
            var dynamicParameters = new DynamicParameters();
            var sql = InnerLoopBody<TEntity>(dbType, o, dynamicParameters, ref count);
            if (!sql.Contains(" "))
            {
                var str = GetValueExpression(true, dynamicParameters, ref count);
                sql = $"{sql} = {str}";
            }

            Console.WriteLine(sql);
            Console.WriteLine("[" + string.Join(", ",
                                  dynamicParameters.ParameterNames.Select(k =>
                                      k + " = " + dynamicParameters.Get<object>(k))) + "]");
            Console.WriteLine(@where.Body + "\r\n");
            return (sql, dynamicParameters);
        }

        private static string InnerLoopBody<TEntity>(DataBaseType dbType, Expression expression, DynamicParameters dp,
            ref int current)
        {
            if (expression is BinaryExpression binExpr)
            {
                var expr1 = InnerLoopBody<TEntity>(dbType, binExpr.Left, dp, ref current);
                var expr2 = InnerLoopBody<TEntity>(dbType, binExpr.Right, dp, ref current);


                switch (expression.NodeType)
                {
                    case ExpressionType.AndAlso:
                        expr1 = CheckExpr<TEntity>(dp, ref current, expr1);
                        expr2 = CheckExpr<TEntity>(dp, ref current, expr2);
                        return $"({expr1}) AND ({expr2})";
                    case ExpressionType.OrElse:
                        expr1 = CheckExpr<TEntity>(dp, ref current, expr1);
                        expr2 = CheckExpr<TEntity>(dp, ref current, expr2);
                        return $"{expr1} OR {expr2}";
                    case ExpressionType.GreaterThan:
                        return expr1 + " > " + expr2;
                    case ExpressionType.GreaterThanOrEqual:
                        return expr1 + " >= " + expr2;
                    case ExpressionType.LessThan:
                        return expr1 + " < " + expr2;
                    case ExpressionType.LessThanOrEqual:
                        return expr1 + " <= " + expr2;
                    case ExpressionType.Equal:
                        if (expr2 == "NULL")
                            return expr1 + " IS NULL";
                        return expr1 + " = " + expr2;
                    case ExpressionType.NotEqual:
                        if (expr2 == "NULL")
                            return expr1 + " IS NOT NULL";
                        return expr1 + " != " + expr2;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var tType = typeof(TEntity);
            if (expression is MemberExpression memberExpr &&
                expression.NodeType == ExpressionType.MemberAccess)
            {
                if (memberExpr.Expression != null && memberExpr.Expression.Type == tType)
                {
                    var colName = ColumnMappingHelper.ColumnMapping[tType][memberExpr.Member];
                    colName = BraceColumnName(dbType, colName);
                    return colName;
                }
                else if (memberExpr.Expression != null && memberExpr.Expression is ConstantExpression consExpr)
                {
                    var type = consExpr.Value.GetType();

                    var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var field = fields.First(k => k == memberExpr.Member);
                    var value = field.GetValue(consExpr.Value);

                    if (memberExpr.Type == typeof(DateTime))
                    {
                        var str = GetValueExpression(((DateTime)value).ToString(ProviderExtension.SqlTimeFormat), dp, ref current);
                        return str;
                    }
                    else
                    {
                        if (value == null)
                        {
                            return "NULL";
                        }

                        var str = GetValueExpression(value, dp, ref current);
                        return str;
                    }
                }
                else if (memberExpr.Member is FieldInfo fieldInfo)
                {
                    if (fieldInfo.Attributes.HasFlag(FieldAttributes.Static))
                    {
                        if (memberExpr.Type == typeof(DateTime))
                        {
                            var time = (DateTime)fieldInfo.GetValue(null);
                            var str = GetValueExpression(time.ToString(ProviderExtension.SqlTimeFormat), dp, ref current);
                            return str;
                        }
                        else
                        {
                            var value = fieldInfo.GetValue(null);
                            if (value == null)
                            {
                                return "NULL";
                            }

                            var str = GetValueExpression(value, dp, ref current);
                            return str;
                        }
                    }

                    throw new NotSupportedException();
                }
                else
                {
                    if (memberExpr.Type == typeof(DateTime))
                    {
                        //var mem = (System.Reflection.RtFieldInfo)memberExpr.Member;
                        if (memberExpr.NodeType == ExpressionType.MemberAccess)
                        {
                            if (memberExpr.Member is PropertyInfo propertyInfo && propertyInfo.Attributes == PropertyAttributes.None)
                            {
                                var time = (DateTime)propertyInfo.GetValue(null);
                                var str = GetValueExpression(time.ToString(ProviderExtension.SqlTimeFormat), dp, ref current);
                                return str;
                            }
                            //return 
                        }
                        else if (memberExpr.NodeType == ExpressionType.Constant)
                        {
                            //return 
                        }
                    }

                    throw new NotSupportedException();
                }
            }
            else if (expression is NewExpression newExpr)
            {
                throw new NotSupportedException("Not support new objects currently");
            }
            else if (expression is ConstantExpression constantExpr &&
                     expression.NodeType == ExpressionType.Constant)
            {
                if (constantExpr.Value == null)
                    return "NULL";
                var str = GetValueExpression(constantExpr.Value, dp, ref current);
                return str;
            }
            else if (expression is UnaryExpression unaryExpr &&
                     expression.NodeType == ExpressionType.Not)
            {
                var o = unaryExpr.Operand;
                if (o is MemberExpression memExpr &&
                    o.NodeType == ExpressionType.MemberAccess)
                {
                    var colName = ColumnMappingHelper.ColumnMapping[tType][memExpr.Member];
                    var str = GetValueExpression(false, dp, ref current);
                    return BraceColumnName(dbType, colName) + " != " + str;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else if (expression is MethodCallExpression methodExpr)
            {
                if (methodExpr.Method == _containsMethod ||
                    methodExpr.Method == _startWithMethod ||
                    methodExpr.Method == _endWithMethod)
                {
                    var o = methodExpr.Object;
                    if (o is MemberExpression memExpr &&
                        o.NodeType == ExpressionType.MemberAccess)
                    {
                        if (memExpr.Expression.Type != tType) throw new NotSupportedException();

                        var colName = ColumnMappingHelper.ColumnMapping[tType][memExpr.Member];
                        var constantExpr1 = (ConstantExpression)methodExpr.Arguments.First();
                        string param = null;
                        if (methodExpr.Method == _containsMethod)
                        {
                            param = $"%{constantExpr1.Value}%";
                        }
                        else if (methodExpr.Method == _startWithMethod)
                        {
                            param = $"{constantExpr1.Value}%";
                        }
                        else if (methodExpr.Method == _endWithMethod)
                        {
                            param = $"%{constantExpr1.Value}";
                        }

                        var sqlExpr = GetValueExpression(param, dp, ref current);
                        return BraceColumnName(dbType, colName) + " LIKE " + sqlExpr;
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    throw new NotSupportedException("Only support string.Contains(), ");
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static string CheckExpr<TEntity>(DynamicParameters dp, ref int current, string expr2)
        {
            if (!expr2.Contains(" "))
            {
                var str = GetValueExpression(true, dp, ref current);
                expr2 = $"{expr2} = {str}";
            }

            return expr2;
        }

        private static string BraceColumnName(DataBaseType dbType, string colName)
        {
            switch (dbType)
            {
                case DataBaseType.SqlServer:
                    return $"[{colName}]";
                case DataBaseType.MySql:
                    return $"`{colName}`";
                case DataBaseType.Access:
                    return $"[{colName}]";
                case DataBaseType.Sqlite:
                    return $"`{colName}`";
                default:
                    throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null);
            }
        }

        private static string GetValueExpression(object value, DynamicParameters dp, ref int current)
        {
            var name = $"P{current}";
            dp.Add(name, value);
            current++;
            return $"@{name}";
        }
    }
}
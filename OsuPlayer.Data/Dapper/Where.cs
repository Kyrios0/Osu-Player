using System;
using System.Data;

namespace Milky.OsuPlayer.Data.Dapper
{
    public class Where
    {
        public Where(string columnName, object value)
        {
            ColumnName = columnName;
            Value = value;
            ColumnNameOther = columnName;
        }

        public WhereType WhereType { get; set; } = WhereType.Equal;
        public string ColumnName { get; set; }
        public string ColumnNameOther { get; set; }
        public object Value { get; set; }
        public DbType? ForceKeyType { get; set; }

        public static implicit operator Where((string columnName, object value) tuple)
        {
            return new Where(tuple.columnName, tuple.value);
        }

        public static implicit operator Where((string columnName, object value, string type) tuple)
        {
            var (columnName, value, type) = tuple;
            return new Where(columnName, value)
            {
                WhereType = SwitchType(type)
            };
        }

        public static implicit operator Where((string columnName, object value, WhereType type) tuple)
        {
            var (columnName, value, type) = tuple;
            return new Where(columnName, value)
            {
                WhereType = type
            };
        }

        public static WhereType SwitchType(string symbol)
        {
            switch (symbol)
            {
                case "=":
                case "==":
                case "===":
                    return WhereType.Equal;
                case "<>":
                case "!=":
                case "!==":
                    return WhereType.Unequal;
                case "<":
                    return WhereType.Less;
                case "<=":
                    return WhereType.LessOrEqual;
                case ">":
                    return WhereType.Greater;
                case ">=":
                    return WhereType.GreaterOrEqual;
                case "%like":
                    return WhereType.LikeAtEnd;
                case "like%":
                    return WhereType.LikeAtBegin;
                case "%like%":
                    return WhereType.LikeAll;
                default:
                    throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null);
            }
        }

        public static string GetTypeSymbol(WhereType whereType)
        {
            switch (whereType)
            {
                case WhereType.Equal:
                    return "=";
                case WhereType.Unequal:
                    return "<>";
                case WhereType.Less:
                    return "<";
                case WhereType.LessOrEqual:
                    return "<=";
                case WhereType.GreaterOrEqual:
                    return ">=";
                case WhereType.Greater:
                    return ">";
                case WhereType.LikeAtEnd:
                case WhereType.LikeAtBegin:
                case WhereType.LikeAll:
                    return "like";
                default:
                    throw new ArgumentOutOfRangeException(nameof(whereType),
                        whereType, null);
            }
        }
    }
}
using System;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class ColumnAttribute : Attribute
    {
        public string ColumnName { get; }

        public int Length { get; set; }

        public object Default { get; set; }

        public string Comment { get; set; }

        public ColumnAttribute(string columnName)
        {
            ColumnName = columnName;
        }
    }

    public class ComputedAttribute : Attribute
    {
        public ComputedAttribute()
        {
        }
    }

    public class KeyAttribute : Attribute
    {
        public KeyAttribute()
        {
        }
    }
}
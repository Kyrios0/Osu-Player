using System;

namespace Milky.OsuPlayer.Data.Dapper.ORM
{
    public class TableAttribute : Attribute
    {
        public string TableName { get; }

        public TableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }
}
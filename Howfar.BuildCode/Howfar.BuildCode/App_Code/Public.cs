﻿using Howfar.BuildCode.Models;
using Panto.Map.Extensions.DAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Howfar.BuildCode.App_Code
{
    public class Public
    {
        public static string MapCsharpType(string dbtype, bool NotNull)
        {
            string strNull = NotNull ? "" : "?";
            if (string.IsNullOrEmpty(dbtype)) return dbtype;
            dbtype = dbtype.ToLower();
            string csharpType = "object";
            switch (dbtype)
            {
                case "bigint": csharpType = "long"; break;
                case "binary": csharpType = "byte[]"; break;
                case "bit": csharpType = "bool" + strNull; break;
                case "char": csharpType = "string"; break;
                case "date": csharpType = "DateTime" + strNull; break;
                case "DateTime": csharpType = "DateTime" + strNull; break;
                case "datetime2": csharpType = "DateTime" + strNull; break;
                case "datetimeoffset": csharpType = "DateTimeOffset"; break;
                case "decimal": csharpType = "decimal" + strNull; break;
                case "float": csharpType = "double" + strNull; break;
                case "image": csharpType = "byte[]"; break;
                case "int": csharpType = "int" + strNull; break;
                case "money": csharpType = "decimal" + strNull; break;
                case "nchar": csharpType = "string"; break;
                case "ntext": csharpType = "string"; break;
                case "numeric": csharpType = "decimal" + strNull; break;
                case "nvarchar": csharpType = "string"; break;
                case "real": csharpType = "Single"; break;
                case "smalldatetime": csharpType = "DateTime" + strNull; break;
                case "smallint": csharpType = "short"; break;
                case "smallmoney": csharpType = "decimal" + strNull; break;
                case "sql_variant": csharpType = "object"; break;
                case "sysname": csharpType = "object"; break;
                case "text": csharpType = "string"; break;
                case "time": csharpType = "TimeSpan"; break;
                case "timestamp": csharpType = "byte[]"; break;
                case "tinyint": csharpType = "byte"; break;
                case "uniqueidentifier": csharpType = "Guid" + strNull; break;
                case "varbinary": csharpType = "byte[]"; break;
                case "varchar": csharpType = "string"; break;
                case "xml": csharpType = "string"; break;
                default: csharpType = "object"; break;
            }
            return csharpType;
        }

        public static void CreateTable(List<Table> List, ConfigInfo Config)
        {
            List<string> strCreateTable = new List<string>();
            List<string> strCreateComment = new List<string>();

            strCreateTable.Add($"CREATE TABLE [dbo].[{Config.TableName}](");
            strCreateComment.Add($"EXEC sp_addextendedproperty N'MS_Description', N'{Config.TableComment}', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', NULL, NULL; ");
            foreach (var item in List)
            {
                string length = item.TypeName.Contains("varchar") ? $"({item.MaxLength.ToString()})" : "";
                strCreateTable.Add(string.Format($"[{item.ColumnName}] {item.TypeName}{{0}} {{1}} {{2}},",
                    length,
                    item.NotNUll ? "Not Null" : "Null",
                    item.DefaultValue?.Length > 0 ? $"'{item.DefaultValue}'" : ""
                    ));
                strCreateComment.Add($"EXEC sp_addextendedproperty N'MS_Description', N'{item.Comment}', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'{item.ColumnName}'; ");
            }
            strCreateTable.Add(@"   [Timestamp] [timestamp] NULL,
                                    [SchoolID] [uniqueidentifier] NOT NULL,
                                    [CreateUser] [nvarchar] (50) COLLATE Chinese_PRC_CI_AS NULL,
                                    [CreateDate] [datetime] NULL,
                                    [UpdateUser] [nvarchar] (50) COLLATE Chinese_PRC_CI_AS NULL,
                                    [UpdateDate] [datetime] NULL,");
            strCreateTable.Add($"PRIMARY KEY ( [{List[0].ColumnName}] ));");
            strCreateComment.Add($@" EXEC sp_addextendedproperty N'MS_Description', N'时间戳', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'Timestamp' 
                                    EXEC sp_addextendedproperty N'MS_Description', N'学校ID', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'SchoolID' 
                                    EXEC sp_addextendedproperty N'MS_Description', N'创建人', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'CreateUser' 
                                    EXEC sp_addextendedproperty N'MS_Description', N'创建时间', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'CreateDate' 
                                    EXEC sp_addextendedproperty N'MS_Description', N'修改人', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'UpdateUser' 
                                    EXEC sp_addextendedproperty N'MS_Description', N'修改时间', 'SCHEMA', N'dbo', 'TABLE', N'{Config.TableName}', 'COLUMN', N'UpdateDate' ");
            CPQuery.From(string.Join("", strCreateTable)).ExecuteNonQuery();
            CPQuery.From(string.Join("", strCreateComment)).ExecuteNonQuery();

        }

    }
}
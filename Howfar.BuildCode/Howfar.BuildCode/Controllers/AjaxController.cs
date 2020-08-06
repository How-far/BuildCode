using System.Collections.Generic;
using System.Web.Mvc;
using Howfar.BuildCode.Models;
using Panto.Map.Extensions.DAL;

namespace Howfar.BuildCode.Controllers
{
    public class AjaxController : Controller
    {


        #region 获取数据表
        public JsonResult GetTableList()
        {
            string strSQL = @"  SELECT  o.object_id AS TableID ,
                                        o.name AS TableName,
                                        o.create_date AS CreateDate ,
                                        o.modify_date AS ModifyDate,
                                        CAST(ep.value AS NVARCHAR(MAX)) Comment ,
                                        COUNT(1) AS FieldCount
                                FROM    sys.all_objects o
                                        LEFT JOIN sys.schemas s ON o.schema_id = s.schema_id
                                        LEFT JOIN sys.tables t ON o.object_id = t.object_id
                                        LEFT JOIN sys.extended_properties ep ON ( o.object_id = ep.major_id
                                                                                  AND ep.class = 1
                                                                                  AND ep.minor_id = 0
                                                                                  AND ep.name = 'MS_Description'
                                                                                )
                                        LEFT JOIN ( SELECT  object_id ,
                                                            SUM(rows) row_count
                                                    FROM    sys.partitions
                                                    WHERE   index_id < 2
                                                    GROUP BY object_id
                                                  ) st ON o.object_id = st.object_id
                                        LEFT JOIN sys.change_tracking_tables ct ON o.object_id = ct.object_id
                                        LEFT JOIN sys.all_columns ac ON ac.object_id = o.object_id
                                WHERE   s.name = N'dbo'
                                        AND ( o.type = 'U'
                                              OR o.type = 'S'
                                            )
                                GROUP BY o.object_id ,
                                        o.name ,
                                        o.create_date ,
                                        o.modify_date ,
                                        ep.value
                                ORDER BY o.name;
                                ";
            return Json(CPQuery.From(strSQL).ToList<Table>(), JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region 获取字段
        public JsonResult GetTableDetail(string TableName)
        {
            var strExclude = new string[] { "ID", "Timestamp", "", "IsDeleted", "CreateTime", "CreateUser", "UpdateUser", "UpdateTime" };

            string strSQL = $@"  SELECT  '{TableName}' AS TableName,
                                        c.name AS ColumnName ,
                                        c.column_id  AS ColumsID,
                                        t.name AS TypeName ,
                                        c.max_length AS MaxLength ,
                                        c.precision AS Precision,
                                        c.scale AS Scale,
                                        c.collation_name ,
                                        c.is_xml_document ,
                                        CAST(CASE WHEN ( do.parent_object_id = 0 ) THEN 1
                                                  ELSE 0
                                             END AS BIT) AS is_default_binding ,
                                        o3.name rule_name ,
                                        c.is_sparse ,
                                        c.is_column_set ,
                                        c.is_filestream ,
                                        CAST(ep.value AS NVARCHAR(MAX)) AS Comment,
                                        CAST(CASE WHEN c.is_nullable=0 THEN 1 ELSE 0 END AS BIT) AS NotNUll,
                                        1 AS IsCheck,
                                        1 AS IsDataColumn
                                FROM    sys.all_columns c
                                        LEFT JOIN sys.all_objects o ON c.object_id = o.object_id
                                        LEFT JOIN sys.schemas s ON o.schema_id = s.schema_id
                                        LEFT JOIN sys.types t ON c.user_type_id = t.user_type_id
                                        LEFT JOIN sys.all_objects do ON c.default_object_id = do.object_id
                                        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
                                        LEFT JOIN sys.all_objects o3 ON c.rule_object_id = o3.object_id
                                        LEFT JOIN sys.identity_columns id ON c.object_id = id.object_id
                                                                             AND c.column_id = id.column_id
                                        LEFT JOIN sys.computed_columns cc ON c.object_id = cc.object_id
                                                                             AND c.column_id = cc.column_id
                                        LEFT JOIN sys.extended_properties ep ON ( c.object_id = ep.major_id
                                                                                  AND ep.class = 1
                                                                                  AND c.column_id = ep.minor_id
                                                                                  AND ep.name = 'MS_Description'
                                                                                )
                                WHERE   s.name = N'dbo'
                                        AND o.type IN ( 'U', 'S', 'V' )
                                        AND o.name = N'{TableName}' 
                                        AND c.name Not IN('{ string.Join("','", strExclude)}')
                                        ORDER BY c.column_id;";
            List<Table> List = CPQuery.From(strSQL).ToList<Table>();
            foreach (var item in List)
            {
                switch (item.TypeName)
                {
                    case "nvarchar":
                        item.MaxLength = (int.Parse(item.MaxLength) / 2).ToString();
                        break;
                    case "decimal":
                        item.MaxLength = $"({item.Precision},{item.Scale})";
                        break;
                    case "varchar":
                        break;
                    default:
                        item.MaxLength = string.Empty;
                        break;
                }
            }
            return Json(List, JsonRequestBehavior.AllowGet);
        }

        #endregion


    }
}
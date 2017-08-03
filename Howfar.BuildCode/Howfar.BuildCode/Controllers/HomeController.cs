﻿using Howfar.BuildCode.App_Code;
using Howfar.BuildCode.Models;
using Panto.Map.Extensions.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Howfar.BuildCode.Controllers
{
    public class HomeController : Controller
    {
        static List<Table> StaticDataList = new List<Table>();
        static ConfigInfo StaticConfigInfo = new ConfigInfo();
        public ActionResult Index()
        {
            return View();
        }

        public string SetData(List<Table> DataList, ConfigInfo ConfigInfo)
        {
            try
            {
                StaticDataList = DataList != null ? DataList.Where(t => t.IsCheck == true).ToList() : StaticDataList;
                StaticConfigInfo = ConfigInfo;
                List<Table> PKList = new List<Table>();
                if (StaticDataList != null && StaticDataList.Count > 0)
                {
                    PKList = GetPKList(ConfigInfo.TableName);
                }
                for (int i = 0; i < StaticDataList.Count; i++)
                {
                    StaticDataList[i].CommentSimple = PublicHelper.SplitComment(StaticDataList[i].Comment);
                    StaticDataList[i].IsPK = PKList.Where(t => t.ColumnName == StaticDataList[i].ColumnName).Count() > 0;
                    if (StaticDataList[i].IsPK.Value && StaticConfigInfo.PKName == null) //保存 主键 名称
                    {
                        StaticConfigInfo.PKName = StaticDataList[i].ColumnName;
                    }
                    StaticDataList[i].CsharpType = PublicHelper.MapCsharpType(StaticDataList[i].TypeName, StaticDataList[i].NotNUll);
                }
                if (StaticConfigInfo.EventName != "CreateTable" && StaticConfigInfo.PKName.Length < 0)
                {
                    return "未获取到主键！";
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        #region · GetPKList
        public List<Table> GetPKList(string TableName)
        {
            string sql = $@"SELECT  col.name ColumnName
                            FROM    sys.key_constraints c
                                    LEFT JOIN sys.indexes i ON c.parent_object_id = i.object_id
                                                                AND c.unique_index_id = i.index_id
                                    LEFT JOIN sys.index_columns ic ON i.object_id = ic.object_id
                                                                        AND i.index_id = ic.index_id
                                                                        AND ic.key_ordinal > 0
                                    LEFT JOIN sys.all_columns col ON ic.column_id = col.column_id
                                                                        AND ic.object_id = col.object_id
                                    LEFT JOIN sys.schemas s ON c.schema_id = s.schema_id
                                    LEFT JOIN sys.objects o ON c.parent_object_id = o.object_id
                                    LEFT JOIN sys.extended_properties ep ON c.object_id = ep.major_id
                                                                            AND ep.minor_id = 0
                                                                            AND ep.class = 1
                                                                            AND ep.name = 'MS_Description'
                            WHERE   s.name = N'dbo'
                                    AND o.type IN ( 'U', 'S', 'V' )
                                    AND o.name = N'{TableName}'";
            return CPQuery.From(sql).ToList<Table>();
        }

        #endregion

        #region · BuildEditHTML
        public ActionResult BuildEditHTML()
        {
            ViewBag.Content = EditHtml();
            Table Entity = new Table();
            Entity.EntityList = StaticDataList;
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }

        public string EditHtml()
        {
            List<string> sb = new List<string>();
            int Index = 0;

            //排除主键
            List<Table> List = StaticDataList.Where(t => t.ColumnName != StaticConfigInfo.PKName).ToList();
            int Count = List.Count;
            foreach (var item in List)
            {
                //扩展字段跳过
                if (item.ParentName.Length > 0) { continue; }
                //日期样式
                string DateClass = item.TypeName.ToLower().Contains("date") ? @"class=""dateShow chooseDate""" : string.Empty;
                //是否必填
                var IsValidate = item.IsValidate ? @"<span style=""color: red; "">*</span>" : "";
                //最大长度
                var strLength = item.MaxLength > 0 ? $@"maxlength=""{item.MaxLength}""" : "";
                //Value
                string strValue = $"@Model.{ item.ColumnName}";
                if (DateClass.Length > 0)
                {
                    strValue = $"@(Model.{item.ColumnName}.HasValue?Model.{item.ColumnName}.Value.ToString(\"yyyy-MM-dd\"):\"\")";
                }
                if (Index % 2 == 0) { sb.Add("<div class=\"form-group maginWidth\">"); }
                sb.Add($"    <label class=\"col-xs-2 control-label form-left\">{IsValidate}{item.CommentSimple}</label>");
                sb.Add("    <div class=\"col-xs-3 form-center\">");
                var Kz = List.Where(t => t.ParentName == item.ColumnName);
                if (Kz.Count() > 0)
                {//*****扩展字段*****
                    sb.Add($"        <input type=\"text\" id=\"{Kz.FirstOrDefault().ColumnName}\" name=\"{Kz.FirstOrDefault().ColumnName}\" value=\"@Model.{Kz.FirstOrDefault().ColumnName}\" {strLength}/>");
                    sb.Add($"        <input type=\"hidden\" id=\"{item.ColumnName}\" name=\"{item.ColumnName}\" value=\"@Model.{item.ColumnName}\" />");
                }
                else
                {
                    sb.Add($"        <input type=\"text\" {DateClass} id=\"{item.ColumnName}\" name=\"{item.ColumnName}\" value=\"{strValue}\" {strLength}/>");
                }
                sb.Add("    </div>");
                sb.Add("    <div class=\"col-xs-1 form-right\"></div>");
                if ((Index + 1) == Count || Index % 2 == 1) { sb.Add("</div>"); }
                Index++;
            }
            sb = sb.Where(t => t != string.Empty).ToList();
            return string.Join("\r\n", sb);
        }

        #endregion


        #region · BuildListJS
        public ActionResult BuildListJS()
        {
            ViewBag.ListJSTitleContent = ListJS();
            Table Entity = new Table();
            Entity.EntityList = StaticDataList;
            Entity.ConfigInfo = StaticConfigInfo;
            ViewBag.ListJSCond = ListJSCond();
            return View(Entity);
        }

        private string ListJS()
        {
            List<string> sb = new List<string>();
            List<Table> List = StaticDataList.Where(t => t.IsShow == true).ToList();
            int Index = 0, Count = List.Count;
            foreach (var item in List)
            {
                sb.Add("            {");
                sb.Add($"               selectName: '{item.ColumnName}',");
                sb.Add($"               name: '{item.Comment}',");
                sb.Add($"               width: '150px',");
                sb.Add($"               sortable: 'true',");
                //sb.Add($"             type: 'link',");
                sb.Add(string.Format("               align: '{0}',", item.TypeName.Contains("int") ? "right" : "left"));
                if (item.TypeName.ToLower().Contains("date"))
                {
                    sb.Add("            fn: function (e) {");
                    sb.Add("                if (e != null && e != '') {");
                    sb.Add($"                   return $.JsonToDateTimeString(e, 'yyyy-MM-dd');");
                    sb.Add("                } else {");
                    sb.Add($"                   return '';");
                    sb.Add("                }");
                    sb.Add("            }");
                }
                sb.Add(string.Format("            }}{0}", (Index + 1) == Count ? "" : ","));
                Index++;
            }

            return string.Join("\r\n", sb).Replace(",\r\n            }", "\r\n            }");
        }
        private string ListJSCond()
        {
            List<string> sb = new List<string>();
            var List = StaticDataList.Where(t => t.IsCondition == true).ToList();
            foreach (var item in List)
            {
                sb.Add($@"        {item.ColumnName}:$.trim($('#{item.ColumnName}').val())");
            }
            return string.Join(",\r\n", sb);
        }
        #endregion

        //public ActionResult test(List<Table> DataList)
        //{
        //    StaticDataList = DataList != null ? DataList : StaticDataList;
        //    ViewBag.Content = BuildEditHtml(StaticDataList);
        //    return View();
        //}

        #region · BuildEntity
        public ActionResult BuildEntity()
        {
            Table Entity = new Table();
            ViewBag.NormalContent = strNormalEntity();
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }
        public string strNormalEntity()
        {
            List<string> sb = new List<string>();
            List<Table> List = StaticDataList.Where(t => t.IsDataColumn == true).ToList();
            sb.Add("        #region 标准字段");
            foreach (var item in List)
            {
                sb.Add("        /// <summary>");
                sb.Add($"        /// {item.Comment}");
                sb.Add("        /// </summary>");
                sb.Add("        [DataMember] ");
                if (item.IsDataColumn && item.IsPK.Value)
                {
                    sb.Add("        [DataColumn(PrimaryKey = true)] ");
                }
                else if (item.IsDataColumn)
                {
                    sb.Add(string.Format("        [DataColumn(IsNullable = {0})] ", item.NotNUll ? "false" : "true"));
                }
                sb.Add($"        [Description(\"{item.Comment}\")] ");
                sb.Add($"        public {item.CsharpType} {item.ColumnName} {{ get; set; }}");
            }
            sb.Add("        #endregion");
            sb.Add("");
            sb.Add("        #region 扩展字段");
            List = StaticDataList.Where(t => t.IsDataColumn == false).ToList();
            foreach (var item in List)
            {
                sb.Add("        /// <summary>");
                sb.Add($"        /// {item.Comment}");
                sb.Add("        /// </summary>");
                sb.Add("        [DataMember] ");
                if (item.IsDataColumn && item.IsPK.Value)
                {
                    sb.Add("        [DataColumn(PrimaryKey = true)] ");
                }
                else if (item.IsDataColumn)
                {
                    sb.Add(string.Format("        [DataColumn(IsNullable = {0})] ", item.NotNUll ? "false" : "true"));
                }
                sb.Add($"        [Description(\"{item.Comment}\")] ");
                sb.Add($"        public {item.CsharpType} {item.ColumnName} {{ get; set; }}");
            }
            sb.Add("        #endregion");
            return string.Join("\r\n", sb);
        }
        #endregion



        #region  · Build Dal
        public ActionResult BuildDal()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            var t = strDalContent();
            ViewBag.sbCond = t.Item1;
            ViewBag.sbParam = t.Item2;
            return View(Entity);
        }
        private Tuple<string, string> strDalContent()
        {
            // 过滤 非 条件 字段
            List<Table> List = StaticDataList.Where(t => t.IsCondition == true).ToList();
            List<string> sbCond = new List<string>();
            List<string> sbParam = new List<string>();
            foreach (var item in List)
            {
                string Islike = item.TypeName.Contains("char") ? $" LIKE '%' + @{item.ColumnName} + '%' " : $"= @{item.ColumnName}";
                sbCond.Add($@"            string {item.ColumnName} =string.Empty;
            if (jo[""{item.ColumnName}""] != null && !string.IsNullOrEmpty(jo[""{item.ColumnName}""].ToString()))
            {{
                   sql += "" AND a.{item.ColumnName} {Islike} "";
                   {item.ColumnName} = jo[""{item.ColumnName}""].ToString();
             }}");
                sbParam.Add($"                {item.ColumnName} = {item.ColumnName},");
            }
            return new Tuple<string, string>(string.Join("\r\n", sbCond), string.Join("\r\n", sbParam));
        }
        #endregion


        public ActionResult BuildBLL()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }

        public ActionResult BuildController()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }

        public ActionResult BuildListHtml()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            Entity.EntityList = StaticDataList.Where(t => t.IsCondition == true).ToList();
            return View(Entity);
        }
        public ActionResult BuildEditJS()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            ViewBag.strEditJS = strEditJS();
            return View(Entity);
        }
        public string strEditJS()
        {
            List<string> sb = new List<string>();
            List<Table> List = StaticDataList.Where(t => t.IsValidate == true).ToList();
            foreach (var item in List)
            {
                sb.Add($"                {item.ColumnName}:{{required: true}}");
            }
            return string.Join(",\r\n", sb);
        }
        #region · CreateTable
        public ActionResult CreateTable()
        {
            ViewBag.SQLContent = PublicHelper.CreateTable(StaticDataList, StaticConfigInfo);
            return View();
        }
        #endregion
    }
}
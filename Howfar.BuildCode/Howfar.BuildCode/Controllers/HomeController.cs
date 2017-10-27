using Howfar.BuildCode.App_Code;
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
using System.Configuration;
using System.Xml;
using RazorEngine;

namespace Howfar.BuildCode.Controllers
{
    public class HomeController : Controller
    {
        static List<Table> StaticDataList = new List<Table>();
        static ConfigInfo StaticConfigInfo = new ConfigInfo();
        public ActionResult Index()
        {
            ViewBag.strCon = ConfigurationManager.ConnectionStrings["PDRZ_Integration"];
            return View();
        }

        public string SetCon(string strcon)
        {
            if (string.IsNullOrEmpty(strcon))
            {
                return "值不可为空！";
            }
            string key = "PDRZ_Integration";
            string value = strcon;
            try
            {
                SqlConnection con = new SqlConnection(value);
                con.Open();
                if (con.State == ConnectionState.Open)
                {
                    con.Close();


                    //ConfigurationManager.ConnectionStrings["PDRZ_Integration"].ConnectionString = strcon;
                    //Configuration cfa = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    //cfa.AppSettings.Settings["con"].Value = "name";
                    //cfa.Save();
                    //ConfigurationManager.RefreshSection("appSettings");
                    System.Xml.XmlDocument xDoc = new System.Xml.XmlDocument();
                    xDoc.Load(HttpContext.Server.MapPath("~/db.config"));
                    XmlNode xNode;
                    XmlElement xElem1;
                    XmlElement xElem2;
                    xNode = xDoc.SelectSingleNode("//connectionStrings");
                    xElem1 = (System.Xml.XmlElement)xNode.SelectSingleNode("//add[@name='" + key + "']");
                    if (xElem1 != null) xElem1.SetAttribute("connectionString", value);
                    else
                    {
                        xElem2 = xDoc.CreateElement("add");
                        xElem2.SetAttribute("key", key);
                        xElem2.SetAttribute("value", value);
                        xNode.AppendChild(xElem2);
                    }
                    xDoc.Save(HttpContext.Server.MapPath("~/db.config"));
                    return "设置成功！";
                }
                else
                {
                    return "设置失败！";
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public string SetData(List<Table> DataList, ConfigInfo ConfigInfo)
        {
            try
            {
                //if (StaticDataList == null || DataList.SequenceEqual(StaticDataList))
                //{
                //    return "相等";
                //}
                //else
                //{
                //    return "不相等";
                //}
                StaticDataList = DataList != null ? DataList.Where(t => t.IsCheck == true).ToList() : StaticDataList;
                StaticConfigInfo = ConfigInfo;

                //字典 Code List
                List<string> CodeList = new List<string>();
                try
                {
                    CodeList = CPQuery.From("SELECT TypeCode FROM dbo.KH_DataDictionaryType").FillScalarList<string>();
                }
                catch (Exception)
                {
                }


                #region  · 获取 PKList

                List<Table> PKList = new List<Table>();
                if (StaticDataList != null && StaticDataList.Count > 0)
                {
                    PKList = GetPKList(ConfigInfo.TableName);
                }

                #endregion

                for (int i = 0; i < StaticDataList.Count; i++)
                {
                    //处理 注释 Simple
                    StaticDataList[i].CommentSimple = PublicHelper.SplitComment(StaticDataList[i].Comment);
                    //查找 主键
                    StaticDataList[i].IsPK = PKList.Where(t => t.ColumnName == StaticDataList[i].ColumnName).Count() > 0;
                    if (StaticDataList[i].IsPK.Value && StaticConfigInfo.PKName == null) //保存 主键 名称
                    {
                        StaticConfigInfo.PKName = StaticDataList[i].ColumnName;
                    }
                    //转成 Csharp 数据类型
                    StaticDataList[i].CsharpType = PublicHelper.MapCsharpType(StaticDataList[i].TypeName, StaticDataList[i].NotNUll);
                    //是否 字典表 字段
                    string Code = PublicHelper.IsCode(StaticDataList[i].ColumnName, CodeList);
                    StaticDataList[i].IsCodeField = Code.Length > 0;
                    if (!StaticConfigInfo.IsViewData && Code.Length > 0)
                    {
                        StaticConfigInfo.IsViewData = true;
                    }
                }

                if (StaticConfigInfo.EventName != "CreateTable" && StaticConfigInfo.PKName.Length < 0)
                {
                    return "未获取到主键！";
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Exception:" + ex.Message;
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

            #region · 判断 实体 渲染方式（默认、ViewData）

            //[默认值] 赋值来源
            string strModel = "Model";
            //[默认值] 引用 命名空间
            string strEditModePath = $"@model PDRZ.Integration.Entity.School.{StaticConfigInfo.ModelFolderName}.{StaticConfigInfo.EntityName}";
            /*
              在本页面中是否 有 字典，有 则 需要使用 ViewData 渲染
            */
            if (StaticConfigInfo.IsViewData)
            {
                strEditModePath = $"@model PDRZ.Integration.Entity.TransferData.School.{StaticConfigInfo.ModelFolderName}.{StaticConfigInfo.EntityName}ViewData";
                strModel = "Model.Entity";
            }
            ViewBag.strEditModePath = strEditModePath;

            #endregion

            foreach (var item in List)
            {
                //扩展字段跳过
                if (item.ParentName?.Length > 0) { continue; }
                //日期样式
                string DateClass = item.TypeName.ToLower().Contains("date") ? @"class=""dateShow chooseDate""" : string.Empty;
                //是否必填
                var IsValidate = item.IsValidate ? @"<span style=""color: red; "">*</span>" : "";
                //最大长度
                int MaxLength = 0;
                int.TryParse(item.MaxLength, out MaxLength);
                var strLength = MaxLength > 0 ? $@"maxlength=""{item.MaxLength}""" : "";
                //Value
                string strValue = $"@{strModel}.{ item.ColumnName}";
                if (DateClass.Length > 0)
                {  //日期 字段 赋值
                    strValue = $"@({strModel}.{item.ColumnName}.HasValue?{strModel}.{item.ColumnName}.Value.ToString(\"yyyy-MM-dd\"):\"\")";
                }

                if (Index % 2 == 0) { sb.Add("<div class=\"form-group maginWidth\">"); }
                sb.Add($"    <label class=\"col-xs-2 control-label form-left\">{IsValidate}{item.CommentSimple}</label>");
                sb.Add("    <div class=\"col-xs-3 form-center\">");

                if (item.IsCodeField)//是否字典 字段
                {
                    sb.Add($@"        <select name=""{item.ColumnName}"" class=""selectpicker show-tick"">");
                    sb.Add(@"            <option value="""">请选择</option>");
                    sb.Add($"            @foreach (var item in Model.{item.ColumnName}List)");
                    sb.Add("             {");
                    sb.Add($@"              <option value=""@item.DictionaryCode"" @(Model.Entity.{item.ColumnName} == item.DictionaryCode ? ""selected"" : """") > @item.DictionaryName </option > ");
                    sb.Add("             }");
                    sb.Add("         </select>");
                }
                else
                {
                    var Kz = List.Where(t => t.ParentName == item.ColumnName);
                    if (Kz.Count() > 0)
                    {//*****扩展字段*****
                        sb.Add($"        <input type=\"text\" id=\"{Kz.FirstOrDefault().ColumnName}\" name=\"{Kz.FirstOrDefault().ColumnName}\" value=\"@{strModel}.{Kz.FirstOrDefault().ColumnName}\" {strLength}/>");
                        sb.Add($"        <input type=\"hidden\" id=\"{item.ColumnName}\" name=\"{item.ColumnName}\" value=\"@{strModel}.{item.ColumnName}\" />");
                    }
                    else
                    {
                        sb.Add($"        <input type=\"text\" {DateClass} id=\"{item.ColumnName}\" name=\"{item.ColumnName}\" value=\"{strValue}\" {strLength}/>");
                    }
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
            //ViewBag.ListJSTitleContent = ListJS();
            Table Entity = new Table();
            Entity.EntityList = StaticDataList;
            Entity.ConfigInfo = StaticConfigInfo;
            var ListJSCond = "";
            var ListJSTitleContent = "";
            List<Table> List = StaticDataList.Where(t => t.IsShow == true).ToList();
            //ViewBag.ListJSCond = ListJSCond();
            string OName = StaticConfigInfo.PageName + "List";
            string tplPath = Server.MapPath("~/Templates/") + "TplListJs.cshtml";
            string tpl = System.IO.File.ReadAllText(tplPath);
            string content = Razor.Parse(tpl, new { OName, List, ListJSCond, ConfigInfo = StaticConfigInfo });
            ViewBag.content = content;
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
                if (Index == 0)
                {
                    sb.Add($"               type: 'link',");
                }
                string align = "left";
                if (item.TypeName.Contains("int"))
                {
                    align = "right";
                }
                else if (item.TypeName.ToLower().Contains("date"))
                {
                    align = "center";
                }
                sb.Add(string.Format("               align: '{0}',", align));
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


        #region · BuildEntity
        public ActionResult BuildEntity()
        {
            Table Entity = new Table();
            var ColList = StaticDataList.Where(t => t.IsDataColumn == true).ToList();
            var ExtColList = StaticDataList.Where(t => t.IsDataColumn == false).ToList();
            string tplPath = Server.MapPath("~/Templates/") + "TplEntity.cshtml";
            string tpl = System.IO.File.ReadAllText(tplPath);
            string content = Razor.Parse(tpl, new { ColList, ExtColList, ConfigInfo = StaticConfigInfo });
            ViewBag.NormalContent = content;
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

        #region · BuildBLL

        public ActionResult BuildBLL()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }

        #endregion

        #region · BuildController

        public ActionResult BuildController()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            //ViewBag.strControllerCode = strControllerCode();
            string tplPath = Server.MapPath("~/Templates/") + "TplController.txt";
            string tpl = System.IO.File.ReadAllText(tplPath);
            string content = Razor.Parse(tpl, Entity);
            //return Content(content);
            ViewBag.strControllerCode = content;
            return View(Entity);
        }

        public string strControllerCode()
        {
            List<Table> List = StaticDataList.Where(t => t.IsCodeField == true).ToList();
            List<string> sb = new List<string>();
            foreach (var item in List)
            {
                var code = item.ColumnName.Substring(0, item.ColumnName.Length - 4);
                sb.Add($@"            vd.{code}CodeList = dyBLL.GetListByType(""{code}"", this.SchoolID);");
            }
            return string.Join("\r\n", sb);
        }

        #endregion


        #region · BuildListHtml


        public ActionResult BuildListHtml()
        {
            Table Entity = new Table();
            Entity.ConfigInfo = StaticConfigInfo;
            Entity.EntityList = StaticDataList.Where(t => t.IsCondition == true).ToList();
            return View(Entity);
        }

        #endregion

        #region · BuildEditJS

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

        #endregion

        #region · CreateTable
        public ActionResult CreateTable()
        {
            ViewBag.SQLContent = PublicHelper.CreateTable(StaticDataList, StaticConfigInfo);
            return View();
        }
        #endregion

        #region · BuildEntityViewData

        public ActionResult BuildEntityViewData()
        {
            Table Entity = new Table();
            ViewBag.strEntityViewData = strEntityViewData();
            Entity.ConfigInfo = StaticConfigInfo;
            return View(Entity);
        }
        public string strEntityViewData()
        {

            List<string> sb = new List<string>();
            List<Table> List = StaticDataList.Where(t => t.IsCodeField == true).ToList();
            foreach (var item in List)
            {
                var code = item.ColumnName.Substring(0, item.ColumnName.Length - 4);
                sb.Add($"        public List<DataDictionary> {code}CodeList {{ get; set; }}");
            }
            return string.Join("\r\n", sb);
        }
        #endregion

        #region · BuildMenuSql
        public ActionResult BuildMenuSql(string id)
        {
            List<string> List = new List<string>();
            DataTable dtMenu = CPQuery.From("SELECT * FROM dbo.PDRZ_Menu WHERE MenuID =@MenuID", new { MenuID = id }).FillDataTable();
            DataTable dtFunction = CPQuery.From("SELECT * FROM dbo.PDRZ_MenuFunction WHERE MenuID = @MenuID", new { MenuID = id }).FillDataTable();

            foreach (DataRow dr in dtMenu.Rows)
            {
                List.Add($"-- 菜单 {dr[1]}");
                List.Add($"DELETE FROM dbo.PDRZ_Menu WHERE MenuID ='{dr[0]}'");
                List.Add($"INSERT INTO PDRZ_Menu SELECT '{dr[0]}', '{dr[1]}', '{dr[2]}', '{dr[3]}', '{dr[4]}', '{dr[5]}', {dr[6]}, '{dr[7]}', GETDATE(), '', ''");
            }
            foreach (DataRow dr in dtFunction.Rows)
            {
                List.Add($"-- {dr[1]}");
                List.Add($"DELETE  FROM PDRZ_MenuFunction WHERE FunctionID = '{dr[0]}';");
                List.Add($"INSERT INTO PDRZ_MenuFunction SELECT '{dr[0]}', '{dr[1]}', '{dr[2]}', {dr[3]}, '{dr[4]}', '{dr[5]}', GETDATE(), '', '', '{dr[9]}'");
            }
            ViewBag.SQLContent = string.Join("\r\n", List);
            return View();
        }
        #endregion
    }
}
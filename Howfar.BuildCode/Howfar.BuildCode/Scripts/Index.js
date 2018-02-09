var nullField = { "Comment": "", "ColumnName": "", "ColumsID": 0, "TypeName": "", "MaxLength": null };
var applist;
var storage;

(function ($) {
    jQuery.expr[':'].Contains = function (a, i, m) {
        return (a.textContent || a.innerText || "").toUpperCase().indexOf(m[3].toUpperCase()) >= 0;
    };
    function filterList(header, list) {
        var form = $("<form>").attr({ "class": "filterform", "action": "#" });
        $(form).appendTo(header);

        $('#txtSearch').change(function () {
            var filter = $(this).val();
            if (filter) {
                $matches = $(list).find('h4:Contains(' + filter + ')').parent();
                $('a', list).not($matches).slideUp();
                $matches.slideDown();
            } else {
                $(list).find("a").slideDown();
            }
            return false;
        }).keyup(function () {
            $(this).change();
        }).on('paste', function () {
            setTimeout(function () {
                var name = $('#txtSearch').val();
                $("a[tablename='" + name + "']")[0].click();
            }, 100);
        });
        $('#txtSearch').val(storage["SearchText"]);
        $('#txtSearch').change();
    }
    $(function () {
        $.getJSON('/Ajax/gettablelist', function (data) {
            createVue(data);
            init();
            filterList($("#form"), $("#list"));
        });

    });
}(jQuery));

function init() {
    storage = window.localStorage;
    $('#txtModelFolderName').val(storage["ModelFolderName"]);
    $('#txtFolderPath').val(storage["FolderPath"]);
    bindType();
}

function bindType() {
    //setTimeout(function () {
    //$(".SelectType").autocompleteArray([
    //    'bigint', 'binary', 'bit', 'char', 'date', 'datetime', 'datetime2', 'datetimeoffset',
    //    'decimal', 'float', 'geography', 'geometry', 'hierarchyid', 'image', 'int', 'money',
    //    'nchar', 'ntext', 'numeric', 'nvarchar', 'real', 'smalldatetime', 'smallint', 'smallmoney',
    //    'sql_variant', 'sysname', 'text', 'time', 'timestamp', 'tinyint', 'uniqueidentifier',
    //    'varbinary', 'varchar', 'xml'
    //], {
    //        delay: 10,
    //        minChars: 0,
    //        matchSubset: 1,
    //        width: 400, //提示的宽度
    //        scrollHeight: 300, //提示的高度
    //        // onItemSelect:selectItem,
    //        // onFindValue:findValue,
    //        autoFill: true,
    //        maxItemsToShow: 10
    //    });
    //}, 500);
}

function createVue(data) {
    applist = new Vue({
        el: "#list"
        , data: {
            tableList: data,
            fieldList: [nullField]
        }
        , created: function () { }
        , methods: {
            init: function () { },
            getTableInfo: function (tableName, tablecomment, e) {
                var element = $(e.currentTarget);
                $('.list-group-item').removeClass('active');
                element.addClass('active');
                $('#txtTableName').val(tableName);
                $('#txtEntityName').val(tableName.substr(tableName.indexOf('_') + 1));
                $('#txtTableComment').val(tablecomment);
                storage["SearchText"] = $("#txtSearch").val();

                if (storage[tableName] && storage[tableName].length > 0) {
                    applist.fieldList = JSON.parse(storage[tableName]);
                    toastr.options.onclick = function () {
                        storage.removeItem(tableName);
                    }
                    toastr['warning']("单击清除缓存", tableName + " 表已加载缓存数据!");
                    return;
                }
                $.getJSON('/Ajax/GetTableDetail?TableName=' + tableName, function (fieldData) {
                    applist.fieldList = fieldData;
                    bindType();
                });
            },
            addRow: function (index) {
                applist.fieldList.splice(index + 1, 0, {
                    IsCheck: true,
                    IsDataColumn: true,
                    TypeName: ''
                });
            },
            delRow: function (index) {
                applist.fieldList.splice(index, 1);
            },
            appendRow: function (index) {
                var name = applist.fieldList[index].ColumnName;
                var comment = applist.fieldList[index].Comment;
                var IsHave = false;
                applist.fieldList.forEach(function (item) {
                    if (item.ParentName != null && name == item.ParentName) {
                        IsHave = true;
                        return;
                    }
                });
                if (IsHave) { toastr['error']("该字段 已存在扩展字段!"); return; }
                if (name.indexOf('ID') >= 0) {
                    applist.fieldList.push({
                        IsCheck: true,
                        ParentName: name,
                        ColumnName: name.substr(0, name.length - 2) + 'Name',
                        Comment: comment.substr(0, comment.length - 2) + '名称',
                        TypeName: 'nvarchar',
                        MaxLength:50
                    });
                } else {
                    toastr['error']("只有列名中含有“ID” 才可追加扩展字段");
                }

            }
        }, filters: {},
        computed: {
            computerArr: function () {
                var arr = applist.fieldList;
                return arr.filter(function (item) {
                    return $.trim(item.ColumnName).length > 0;
                });
            }
        }
    });
}


function setData(name) {
    var ConfigInfo = {
        TableName: $.trim($('#txtTableName').val()),
        TableComment: $.trim($('#txtTableComment').val()),
        ModelFolderName: $.trim($('#txtModelFolderName').val()),
        EntityName: $.trim($('#txtEntityName').val()),
        PageName: $.trim($('#txtPageName').val()),
        ControllerName: $.trim($('#txtEntityName').val()),
        FolderPath: $.trim($('#txtFolderPath').val()),
        EventName: name
    };
    var isVer = ['BuildEntity', 'CreateTable'].indexOf(name) >= 0;//不需要判断
    var flag = true;
    if (ConfigInfo.TableName.length <= 0) {
        toastr['error']("TableName 为空")
        flag = false;
    }
    if (ConfigInfo.ModelFolderName.length <= 0 && !isVer) {
        toastr['error']("ModelFolderName 为空")
        flag = false;
    } else {
        storage["ModelFolderName"] = ConfigInfo.ModelFolderName;
        storage["FolderPath"] = ConfigInfo.FolderPath;
    }
    if (ConfigInfo.PageName.length <= 0) {
        toastr['error']("PageName 为空")
        flag = false;
    }
    if (!flag) { return; }
    storage[ConfigInfo.TableName] = JSON.stringify(applist.computerArr);
    var data = applist.computerArr;
    flag = false;
    data.forEach(function (item, index) {
        if (item.Comment.length <= 0) {
            toastr['error']("第" + (index + 1) + "行 注释 为空！");
            flag = true;
        }
        if (item.TypeName.indexOf('char') >= 0 && item.MaxLength.length <= 0) {
            toastr['error']("第" + (index + 1) + "行 长度 为空！");
            flag = true;
        }
    });
    if (flag) { return; };
    $.post('/Home/SetData', { DataList: data, ConfigInfo: ConfigInfo }, function (msg) {
        if (msg.length > 0) {
            toastr['error'](msg);
            return;
        }
        if (name == 'CreateTable') {
            refreshList();
        }
        document.getElementById('iframe' + name).src = '/Home/' + name;
    });
}

//单击 表名 文本框 从缓存 获取数据
function getDB() {
    applist.fieldList = JSON.parse(storage[$('#txtTableName').val()]);
    toastr['warning']("单击清除缓存", $('#txtTableName').val() + " 表已加载缓存数据!");
};

//刷新 表 列表数据
function refreshList() {
    $.getJSON('/Ajax/gettablelist', function (data) {
        applist.tableList = data;
        $('#txtSearch').change();
        toastr['success']("列表已刷新！");
    })
}


function iframeBuildMenuSQl() {
    var menuid = $('#txtMenuID').val();
    if (menuid.length > 0) {
        document.getElementById('iframeBuildMenuSQl').src = '/Home/BuildMenuSql?id=' + menuid;
    }
}

function setCon() {
    $.post('/home/SetCon', { strcon: $('#defaultCon').val() }, function (data) {
        alert(data);
        location.href = location.href;
    })
}
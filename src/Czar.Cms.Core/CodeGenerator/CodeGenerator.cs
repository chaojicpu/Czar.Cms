﻿using Czar.Cms.Core.Models;
using Czar.Cms.Core.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Czar.Cms.Core.Extensions;
using System.Data;
using Dapper;
using System.Threading.Tasks;
using System.Data.SqlTypes;
using Czar.Cms.Core.DbHelper;

namespace Czar.Cms.Core.CodeGenerator
{
    /// <summary>
    /// yilezhu
    /// 2018.12.12
    /// 代码生成器。参考自：Zxw.Framework.NetCore
    /// <remarks>
    /// 根据数据库表以及表对应的列生成对应的数据库实体
    /// </remarks>
    /// </summary>
    public class CodeGenerator
    {
        private readonly string Delimiter = "\\";//分隔符，默认为windows下的\\分隔符

        private static CodeGenerateOption _options;
        public CodeGenerator(IOptions<CodeGenerateOption> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            _options = options.Value;
            if (_options.ConnectionString.IsNullOrWhiteSpace())
                throw new ArgumentNullException("不指定数据库连接串就生成代码，你想上天吗？");
            if (_options.DbType.IsNullOrWhiteSpace())
                throw new ArgumentNullException("不指定数据库类型就生成代码，你想逆天吗？");
            var path = AppDomain.CurrentDomain.BaseDirectory;
            if (_options.OutputPath.IsNullOrWhiteSpace())
                _options.OutputPath = path;
            var flag = path.IndexOf("/bin");
            if (flag > 0)
                Delimiter = "/";//如果可以取到值，修改分割符
        }

        /// <summary>
        /// 根据数据库连接字符串生成数据库表对应的Model层代码
        /// </summary>
        /// <param name="isCoveredExsited">是否覆盖已存在的同名文件</param>
        public void GenerateModelCodesFromDatabase(bool isCoveredExsited = true)
        {
            DatabaseType dbType = ConnectionFactory.GetDataBaseType(_options.DbType);
            List<DbTable> tables = new List<DbTable>();
            using (var dbConnection = ConnectionFactory.CreateConnection(dbType, _options.ConnectionString))
            {
                tables = dbConnection.GetCurrentDatabaseTableList(dbType);
            }

            if (tables != null && tables.Any())
            {
                foreach (var table in tables)
                {
                    if (table.Columns.Any(c => c.IsPrimaryKey))
                    {
                        GenerateEntity(table, isCoveredExsited);
                    }
                }
            }
        }


        private void GenerateEntity(DbTable table, bool isCoveredExsited = true)
        {
            var modelPath = _options.OutputPath;
            if (!Directory.Exists(modelPath))
            {
                Directory.CreateDirectory(modelPath);
            }

            var fullPath = modelPath + Delimiter + table.TableName + ".cs";
            if (File.Exists(fullPath) && !isCoveredExsited)
                return;

            var pkTypeName = table.Columns.First(m => m.IsPrimaryKey).CSharpType;
            var sb = new StringBuilder();
            foreach (var column in table.Columns)
            {
                var tmp = GenerateEntityProperty(table.TableName,column);
                sb.AppendLine(tmp);
            }
            var content = ReadTemplate("ModelTemplate.txt");
            content = content.Replace("{GeneratorTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{ModelsNamespace}", _options.ModelsNamespace)
                .Replace("{Author}", _options.Author)
                .Replace("{Comment}", table.TableComment)
                .Replace("{ModelName}", table.TableName)
                .Replace("{ModelProperties}", sb.ToString());
            WriteAndSave(fullPath, content);
        }

        /// <summary>
        /// 生成属性
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="column">列</param>
        /// <returns></returns>
        private static string GenerateEntityProperty(string tableName,DbTableColumn column)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(column.Comment))
            {
                sb.AppendLine("\t\t/// <summary>");
                sb.AppendLine("\t\t/// " + column.Comment);
                sb.AppendLine("\t\t/// </summary>");
            }
            if (column.IsPrimaryKey)
            {
                sb.AppendLine("\t\t[Key]");
                //if (column.IsIdentity)
                //{
                //    sb.AppendLine("\t\t[DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
                //}
                sb.AppendLine($"\t\tpublic {column.CSharpType} Id " + "{get;set;}");
            }
            else
            {
                if (!column.IsNullable)
                {
                    sb.AppendLine("\t\t[Required]");
                }

                //if (column.ColumnLength.HasValue && column.ColumnLength.Value > 0)
                //{
                //    sb.AppendLine($"\t\t[MaxLength({column.ColumnLength.Value})]");
                //}
                //if (column.IsIdentity)
                //{
                //    sb.AppendLine("\t\t[DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
                //}

                var colType = column.CSharpType;
                if (colType.ToLower() != "string" && colType.ToLower() != "byte[]" && colType.ToLower() != "object" &&
                    column.IsNullable)
                {
                    colType = colType + "?";
                }

                sb.AppendLine($"\t\tpublic {colType} {column.ColName} " + "{get;set;}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 从代码模板中读取内容
        /// </summary>
        /// <param name="templateName">模板名称，应包括文件扩展名称。比如：template.txt</param>
        /// <returns></returns>
        private string ReadTemplate(string templateName)
        {
            var currentAssembly = Assembly.GetExecutingAssembly();
            var content = string.Empty;
            using (var stream = currentAssembly.GetManifestResourceStream($"{currentAssembly.GetName().Name}.CodeTemplate.{templateName}"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        content = reader.ReadToEnd();
                    }
                }
            }
            return content;
        }

        /// <summary>
        /// 写文件
        /// </summary>
        /// <param name="fileName">文件完整路径</param>
        /// <param name="content">内容</param>
        private static void WriteAndSave(string fileName, string content)
        {
            //实例化一个文件流--->与写入文件相关联
            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                //实例化一个StreamWriter-->与fs相关联
                using (var sw = new StreamWriter(fs))
                {
                    //开始写入
                    sw.Write(content);
                    //清空缓冲区
                    sw.Flush();
                    //关闭流
                    sw.Close();
                    fs.Close();
                }
            }
        }

    }
}

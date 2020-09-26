﻿using Chloe;
using Chloe.Descriptors;
using Chloe.Infrastructure;
using Chloe.Oracle;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChloeDemo
{
    class OracleDemo : DemoBase
    {
        public OracleDemo()
        {
            DbConfiguration.UseTypeBuilders(typeof(OracleTestEntityMap));
        }

        protected override IDbContext CreateDbContext()
        {
            IDbContext dbContext = new OracleContext(new OracleConnectionFactory("Data Source=localhost/Chloe;User ID=system;Password=sasa;"));

            return dbContext;
        }

        public override void InitTable<TEntity>()
        {
            Type entityType = typeof(TEntity);
            var sqlList = this.CreateTableScript(entityType);

            foreach (var sql in sqlList)
            {
                this.DbContext.Session.ExecuteNonQuery(sql);
            }
        }
        List<string> CreateTableScript(Type entityType)
        {
            List<string> sqlList = new List<string>();
            TypeDescriptor typeDescriptor = EntityTypeContainer.GetDescriptor(entityType);
            string tableName = typeDescriptor.Table.Name;

            bool tableExists = this.DbContext.SqlQuery<int>($"select count(1) from user_tables where TABLE_NAME = '{tableName.ToUpper()}'").First() > 0;
            if (!tableExists)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"CREATE TABLE {this.QuoteName(tableName)}(");

                string c = "";
                foreach (var propertyDescriptor in typeDescriptor.PrimitivePropertyDescriptors)
                {
                    sb.AppendLine(c);
                    sb.Append($"  {this.BuildColumnPart(propertyDescriptor)}");
                    c = ",";
                }

                sb.AppendLine();
                sb.Append(")");

                sqlList.Add(sb.ToString());

                if (typeDescriptor.PrimaryKeys.Count > 0)
                {
                    string key = typeDescriptor.PrimaryKeys.First().Column.Name;
                    sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD CHECK ({this.QuoteName(key)} IS NOT NULL)");

                    sqlList.Add($"ALTER TABLE {this.QuoteName(tableName)} ADD PRIMARY KEY ({this.QuoteName(key)})");
                }
            }

            if (typeDescriptor.AutoIncrement != null)
            {
                string seqName = $"{tableName.ToUpper()}_{typeDescriptor.AutoIncrement.Column.Name.ToUpper()}_SEQ".ToUpper();
                bool seqExists = this.DbContext.SqlQuery<int>($"select count(*) from dba_sequences where SEQUENCE_NAME='{seqName}'").First() > 0;
                if (!seqExists)
                {
                    string seqScript = $"CREATE SEQUENCE {this.QuoteName(seqName)} INCREMENT BY 1 MINVALUE 1 MAXVALUE 9999999999999999999999999999 START WITH 1 CACHE 20";

                    sqlList.Add(seqScript);
                }

                string triggerName = $"{seqName.ToUpper()}_TRIGGER";
                string createTrigger = $@"create or replace trigger {triggerName} before insert on {tableName.ToUpper()} for each row 
begin 
select {seqName.ToUpper()}.nextval into :new.{typeDescriptor.AutoIncrement.Column.Name} from dual;
end;";

                sqlList.Add(createTrigger);
            }

            var seqProperties = typeDescriptor.PrimitivePropertyDescriptors.Where(a => a.HasSequence());
            foreach (var seqProperty in seqProperties)
            {
                if (seqProperty == typeDescriptor.AutoIncrement)
                {
                    continue;
                }

                string seqName = seqProperty.Definition.SequenceName;
                bool seqExists = this.DbContext.SqlQuery<int>($"select count(*) from dba_sequences where SEQUENCE_NAME='{seqName}'").First() > 0;

                if (!seqExists)
                {
                    string seqScript = $"CREATE SEQUENCE {this.QuoteName(seqName)} INCREMENT BY 1 MINVALUE 1 MAXVALUE 9999999999999999999999999999 START WITH 1 CACHE 20";
                    sqlList.Add(seqScript);
                }
            }

            return sqlList;
        }
        string QuoteName(string name)
        {
            return string.Concat("\"", name.ToUpper(), "\"");
        }

        string BuildColumnPart(PrimitivePropertyDescriptor propertyDescriptor)
        {
            string part = $"{this.QuoteName(propertyDescriptor.Column.Name)} {this.GetMappedDbTypeName(propertyDescriptor)}";

            if (!propertyDescriptor.IsNullable)
            {
                part += " NOT NULL";
            }
            else
            {
                part += " NULL";
            }

            return part;
        }

        string GetMappedDbTypeName(PrimitivePropertyDescriptor propertyDescriptor)
        {
            Type type = propertyDescriptor.PropertyType.GetUnderlyingType();
            if (type.IsEnum)
            {
                type = type.GetEnumUnderlyingType();
            }

            if (type == typeof(string))
            {
                int stringLength = propertyDescriptor.Column.Size ?? 2000;
                return $"NVARCHAR2({stringLength})";
            }

            if (type == typeof(int))
            {
                return "NUMBER(9,0)";
            }

            if (type == typeof(byte))
            {
                return "NUMBER(3,0)";
            }

            if (type == typeof(Int16))
            {
                return "NUMBER(4,0)";
            }

            if (type == typeof(long))
            {
                return "NUMBER(18,0)";
            }

            if (type == typeof(float))
            {
                return "BINARY_FLOAT";
            }

            if (type == typeof(double))
            {
                return "BINARY_DOUBLE";
            }

            if (type == typeof(decimal))
            {
                return "NUMBER";
            }

            if (type == typeof(bool))
            {
                return "NUMBER(9,0)";
            }

            if (type == typeof(DateTime))
            {
                return "DATE";
            }

            if (type == typeof(Guid))
            {
                return "BLOB";
            }

            throw new NotSupportedException(type.FullName);
        }

        public override void Method()
        {
            IQuery<Person> q = this.DbContext.Query<Person>();

            var space = new char[] { ' ' };

            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime.AddDays(1);
            var ret = q.Select(a => new
            {
                Id = a.Id,

                //CustomFunction = DbFunctions.MyFunction(a.Id), //自定义函数

                String_Length = (int?)a.Name.Length,//LENGTH("PERSON"."NAME")
                Substring = a.Name.Substring(0),//SUBSTR("PERSON"."NAME",0 + 1,LENGTH("PERSON"."NAME"))
                Substring1 = a.Name.Substring(1),//SUBSTR("PERSON"."NAME",1 + 1,LENGTH("PERSON"."NAME"))
                Substring1_2 = a.Name.Substring(1, 2),//SUBSTR("PERSON"."NAME",1 + 1,2)
                ToLower = a.Name.ToLower(),//LOWER("PERSON"."NAME")
                ToUpper = a.Name.ToUpper(),//UPPER("PERSON"."NAME")
                IsNullOrEmpty = string.IsNullOrEmpty(a.Name),//too long
                Contains = (bool?)a.Name.Contains("s"),//
                Trim = a.Name.Trim(),//TRIM("PERSON"."NAME")
                TrimStart = a.Name.TrimStart(space),//LTRIM("PERSON"."NAME")
                TrimEnd = a.Name.TrimEnd(space),//RTRIM("PERSON"."NAME")
                StartsWith = (bool?)a.Name.StartsWith("s"),//
                EndsWith = (bool?)a.Name.EndsWith("s"),//
                Replace = a.Name.Replace("l", "L"),

                /* oracle is not supported DbFunctions.Diffxx. */
                //DiffYears = DbFunctions.DiffYears(startTime, endTime),//
                //DiffMonths = DbFunctions.DiffMonths(startTime, endTime),//
                //DiffDays = DbFunctions.DiffDays(startTime, endTime),//
                //DiffHours = DbFunctions.DiffHours(startTime, endTime),//
                //DiffMinutes = DbFunctions.DiffMinutes(startTime, endTime),//
                //DiffSeconds = DbFunctions.DiffSeconds(startTime, endTime),//
                //DiffMilliseconds = DbFunctions.DiffMilliseconds(startTime, endTime),//
                //DiffMicroseconds = DbFunctions.DiffMicroseconds(startTime, endTime),//

                /* ((CAST(:P_0 AS DATE)-CAST(:P_1 AS DATE)) * 86400000 + CAST(TO_CHAR(CAST(:P_0 AS TIMESTAMP),'ff3') AS NUMBER) - CAST(TO_CHAR(CAST(:P_1 AS TIMESTAMP),'ff3') AS NUMBER)) / 86400000 */
                SubtractTotalDays = endTime.Subtract(startTime).TotalDays,//
                SubtractTotalHours = endTime.Subtract(startTime).TotalHours,//...
                SubtractTotalMinutes = endTime.Subtract(startTime).TotalMinutes,//...
                SubtractTotalSeconds = endTime.Subtract(startTime).TotalSeconds,//...
                SubtractTotalMilliseconds = endTime.Subtract(startTime).TotalMilliseconds,//...

                AddYears = startTime.AddYears(1),//ADD_MONTHS(:P_0,12 * 1)
                AddMonths = startTime.AddMonths(1),//ADD_MONTHS(:P_0,1)
                AddDays = startTime.AddDays(1),//(:P_0 + 1)
                AddHours = startTime.AddHours(1),//(:P_0 + NUMTODSINTERVAL(1,'HOUR'))
                AddMinutes = startTime.AddMinutes(2),//(:P_0 + NUMTODSINTERVAL(2,'MINUTE'))
                AddSeconds = startTime.AddSeconds(120),//(:P_0 + NUMTODSINTERVAL(120,'SECOND'))
                                                       //AddMilliseconds = startTime.AddMilliseconds(20000),//不支持

                Now = DateTime.Now,//SYSTIMESTAMP
                UtcNow = DateTime.UtcNow,//SYS_EXTRACT_UTC(SYSTIMESTAMP)
                Today = DateTime.Today,//TRUNC(SYSDATE,'DD')
                Date = DateTime.Now.Date,//TRUNC(SYSTIMESTAMP,'DD')
                Year = DateTime.Now.Year,//CAST(TO_CHAR(SYSTIMESTAMP,'yyyy') AS NUMBER)
                Month = DateTime.Now.Month,//CAST(TO_CHAR(SYSTIMESTAMP,'mm') AS NUMBER)
                Day = DateTime.Now.Day,//CAST(TO_CHAR(SYSTIMESTAMP,'dd') AS NUMBER)
                Hour = DateTime.Now.Hour,//CAST(TO_CHAR(SYSTIMESTAMP,'hh24') AS NUMBER)
                Minute = DateTime.Now.Minute,//CAST(TO_CHAR(SYSTIMESTAMP,'mi') AS NUMBER)
                Second = DateTime.Now.Second,//CAST(TO_CHAR(SYSTIMESTAMP,'ss') AS NUMBER)
                Millisecond = DateTime.Now.Millisecond,//CAST(TO_CHAR(SYSTIMESTAMP,'ff3') AS NUMBER)
                DayOfWeek = DateTime.Now.DayOfWeek,//(CAST(TO_CHAR(SYSTIMESTAMP,'D') AS NUMBER) - 1)

                Int_Parse = int.Parse("1"),//CAST(N'1' AS NUMBER)
                Int16_Parse = Int16.Parse("11"),//CAST(N'11' AS NUMBER)
                Long_Parse = long.Parse("2"),//CAST(N'2' AS NUMBER)
                Double_Parse = double.Parse("3"),//CAST(N'3' AS BINARY_DOUBLE)
                Float_Parse = float.Parse("4"),//CAST(N'4' AS BINARY_FLOAT)
                Decimal_Parse = decimal.Parse("5"),//CAST(N'5' AS NUMBER)
                                                   //Guid_Parse = Guid.Parse("D544BC4C-739E-4CD3-A3D3-7BF803FCE179"),//不支持

                Bool_Parse = bool.Parse("1"),//
                DateTime_Parse = DateTime.Parse("1992-1-16"),//TO_TIMESTAMP(N'1992-1-16','yyyy-mm-dd hh24:mi:ssxff')

                B = a.Age == null ? false : a.Age > 1, //三元表达式
                CaseWhen = Case.When(a.Id > 100).Then(1).Else(0) //case when
            }).ToList();

            ConsoleHelper.WriteLineAndReadKey();
        }

        public override void ExecuteCommandText()
        {
            List<Person> persons = this.DbContext.SqlQuery<Person>("select * from Person where Age > :age", DbParam.Create(":age", 12)).ToList();

            int rowsAffected = this.DbContext.Session.ExecuteNonQuery("update Person set name=:name where Id = 1", DbParam.Create(":name", "Chloe"));

            /* 
             * 执行存储过程:
             * Person person = context.SqlQuery<Person>("Proc_GetPerson", CommandType.StoredProcedure, DbParam.Create(":id", 1)).FirstOrDefault();
             * rowsAffected = context.Session.ExecuteNonQuery("Proc_UpdatePersonName", CommandType.StoredProcedure, DbParam.Create(":name", "Chloe"));
             */

            ConsoleHelper.WriteLineAndReadKey();
        }
    }
}

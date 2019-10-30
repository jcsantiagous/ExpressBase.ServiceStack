﻿using ExpressBase.Common;
using ExpressBase.Common.Constants;
using ExpressBase.Common.Data;
using ExpressBase.Common.ProductionDBManager;
using ExpressBase.Objects.ServiceStack_Artifacts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ExpressBase.ServiceStack.Services
{
    public class ProductionDBManagerServices : EbBaseService
    {
        public ProductionDBManagerServices(IEbConnectionFactory _dbf) : base(_dbf) { }
        Dictionary<string, string[]> dictTenant = new Dictionary<string, string[]>();
        Dictionary<string, Eb_FileDetails> dictInfra = new Dictionary<string, Eb_FileDetails>();

        public IDatabase GetTenantDB(string SolutionId)
        {
            IDatabase _ebconfactoryDatadb = null;
            EbConnectionFactory factory = new EbConnectionFactory(SolutionId, this.Redis, true);
            if (factory != null && factory.DataDB != null)
            {
                _ebconfactoryDatadb = factory.DataDB;
            }
            return _ebconfactoryDatadb;
        }

        public UpdateInfraWithSqlScriptsResponse Post(UpdateInfraWithSqlScriptsRequest request)
        {
            UpdateInfraWithSqlScriptsResponse resp = new UpdateInfraWithSqlScriptsResponse();
            SetFileMd5InfraReference();
            return resp;
        }

        public CheckChangesInFilesResponse Post(CheckChangesInFilesRequest request)
        {
            CheckChangesInFilesResponse resp = new CheckChangesInFilesResponse();
            CheckChangesInFilesResponse resp1 = new CheckChangesInFilesResponse();
            List<Eb_FileDetails> ChangesList = new List<Eb_FileDetails>();
            try
            {
                IDatabase _ebconfactoryDatadb = GetTenantDB(request.SolutionId);
                if (_ebconfactoryDatadb != null)
                {
                    GetFileScriptFromInfra(_ebconfactoryDatadb);
                    ChangesList = GetFileScriptFromTenant(_ebconfactoryDatadb);
                    if (request.IsUpdate)
                    {
                        resp.ModifiedDate = UpdateDBFunctionByDB(ChangesList, request.SolutionId);
                        request.IsUpdate = false;
                        resp1 = this.Post(request);
                        ChangesList = resp1.Changes;
                    }
                    resp.Changes = ChangesList;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return resp;
            }
            return resp;
        }

        public GetSolutionForIntegrityCheckResponse Post(GetSolutionForIntegrityCheckRequest request)
        {
            GetSolutionForIntegrityCheckResponse resp = new GetSolutionForIntegrityCheckResponse();
            List<Eb_Changes_Log> list = new List<Eb_Changes_Log>();
            string name = string.Empty;
            try
            {
                using (DbConnection con = this.InfraConnectionFactory.DataDB.GetNewConnection())
                {
                    con.Open();
                    string str = @"
                                SELECT * 
                                FROM eb_solutions
                                WHERE eb_del = false";
                    EbDataTable dt = InfraConnectionFactory.DataDB.DoQuery(str);
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        name = dt.Rows[i]["isolution_id"].ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            string str1 = string.Format(@"
                                   SELECT * 
                                   FROM eb_dbchangeslog
                                   WHERE solution_id = '{0}' ", name);
                            EbDataTable dt1 = InfraConnectionFactory.DataDB.DoQuery(str1);
                            if (dt1 != null && dt1.Rows.Count > 0)
                            {
                                string str2 = string.Format(@"
                                    SELECT d.modified_at, t.email , t.fullname 
                                    FROM eb_dbchangeslog as d, eb_tenants as t  
                                    WHERE d.solution_id = '{0}'
                                        AND  t.id = ( 
                                                        SELECT tenant_id 
                                                        FROM eb_solutions 
                                                        WHERE isolution_id = '{0}' 
                                                            AND eb_del = false)", name);
                                try
                                {
                                    EbDataTable dt2 = InfraConnectionFactory.DataDB.DoQuery(str2);
                                    if (dt2 != null && dt1.Rows.Count > 0)
                                    {
                                        list.Add(new Eb_Changes_Log
                                        {
                                            Solution = name,
                                            DBName = dt1.Rows[0]["dbname"].ToString(),
                                            TenantName = dt2.Rows[0]["fullname"].ToString(),
                                            TenantEmail = dt2.Rows[0]["email"].ToString(),
                                            Last_Modified = DateTime.Parse(dt2.Rows[0][0].ToString()),
                                            Vendor = dt1.Rows[0]["vendor"].ToString()
                                        });
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("ERROR : " + e.Message + " : " + e.StackTrace);
                                    continue;
                                }
                            }
                            else
                            {
                                try
                                {
                                    IDatabase _ebconfactoryDatadb = GetTenantDB(name);
                                    if (_ebconfactoryDatadb != null)
                                    {
                                        string str2 = string.Format(@"
                                                SELECT t.email, t.fullname, s.date_created 
                                                FROM eb_solutions as s, eb_tenants as t 
                                                WHERE s.isolution_id = '{0}'
                                                    AND s.eb_del = false
                                                    AND s.tenant_id = t.id", name);
                                        try
                                        {
                                            EbDataTable dt2 = InfraConnectionFactory.DataDB.DoQuery(str2);
                                            if (dt2.Rows.Count > 0)
                                            {
                                                string str3 = string.Format(@"
                                                                INSERT INTO 
                                                                    eb_dbchangeslog (solution_id, dbname, vendor, modified_at)
                                                                VALUES ('{0}','{1}','{2}','{3}')", name, _ebconfactoryDatadb.DBName, _ebconfactoryDatadb.Vendor, dt2.Rows[0]["date_created"].ToString());
                                                DbCommand cmd = InfraConnectionFactory.DataDB.GetNewCommand(con, str3);
                                                cmd.ExecuteNonQuery();
                                                list.Add(new Eb_Changes_Log
                                                {
                                                    Solution = name,
                                                    DBName = _ebconfactoryDatadb.DBName,
                                                    TenantName = dt2.Rows[0]["fullname"].ToString(),
                                                    TenantEmail = dt2.Rows[0]["email"].ToString(),
                                                    Last_Modified = DateTime.Parse(dt2.Rows[0]["date_created"].ToString()),
                                                    Vendor = _ebconfactoryDatadb.Vendor.ToString()
                                                });
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine("ERROR : " + e.Message + " : " + e.StackTrace);
                                            continue;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("ERROR : " + e.Message + " : " + e.StackTrace);
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("ERROR : isolution_id is NULL : id :" + dt.Rows[i]["id"].ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            resp.ChangesLog = list;
            return resp;
        }

        List<Eb_FileDetails> GetFileScriptFromTenant(IDatabase _ebconfactoryDatadb)
        {
            string str = string.Empty;
            StringBuilder hash = new StringBuilder();
            MD5CryptoServiceProvider md5provider = new MD5CryptoServiceProvider();
            string result = string.Empty;
            string file_name;
            string vendor = _ebconfactoryDatadb.Vendor.ToString();
            dictTenant.Clear();
            if (_ebconfactoryDatadb.Vendor == DatabaseVendors.PGSQL)
            {
                str = @"
                        SELECT pg_get_functiondef(oid)::text, proname 
                        FROM pg_proc 
                        WHERE proname 
                        IN 
                            (SELECT routine_name 
                            FROM information_schema.routines 
                            WHERE routine_type = 'FUNCTION' 
                                AND specific_schema = 'public')";
                EbDataTable dt = _ebconfactoryDatadb.DoQuery(str);
                if (dt != null && dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        result = dt.Rows[i][0].ToString();
                        file_name = GetFileName(result, dt.Rows[i][1].ToString(), "FUNCTION");
                        result = FormatDBStringPGSQL(result, "FUNCTION");
                        byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(result));
                        hash.Clear();
                        for (int j = 0; j < bytes.Length; j++)
                        {
                            hash.Append(bytes[j].ToString("x2"));
                        }
                        dictTenant.Add(file_name, new[] { vendor, hash.ToString(), "FUNCTION" });
                    }
                }

                str = @"
                            SELECT row_to_json(t)
                            FROM (
                              SELECT table_name,
                                (
                                  SELECT array_to_json(array_agg(row_to_json(d)))
                                  FROM (
                                    SELECT column_name, data_type
                                    FROM information_schema.columns
                                    WHERE table_name = C.table_name 
                                    ORDER BY column_name
                                  ) d
                                ) AS col
                              FROM information_schema.columns C
                             WHERE table_name LIKE 'eb_%'
	                            GROUP BY table_name
                            ) t";
                EbDataTable dt1 = _ebconfactoryDatadb.DoQuery(str);
                if (dt1 != null && dt1.Rows.Count > 0)
                {
                    for (int i = 0; i < dt1.Rows.Count; i++)
                    {
                        result = dt1.Rows[i][0].ToString();
                        file_name = GetFileName(result, null, "TABLE");
                        dictTenant.Add(file_name, new[] { vendor, result, "TABLE" });
                    }
                }
            }
            return CompareScripts(_ebconfactoryDatadb);
        }

        void GetFileScriptFromInfra(IDatabase _ebconfactoryDatadb)
        {
            string vendor = _ebconfactoryDatadb.Vendor.ToString();
            dictInfra.Clear();
            string str = string.Format(@"
                        SELECT d.change_id, d.filename, d.contents, c.filepath, c.type
                        FROM eb_dbmd5 as d, eb_dbstructure as c
                        WHERE c.vendor = '{0}'
                        AND d.eb_del = 'F'
                        AND c.id = d.change_id
                        AND (c.type='FUNCTION'
                        OR c.type='TABLE')
                        ORDER BY c.type", vendor);
            EbDataTable dt = InfraConnectionFactory.DataDB.DoQuery(str);
            if (dt != null && dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    dictInfra.Add(dt.Rows[i]["filename"].ToString(), new Eb_FileDetails
                    {
                        Id = dt.Rows[i]["change_id"].ToString(),
                        Vendor = vendor,
                        FilePath = dt.Rows[i]["filepath"].ToString(),
                        Content = dt.Rows[i]["contents"].ToString().Trim(),
                        Type = dt.Rows[i]["type"].ToString()
                    });
                }
            }
        }
        
        List<Eb_FileDetails> CompareScripts(IDatabase _ebconfactoryDatadb)
        {
            List<Eb_FileDetails> ChangesList = new List<Eb_FileDetails>();

            foreach (KeyValuePair<string, Eb_FileDetails> _infraitem in dictInfra)
            {
                if (dictTenant.TryGetValue(_infraitem.Key, out string[] _tenantitem))
                {
                    if ((_tenantitem != null) && (_tenantitem[1] != _infraitem.Value.Content))
                    {
                        ChangesList.Add(new Eb_FileDetails
                        {
                            Id = _infraitem.Value.Id,
                            FileHeader = _infraitem.Key,
                            FilePath = _infraitem.Value.FilePath,
                            Vendor = _infraitem.Value.Vendor,
                            Content = _infraitem.Value.Content,
                            Type = _infraitem.Value.Type,
                            NewItem = false
                        });
                    }
                }
                else
                {
                    ChangesList.Add(new Eb_FileDetails
                    {
                        Id = _infraitem.Value.Id,
                        FileHeader = _infraitem.Key,
                        FilePath = _infraitem.Value.FilePath,
                        Vendor = _infraitem.Value.Vendor,
                        Content = _infraitem.Value.Content,
                        Type = _infraitem.Value.Type,
                        NewItem = true
                    });
                }
            }
            return ChangesList;
        }

        void SetFileMd5InfraReference()
        {
            StringBuilder hash = new StringBuilder();
            string content = string.Empty;
            MD5CryptoServiceProvider md5provider = new MD5CryptoServiceProvider();
            string result = string.Empty;
            string file_name = string.Empty;
            string file_name_shrt = string.Empty;
            string type = string.Empty;
            string[] func_create = SqlScriptArrayConstant.SQLSCRIPTARRAY;

            foreach (string vendor in Enum.GetNames(typeof(DatabaseVendors)))
            {
                if (vendor == "PGSQL")
                {
                    string Urlstart = string.Format("ExpressBase.Common.sqlscripts.{0}.", vendor.ToLower());
                    foreach (string file in func_create)
                    {
                        string path = Urlstart + file;
                        var assembly = typeof(sqlscripts).Assembly;
                        using (Stream stream = assembly.GetManifestResourceStream(path))
                        {
                            if (stream != null)
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                    result = reader.ReadToEnd();
                                if (result.Split("\n").Length > 1)
                                {
                                    if (file.Split(".")[1] == "functioncreate")
                                    {
                                        type = "FUNCTION";
                                        file_name = GetFileName(result, file, type);
                                        file_name_shrt = file_name.Split("(")[0];
                                        if (vendor == "PGSQL")
                                        {
                                            result = FormatFileStringPGSQL(result, type);
                                        }
                                        byte[] bytes = md5provider.ComputeHash(new UTF8Encoding().GetBytes(result));
                                        hash.Clear();
                                        for (int j = 0; j < bytes.Length; j++)
                                        {
                                            hash.Append(bytes[j].ToString("x2"));
                                        }
                                        content = hash.ToString();
                                    }
                                    else if (file.Split(".")[1] == "tablecreate")
                                    {
                                        type = "TABLE";
                                        file_name = GetFileName(result, file, type);
                                        file_name_shrt = file_name;
                                        using (DbConnection con = this.InfraConnectionFactory.DataDB.GetNewConnection())
                                        {
                                            con.Open();
                                            string str = String.Format(@"
                                                            SELECT row_to_json(t)
                                                            FROM (
                                                              SELECT table_name,
                                                                (
                                                                  SELECT array_to_json(array_agg(row_to_json(d)))
                                                                  FROM (
                                                                    SELECT column_name, data_type
                                                                    FROM information_schema.columns
                                                                    WHERE table_name = C.table_name 
                                                                    ORDER BY column_name
                                                                  ) d
                                                                ) AS col
                                                              FROM information_schema.columns C
                                                             WHERE table_name = '{0}'
	                                                            GROUP BY table_name
                                                            ) t", file_name);
                                            EbDataTable dt = InfraConnectionFactory.DataDB.DoQuery(str);
                                            if (dt != null && dt.Rows.Count > 0)
                                            {
                                                content = dt.Rows[0][0].ToString();
                                            }
                                        }
                                    }
                                    using (DbConnection con = this.InfraConnectionFactory.DataDB.GetNewConnection())
                                    {
                                        con.Open();
                                        string str = string.Format(@"
                                                SELECT * 
                                                FROM eb_dbmd5 as d , eb_dbstructure as c
                                                WHERE d.filename = '{0}' 
                                                    AND d.eb_del = 'F' 
                                                    AND c.vendor = '{1}'
                                                    AND d.change_id = c.id", file_name, vendor);
                                        EbDataTable dt = InfraConnectionFactory.DataDB.DoQuery(str);
                                        if (dt != null && dt.Rows.Count > 0)
                                        {
                                            string str1 = string.Format(@"
                                            SELECT * 
                                            FROM eb_dbmd5 as d , eb_dbstructure as c
                                            WHERE  d.change_id = {0}
                                                AND c.id = {0}
                                                AND d.eb_del = 'F'
                                                AND d.contents <> '{1}'", dt.Rows[0]["change_id"].ToString(), content);
                                            EbDataTable dt1 = InfraConnectionFactory.DataDB.DoQuery(str1);
                                            if (dt1 != null && dt1.Rows.Count > 0)
                                            {
                                                string str2 = string.Format(@"
                                                    UPDATE eb_dbmd5 
                                                    SET eb_del = 'T'
                                                    WHERE filename = '{0}' 
                                                        AND eb_del = 'F'
                                                        AND change_id = '{1}'", file_name, dt1.Rows[0]["change_id"].ToString());
                                                DbCommand cmd = InfraConnectionFactory.DataDB.GetNewCommand(con, str2);
                                                cmd.ExecuteNonQuery();

                                                string str3 = string.Format(@"
                                                    INSERT INTO 
                                                        eb_dbmd5 (change_id, filename, contents, eb_del)
                                                    VALUES ('{0}','{1}','{2}','F')", dt1.Rows[0]["change_id"], file_name, content);
                                                DbCommand cmd1 = InfraConnectionFactory.DataDB.GetNewCommand(con, str3);
                                                cmd1.ExecuteNonQuery();
                                            }
                                        }
                                        else
                                        {
                                            string str1 = string.Format(@"
                                            INSERT INTO 
                                                eb_dbstructure (filename, filepath, vendor, type)
                                            VALUES ('{0}','{1}','{2}','{3}')", file_name_shrt, file, vendor, type);
                                            DbCommand cmd = InfraConnectionFactory.DataDB.GetNewCommand(con, str1);
                                            cmd.ExecuteNonQuery();

                                            string str2 = string.Format(@"
                                                INSERT INTO 
                                                    eb_dbmd5 (change_id, filename, contents, eb_del)
                                                VALUES ((SELECT id 
                                                FROM eb_dbstructure
                                                WHERE filename = '{0}'
                                                    AND vendor = '{1}'),'{2}','{3}','F')", file_name_shrt, vendor, file_name, content);
                                            DbCommand cmd1 = InfraConnectionFactory.DataDB.GetNewCommand(con, str2);
                                            cmd1.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        void UpdateDB(List<Eb_FileDetails> ChangesList , IDatabase _ebconfactoryDatadb)
        {
            string result = string.Empty;
            for (int i = 0; i < ChangesList.Count; i++)
            {
                if (ChangesList[i].Type == "FUNCTION")
                {
                    string Urlstart = string.Format("ExpressBase.Common.sqlscripts.{0}.", ChangesList[i].Vendor.ToLower());
                    string path = Urlstart + ChangesList[i].FilePath;
                    var assembly = typeof(sqlscripts).Assembly;
                    using (Stream stream = assembly.GetManifestResourceStream(path))
                    {
                        if (stream != null)
                        {
                            using (StreamReader reader = new StreamReader(stream))
                                result = reader.ReadToEnd();
                            string fun = GetFuncDef(result, ChangesList[i].FileHeader);
                            if (!ChangesList[i].NewItem)
                            {
                                using (DbConnection con = _ebconfactoryDatadb.GetNewConnection())
                                {
                                    con.Open();
                                    DbCommand cmd = _ebconfactoryDatadb.GetNewCommand(con, fun);
                                    int y = cmd.ExecuteNonQuery();
                                    con.Close();
                                }
                            }
                            using (DbConnection con1 = _ebconfactoryDatadb.GetNewConnection())
                            {
                                con1.Open();
                                DbCommand cmd1 = _ebconfactoryDatadb.GetNewCommand(con1, result);
                                int x = cmd1.ExecuteNonQuery();
                                con1.Close();
                            }
                        }
                    }
                }
                else if (ChangesList[i].Type == "TABLE")
                { 
                    JObject json = JObject.Parse(ChangesList[i].Content);
                    Eb_TableFieldChangesList infra_table_list = JsonConvert.DeserializeObject<Eb_TableFieldChangesList>(json.ToString());
                    //Dictionary<string, string> infra_table_dict = infra_table_list.Col.
                    Eb_TableFieldChangesList tenant_table_list = null;
                    string str = string.Format(@"
                            SELECT row_to_json(t)
                            FROM (
                              SELECT table_name,
                                (
                                  SELECT array_to_json(array_agg(row_to_json(d)))
                                  FROM (
                                    SELECT column_name, data_type
                                    FROM information_schema.columns
                                    WHERE table_name = C.table_name 
                                    ORDER BY column_name
                                  ) d
                                ) AS col
                              FROM information_schema.columns C
                             WHERE table_name = '{0}'
	                            GROUP BY table_name
                            ) t", ChangesList[i].FileHeader);
                    EbDataTable dt = _ebconfactoryDatadb.DoQuery(str);
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        result = dt.Rows[0][0].ToString();
                    }
                    tenant_table_list = JsonConvert.DeserializeObject<Eb_TableFieldChangesList>(result);
                    
                }
            }
        }

        string UpdateDBFunctionByDB(List<Eb_FileDetails> ChangesList, string Solution)
        {
            DateTime modified_date = DateTime.Now;
            UpdateDBFunctionByDBResponse resp = new UpdateDBFunctionByDBResponse();
            IDatabase _ebconfactoryDatadb = GetTenantDB(Solution);
            UpdateDB(ChangesList, _ebconfactoryDatadb);
            using (DbConnection con = InfraConnectionFactory.DataDB.GetNewConnection())
            {
                con.Open();
                string str1 = string.Format(@"
                                              UPDATE eb_dbchangeslog 
                                              SET modified_at = NOW()
                                              WHERE solution_id = '{0}'", Solution);
                DbCommand cmd2 = InfraConnectionFactory.DataDB.GetNewCommand(con, str1);
                cmd2.ExecuteNonQuery();
            }
            return modified_date.ToString();
        }

        string GetFuncDef(string str, string filename)
        {
            string[] split = str.Split("\r\n\r\n");
            if (split.Length > 1)
            {
                str = split[1];
                str = str.Replace("-- ", "").Replace(";", "");
            }
            else
            {
                str = "DROP FUNCTION " + filename;
            }
            return str;
        }

        string GetFileName(string str, string file, string type)
        {
            string[] fname;
            string res = string.Empty;
            if (type == "FUNCTION")
            {
                Regex regex = new Regex(@".*?\(.*?\)");
                str = str.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");
                MatchCollection matches = regex.Matches(str);
                if (matches.Count > 1)
                {
                    res = matches[0].Value.Contains("CREATE") ? matches[0].Value.Split(".")[1] : matches[2].Value.Split(".")[1];
                    res = res.Replace(" DEFAULT NULL::text", "").Replace(" DEFAULT NULL::integer", "").Replace(" DEFAULT 0", "");
                }
                else
                {
                    fname = file.Split(".");
                    res = fname.Length > 1 ? fname[2] + "()" : file + "()";
                }
                res = res.Replace(", ", ",").Trim();
            }
            else if (type == "TABLE" && file != null)
            {
                int x = file.Split(".").Length;
                res = str.Split(" ").Length > 1 ? str.Split(" ")[2] : file.Split(".")[file.Split(".").Length - 2];
                res = res.Remove(0, 7).Replace("\r", "").Replace("\n", "").Replace("--", "");
            }
            else if(type == "TABLE" && file == null)
            {
                res = str.Split("\"")[3];
            }
            return res;
        }
        
        string FormatDBStringPGSQL(string str, string type)
        {
            if (type == "FUNCTION")
            {
                str = str.Replace(" ", "").Replace("\r", "").Replace("\t", "").Replace("\n", "").Trim();
            }
            else if (type == "TABLE")
            {
                Regex regex = new Regex(@"integer DEFAULT nextval\(.*?\) NOT NULL");
                MatchCollection matches = regex.Matches(str);
                str = str.Replace(matches[0].ToString(), "serial").Replace("  NULL", "");
                str = str.Replace(" ", "").Replace("\r", "").Replace("\t", "").Replace("\n", "").Trim();
                str = str + ")";
            }
            return str;
        }

        string FormatFileStringPGSQL(string str, string type)
        {
            if (type == "FUNCTION")
            {
                str = str.Replace("$BODY$", "$function$");
                string[] split = str.Split("$function$");
                if (split.Length == 3)
                {
                    string[] split1 = split[0].Split("\r\n\r\n");
                    str = split1[2] + split[1] + "$function$";
                    str = str.Replace(" ", "").Replace("\r", "").Replace("\t", "").Replace("\n", "").Replace("'plpgsql'", "plpgsqlAS$function$").Replace("plpgsqlAS$function$AS", "plpgsqlAS$function$");
                }
                else if (split.Length == 1)
                {
                    str = "";
                }
            }
            else if (type == "TABLE")
            {
                string s = str;
                str = s;
                str = str.Split("\r\n\r\n")[2];
                str = str.Replace(" ", "").Replace("\r", "").Replace("\t", "").Replace("\n", "");
                if (str.IndexOf(",CONSTRAINT") > 0)
                    str = str.Remove(str.IndexOf(",CONSTRAINT"));
                str = str + ")";
            }
            return str;
        }

        string GetFuncDef(string str)
        {
            string[] split = str.Split("\r\n\r\n");
            if (split.Length > 1)
            {
                str = split[1];
                str = str.Replace("-- ", "").Replace(";", "");
            }
            else
            {
            }
            return str;
        }
    }
}


